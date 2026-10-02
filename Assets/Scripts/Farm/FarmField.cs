using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Why a field is waiting instead of growing. Each has a sentence for the player.</summary>
public enum PauseReason { None, ChooseDensity, Unprofitable, CannotProfit, NoFunds, NoEquipment, SoilRestored }

/// <summary>
/// The farming half of a parcel: its land, who owns it, what it is used for, the player's
/// cabbage settings, the crop growing now and the last one finished. It sits beside
/// <see cref="Parcel"/>, which keeps owning geometry and selection, so nothing here repaints
/// tiles or runs in the editor.
///
/// This class keeps state and changes it only when <see cref="Farm"/> tells it to. Farm owns
/// the money, the equipment sets, the research and the decision of what grows next, so every
/// rule about paying lives in one place.
///
/// A field is Ready (waiting for Start), Running (a paid crop is growing) or Paused (it wanted
/// to go on but a rule stopped it, with a reason). Only an explicit Start or Resume leaves Ready
/// or Paused; Repeat only decides whether a finished crop is followed by another on its own.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Parcel))]
public class FarmField : MonoBehaviour
{
    public enum Stage { Idle, Sowing, Growing, Finishing }
    public enum Use { Empty, Farm, Depot }
    public enum Status { Ready, Running, Paused }

    [Header("Identity")]
    [Tooltip("Stable name used to find this field in a save. Do not rename it once saves exist.")]
    [SerializeField] private string id = "A";

    [Header("Land")]
    [Tooltip("Fertility a fresh farm starts with, in percent. It changes as crops use and restore the soil.")]
    [SerializeField, Range(0, 100)] private int startFertility = 90;
    [Tooltip("Natural moisture in percent. It does not change in this prototype.")]
    [SerializeField, Range(0, 100)] private int moisturePercent = 25;
    [Tooltip("Whether this land can ever be farmed. Depleted farmland stays farmland; building-only land never becomes farmland.")]
    [SerializeField] private bool arable = true;

    [Header("Ownership")]
    [Tooltip("Whether a fresh farm already owns this land.")]
    [SerializeField] private bool ownedAtStart = true;
    [Tooltip("Coins to buy this land. 0 means it cannot be bought (it is granted, or never for sale).")]
    [SerializeField] private int purchasePrice;
    [Tooltip("Owning this land gives it a prepared crop plan and an equipment set, ready for Start.")]
    [FormerlySerializedAs("farmingAtStart")]
    [SerializeField] private bool preparedWhenOwned = true;
    [Tooltip("The introduction will hand this land over; one equipment set is kept free for it until then.")]
    [SerializeField] private bool reservedForIntroduction;

    [Header("Farming")]
    [Tooltip("The cabbage settings a fresh or replanted field starts with, in percent.")]
    [SerializeField] private FarmPlan starterCashPlan = new FarmPlan(50, 0, 0);

    private Parcel parcel;
    private float elapsed;

    public string Id { get { return id; } }
    public int StartingFertility { get { return startFertility; } }
    public int Moisture { get { return moisturePercent; } }
    public bool Arable { get { return arable; } }
    public int PurchasePrice { get { return purchasePrice; } }
    public bool ForSale { get { return !Owned && purchasePrice > 0; } }
    public bool PreparedWhenOwned { get { return preparedWhenOwned; } }
    public bool AwaitsGrant { get { return !Owned && reservedForIntroduction; } }
    public FarmPlan StarterPlan { get { return starterCashPlan; } }
    public Parcel Parcel { get { return parcel; } }
    public string DisplayName { get { return parcel != null ? parcel.DisplayName : name; } }

    // ---------- saved state ----------

    public bool Owned { get; private set; }
    public Use LandUse { get; private set; }
    /// <summary>Current fertility in whole percentage points, 0..100.</summary>
    public int Fertility { get; private set; }
    /// <summary>The confirmed cabbage settings. Kept while the field restores soil.</summary>
    public FarmPlan CashPlan { get; private set; }
    /// <summary>The crop the player chose by hand. A working soil controller overrides it.</summary>
    public CropKind ManualCrop { get; private set; }
    public bool Repeat { get; private set; }
    /// <summary>The equipment set working this field, 0 for none. Sets are numbered from 1.</summary>
    public int AssignedSet { get; private set; }
    public Status State { get; private set; }
    public PauseReason Pause { get; private set; }
    /// <summary>Clear field was asked for: the running crop finishes, then the land is emptied.</summary>
    public bool PendingClear { get; private set; }

    public bool ControllerInstalled { get; private set; }
    public bool ControllerEnabled { get; private set; }
    /// <summary>The controller's mode: true while it is restoring soil.</summary>
    public bool ControllerRecovering { get; private set; }

    /// <summary>The paid crop growing now, frozen at Start. Null while nothing runs.</summary>
    public CropForecast Active { get; private set; }
    /// <summary>The last crop that finished. History only, never a preview.</summary>
    public CropForecast Last { get; private set; }

    public int CashCompleted { get; private set; }
    public int RecoveryCompleted { get; private set; }
    public int ManualRecoveryCompleted { get; private set; }

    public bool Running { get { return State == Status.Running; } }
    public bool Farming { get { return Owned && LandUse == Use.Farm; } }
    /// <summary>A working controller: installed, switched on and on farmed land.</summary>
    public bool ControllerActive { get { return ControllerInstalled && ControllerEnabled && Farming; } }
    public float Elapsed { get { return elapsed; } }

    private void Awake()
    {
        parcel = GetComponent<Parcel>();
        ResetToFreshFarm();
    }

    private void ResetToFreshFarm()
    {
        Owned = ownedAtStart;
        Fertility = startFertility;
        CashPlan = starterCashPlan;
        ManualCrop = CropKind.Cash;
        LandUse = ownedAtStart && preparedWhenOwned && arable ? Use.Farm : Use.Empty;
        State = Status.Ready;
    }

    public Stage CurrentStage
    {
        get
        {
            if (!Running || Active == null) return Stage.Idle;
            if (elapsed < Active.sowSeconds) return Stage.Sowing;
            if (elapsed < Active.sowSeconds + Active.growSeconds) return Stage.Growing;
            return Stage.Finishing;
        }
    }

    /// <summary>How far through its current stage the crop is, 0 to 1.</summary>
    public float StageProgress
    {
        get
        {
            if (!Running || Active == null) return 0f;
            switch (CurrentStage)
            {
                case Stage.Sowing: return elapsed / Active.sowSeconds;
                case Stage.Growing: return (elapsed - Active.sowSeconds) / Active.growSeconds;
                default: return (elapsed - Active.sowSeconds - Active.growSeconds) / Active.finishSeconds;
            }
        }
    }

    public float SecondsLeftInStage
    {
        get
        {
            if (!Running || Active == null) return 0f;
            switch (CurrentStage)
            {
                case Stage.Sowing: return Active.sowSeconds - elapsed;
                case Stage.Growing: return Active.sowSeconds + Active.growSeconds - elapsed;
                default: return Active.CycleSeconds - elapsed;
            }
        }
    }

    // ---------- tools for Farm, which checks money, equipment and rules first ----------

    public void TakeOwnership() { Owned = true; }

    public void Prepare(int set)
    {
        LandUse = Use.Farm;
        AssignedSet = set;
        CashPlan = starterCashPlan;
        ManualCrop = CropKind.Cash;
        State = Status.Ready;
        Pause = PauseReason.None;
        PendingClear = false;
    }

    public void AssignSet(int set) { AssignedSet = set; }
    public void SetDepot(bool built) { LandUse = built ? Use.Depot : Use.Empty; }
    public void SetRepeat(bool repeat) { Repeat = repeat; }
    public void SetPendingClear(bool clear) { PendingClear = clear && Farming; }

    public void ConfirmPlan(FarmPlan plan, CropKind crop)
    {
        CashPlan = plan;
        ManualCrop = crop;
    }

    public void InstallController()
    {
        ControllerInstalled = true;
        ControllerEnabled = true;
        ControllerRecovering = false;
    }

    public void SetControllerEnabled(bool on) { if (ControllerInstalled) ControllerEnabled = on; }

    /// <summary>Starts a paid (or free) crop from its frozen numbers.</summary>
    public void Begin(CropForecast snapshot, bool controllerRecovering)
    {
        Active = snapshot;
        elapsed = 0f;
        State = Status.Running;
        Pause = PauseReason.None;
        ControllerRecovering = controllerRecovering;
    }

    public void Hold(PauseReason reason, bool controllerRecovering)
    {
        State = Status.Paused;
        Pause = reason;
        ControllerRecovering = controllerRecovering;
    }

    public void BecomeReady()
    {
        State = Status.Ready;
        Pause = PauseReason.None;
    }

    /// <summary>Moves the clock. Returns true on the frame the crop's time is up.</summary>
    public bool Advance(float deltaTime)
    {
        if (!Running || Active == null) return false;
        elapsed += deltaTime;
        return elapsed >= Active.CycleSeconds;
    }

    /// <summary>
    /// Applies the frozen result to the soil exactly once and files it as history. Money is
    /// paid out by Farm from the same snapshot.
    /// </summary>
    public CropForecast Finish()
    {
        CropForecast done = Active;
        if (!done.legacy) Fertility = Mathf.Clamp(done.endFertility, 0, 100);
        if (done.kind == CropKind.Cash) CashCompleted++;
        else
        {
            RecoveryCompleted++;
            if (!done.automatic) ManualRecoveryCompleted++;
        }
        Last = done;
        Active = null;
        elapsed = 0f;
        State = Status.Ready;
        return done;
    }

    /// <summary>Empties the land: plan reset, set released. An installed controller stays.</summary>
    public void Clear()
    {
        LandUse = Use.Empty;
        AssignedSet = 0;
        CashPlan = starterCashPlan;
        ManualCrop = CropKind.Cash;
        Repeat = false;
        PendingClear = false;
        State = Status.Ready;
        Pause = PauseReason.None;
        ControllerRecovering = false;
    }

    // ---------- saving ----------

    public FieldSaveV3 Capture()
    {
        FieldSaveV3 d = new FieldSaveV3();
        d.id = id;
        d.owned = Owned;
        d.use = (int)LandUse;
        d.assignedSet = AssignedSet;
        d.fertility = Fertility;
        d.cashPlan = CashPlan;
        d.manualCrop = (int)ManualCrop;
        d.repeat = Repeat;
        d.status = (int)State;
        d.pause = (int)Pause;
        d.pendingClear = PendingClear;
        d.controllerInstalled = ControllerInstalled;
        d.controllerEnabled = ControllerEnabled;
        d.controllerRecovering = ControllerRecovering;
        d.hasActive = Active != null;
        d.active = Active != null ? Active : new CropForecast();
        d.elapsed = elapsed;
        d.hasLast = Last != null;
        d.last = Last != null ? Last : new CropForecast();
        d.cashCompleted = CashCompleted;
        d.recoveryCompleted = RecoveryCompleted;
        d.manualRecoveryCompleted = ManualRecoveryCompleted;
        return d;
    }

    /// <summary>
    /// Puts back a saved state exactly, including a crop already paid for, so reopening the
    /// game continues that crop instead of charging for it again.
    /// </summary>
    public void Restore(FieldSaveV3 d)
    {
        Owned = d.owned;
        LandUse = d.owned ? (Use)d.use : Use.Empty;
        AssignedSet = d.assignedSet;
        Fertility = d.fertility;
        CashPlan = d.cashPlan;
        ManualCrop = (CropKind)d.manualCrop;
        Repeat = d.repeat;
        State = (Status)d.status;
        Pause = (PauseReason)d.pause;
        PendingClear = d.pendingClear && LandUse == Use.Farm;
        ControllerInstalled = d.controllerInstalled;
        ControllerEnabled = d.controllerEnabled;
        ControllerRecovering = d.controllerRecovering;
        Active = d.hasActive ? d.active : null;
        elapsed = d.hasActive ? d.elapsed : 0f;
        if (State == Status.Running && Active == null) State = Status.Ready;
        if (State != Status.Running) Active = null;
        Last = d.hasLast ? d.last : null;
        CashCompleted = d.cashCompleted;
        RecoveryCompleted = d.recoveryCompleted;
        ManualRecoveryCompleted = d.manualRecoveryCompleted;
    }
}
