using UnityEngine;

/// <summary>
/// The farming half of a parcel: what the land is like, who owns it, what it is used for, the
/// plan the player saved for it and the crop currently growing. It sits beside
/// <see cref="Parcel"/>, which keeps owning geometry and selection, so nothing here repaints
/// the parcel's tiles or runs in the editor.
///
/// Only parcels that take part in the farm carry this component. A parcel without it is
/// scenery the player can still pick and look at.
///
/// A crop cycle, driven by <see cref="Farm"/> calling <see cref="Tick"/> every frame:
///   1. Not running: take a copy of the saved plan and pay its costs. No money, no start.
///   2. Running: the clock goes through sowing, growing and harvesting.
///   3. Clock done: work out the harvest from the copied plan, sell it, stop running.
/// The next Tick starts again from step 1, so a field keeps farming on its own. A plan saved
/// mid-crop only changes what step 1 copies next time; the paid-for crop keeps its own plan.
///
/// Land use (buying, planting, the depot, stopping) is decided by <see cref="Farm"/>, which
/// also owns the money and the equipment sets; the setters here are only its tools.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Parcel))]
public class FarmField : MonoBehaviour
{
    public enum Stage { Idle, Sowing, Growing, Harvesting }
    public enum Use { Empty, Farm, Depot }

    [Header("Identity")]
    [Tooltip("Stable name used to find this field in a save. Do not rename it once saves exist.")]
    [SerializeField] private string id = "A";

    [Header("Land")]
    [Tooltip("Natural soil fertility, 0 to 1. Below the rules' planting threshold only building is offered.")]
    [SerializeField, Range(0f, 1f)] private float fertility = 0.9f;
    [Tooltip("Natural soil moisture, 0 to 1. Stays the same while a plan is being compared.")]
    [SerializeField, Range(0f, 1f)] private float moisture = 0.25f;

    [Header("Ownership")]
    [Tooltip("Whether a fresh farm already owns this land.")]
    [SerializeField] private bool ownedAtStart = true;
    [Tooltip("Coins to buy this land. 0 means it cannot be bought (it is granted, or never for sale).")]
    [SerializeField] private int purchasePrice;

    [Header("Farming")]
    [Tooltip("The plan a fresh farm, or a replanted field, starts with.")]
    [SerializeField] private FarmPlan starterPlan = new FarmPlan(Density.Standard, Care.Off, Care.Off);
    [Tooltip("Whether this land is farmed as soon as it is owned (a fresh farm, or a granted field).")]
    [SerializeField] private bool farmingAtStart = true;

    private Parcel parcel;
    private float cycleTime;

    public string Id { get { return id; } }
    public float Fertility { get { return fertility; } }
    public float Moisture { get { return moisture; } }
    public int PurchasePrice { get { return purchasePrice; } }
    public bool ForSale { get { return !Owned && purchasePrice > 0; } }

    /// <summary>Whether owning this land starts farming it straight away.</summary>
    public bool FarmsWhenOwned { get { return farmingAtStart; } }

    /// <summary>Land the introduction will hand over: its equipment set is kept for it.</summary>
    public bool AwaitsGrant { get { return !Owned && farmingAtStart && purchasePrice == 0; } }
    public FarmPlan StarterPlan { get { return starterPlan; } }
    public Parcel Parcel { get { return parcel; } }
    public string DisplayName { get { return parcel != null ? parcel.DisplayName : name; } }

    /// <summary>The plan the player confirmed. Unsaved edits live in the UI, never here.</summary>
    public FarmPlan Plan { get; private set; }

    public bool Owned { get; private set; }
    public Use LandUse { get; private set; }

    /// <summary>Whether this field grows crops (it may be finishing its last one).</summary>
    public bool Farming { get { return Owned && LandUse == Use.Farm; } }

    /// <summary>The equipment set working this field, 0 for none. Sets are numbered from 1.</summary>
    public int AssignedSet { get; private set; }

    /// <summary>The player asked to stop farming: the running crop is finished and sold first.</summary>
    public bool PendingStop { get; private set; }

    /// <summary>True from the moment a crop is paid for until it is sold.</summary>
    public bool Running { get; private set; }

    /// <summary>The copy of the plan the current crop was paid for.</summary>
    public FarmPlan ActivePlan { get; private set; }

    /// <summary>Set while a new crop is due but the wallet cannot cover it.</summary>
    public bool WaitingForMoney { get; private set; }

    public bool HasResult { get; private set; }
    public HarvestResult LastResult { get; private set; }
    public int CropsSold { get; private set; }

    /// <summary>Seconds into the current crop, 0 when none is running.</summary>
    public float CycleTime { get { return cycleTime; } }

    private void Awake()
    {
        parcel = GetComponent<Parcel>();
        Plan = starterPlan;
        Owned = ownedAtStart;
        LandUse = ownedAtStart && farmingAtStart ? Use.Farm : Use.Empty;
    }

    public void ConfirmPlan(FarmPlan plan)
    {
        Plan = plan;
    }

    // ---------- tools for Farm, which checks money and equipment first ----------

    /// <summary>Hands the land over. For a granted field this also starts farming it.</summary>
    public void TakeOwnership(bool startFarming)
    {
        Owned = true;
        if (startFarming) StartFarming(AssignedSet);
    }

    public void AssignSet(int set) { AssignedSet = set; }

    public void StartFarming(int set)
    {
        LandUse = Use.Farm;
        AssignedSet = set;
        Plan = starterPlan;
        PendingStop = false;
    }

    public void SetDepot(bool built)
    {
        LandUse = built ? Use.Depot : Use.Empty;
    }

    public void RequestStop(bool stop)
    {
        if (LandUse == Use.Farm) PendingStop = stop;
    }

    public Stage CurrentStage(FarmRules rules)
    {
        if (!Running) return Stage.Idle;
        if (cycleTime < rules.SowSeconds) return Stage.Sowing;
        if (cycleTime < rules.SowSeconds + rules.GrowSeconds) return Stage.Growing;
        return Stage.Harvesting;
    }

    /// <summary>How far through its current stage the crop is, 0 to 1.</summary>
    public float StageProgress(FarmRules rules)
    {
        switch (CurrentStage(rules))
        {
            case Stage.Sowing: return cycleTime / rules.SowSeconds;
            case Stage.Growing: return (cycleTime - rules.SowSeconds) / rules.GrowSeconds;
            case Stage.Harvesting:
                return (cycleTime - rules.SowSeconds - rules.GrowSeconds) / rules.HarvestSeconds;
            default: return 0f;
        }
    }

    public FieldSaveData Capture()
    {
        FieldSaveData d = new FieldSaveData();
        d.id = id;
        d.owned = Owned;
        d.farming = LandUse == Use.Farm;
        d.use = (int)LandUse;
        d.assignedSet = AssignedSet;
        d.pendingStop = PendingStop;
        d.plan = Plan;
        d.running = Running;
        d.cycleTime = cycleTime;
        d.activePlan = ActivePlan;
        d.hasResult = HasResult;
        d.lastResult = LastResult;
        d.cropsSold = CropsSold;
        return d;
    }

    /// <summary>
    /// Puts back a saved state exactly, including a crop that was already paid for, so
    /// reopening the game continues that crop instead of charging for it again.
    /// </summary>
    public void Restore(FieldSaveData d)
    {
        Owned = d.owned;
        LandUse = d.owned ? (Use)d.use : Use.Empty;
        AssignedSet = d.assignedSet;
        PendingStop = d.pendingStop && LandUse == Use.Farm;
        Plan = d.plan;
        Running = d.running && LandUse == Use.Farm;
        cycleTime = Running ? d.cycleTime : 0f;
        ActivePlan = d.activePlan;
        HasResult = d.hasResult;
        LastResult = d.lastResult;
        CropsSold = d.cropsSold;
        WaitingForMoney = false;
    }

    /// <summary>
    /// Moves the crop along. Returns true when money or land use changed this call, so the
    /// farm saves at once.
    /// </summary>
    public bool Tick(Farm farm, float deltaTime)
    {
        if (!Farming) return false;
        FarmRules rules = farm.Rules;

        if (!Running)
        {
            // A requested stop takes effect between crops: nothing is running, nothing is lost.
            if (PendingStop)
            {
                LandUse = Use.Empty;
                PendingStop = false;
                WaitingForMoney = false;
                AssignedSet = 0;
                Plan = starterPlan;
                return true;
            }

            // Step 1: pay for the next crop, or wait until the wallet can.
            int cost = rules.Costs(Plan).Cost;
            WaitingForMoney = !farm.TrySpend(cost);
            if (WaitingForMoney) return false;

            ActivePlan = Plan;
            cycleTime = 0f;
            Running = true;
            return true;
        }

        // Step 2: let the clock run.
        cycleTime += deltaTime;
        if (cycleTime < rules.CycleSeconds) return false;

        // Step 3: sell once, then stop so the next Tick starts a new crop (or the stop).
        HarvestResult result = rules.Evaluate(ActivePlan, fertility, moisture);
        farm.Earn(result.income);
        LastResult = result;
        HasResult = true;
        CropsSold++;
        Running = false;
        cycleTime = 0f;
        return true;
    }
}
