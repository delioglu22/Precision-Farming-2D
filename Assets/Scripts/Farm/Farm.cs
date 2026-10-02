using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The farm as a business: one wallet, the equipment sets, research, land decisions, the
/// clock that moves every crop along, and saving. Unity has no production, accounting or
/// tutorial system to lean on, so this is plain game logic; the UI and map only ask and show.
///
/// Money moves only in this class: crop costs when a crop starts, income when cabbage is
/// sold, and the one-off prices of land, the depot, research and soil controllers. Every one
/// of those re-checks its own conditions first, so a second tap finds the thing already done.
///
/// What grows next is decided by <see cref="Decide"/>, which changes nothing. The page's
/// preview, an explicit Start and the automatic follow-up after a harvest all call it, so the
/// preview is always the crop that would really start.
///
/// Operating money is protected: each field holding an equipment set keeps the cost of its
/// next cabbage crop in reserve. Land, buildings, research and controllers may only spend what
/// is left above that reserve. Starting a crop may use its own reserve but never another
/// field's. Restoration costs nothing, so a worn-out field can always recover.
/// </summary>
[DisallowMultipleComponent]
public class Farm : MonoBehaviour
{
    /// <summary>The answer to "what would this field grow next, and may it?".</summary>
    public struct Decision
    {
        public CropKind kind;
        public CropForecast forecast;
        public PauseReason reason;
        /// <summary>The controller's mode if this decision is carried out.</summary>
        public bool recovering;
        public bool byController;
    }

    public enum ResearchState { Owned, Available, Locked }

    [Header("Rules")]
    [SerializeField] private FarmRules rules;
    [Tooltip("How the UI scene finds this farm.")]
    [SerializeField] private FarmChannel channel;

    [Header("Fields")]
    [SerializeField] private FarmField[] fields;

    [Header("Introduction")]
    [SerializeField] private FarmIntro intro;

    [Header("Saving")]
    [Tooltip("File name inside Application.persistentDataPath.")]
    [SerializeField] private string saveFileName = "farm.json";
    [Tooltip("Seconds between saves of the crops' progress. Any decision or payment saves at once.")]
    [SerializeField] private float autosaveSeconds = 5f;

    private readonly HashSet<string> research = new HashSet<string>();
    private float sinceSave;
    private bool resetting;

    public FarmRules Rules { get { return rules; } }
    public FarmIntro Intro { get { return intro; } }
    public FarmField[] Fields { get { return fields; } }
    public int Coins { get; private set; }
    public FarmField DepotField { get; private set; }
    public int InstallCredits { get; private set; }
    public bool CreditGranted { get; private set; }
    /// <summary>How often a soil controller has handed a restored field back to cabbage.</summary>
    public int AutomaticReturns { get; private set; }

    /// <summary>
    /// Set when a save file exists but cannot be used. The farm then stands still and never
    /// saves, so the file is not overwritten, until the player chooses to start a new farm.
    /// </summary>
    public string LoadProblem { get; private set; }

    private void Awake()
    {
        Coins = rules != null ? rules.StartingCoins : 0;
    }

    // Start, not Awake: every field has set up its own defaults in Awake by now, and the
    // saved state is laid over those defaults. Start still runs before any Update.
    private void Start()
    {
        Load();
        if (LoadProblem != null) return;
        foreach (FarmField f in fields)
            if (f != null && f.LandUse == FarmField.Use.Depot) DepotField = f;
        // A fresh farm (or an older save) has prepared fields without a set yet.
        foreach (FarmField f in fields)
            if (f != null && f.Farming && f.AssignedSet == 0) f.AssignSet(NextFreeSet());
        // A clear asked for while nothing was growing takes effect now.
        foreach (FarmField f in fields)
            if (f != null && f.PendingClear && !f.Running) f.Clear();
    }

    private void OnEnable() { if (channel != null) channel.Register(this); }

    // Leaving Play Mode or unloading the map always passes through here, which makes it the
    // dependable last save in the editor. On a phone, OnApplicationPause is the one that counts.
    private void OnDisable()
    {
        Save();
        if (channel != null) channel.Unregister(this);
    }

    private void OnApplicationPause(bool paused) { if (paused) Save(); }
    private void OnApplicationQuit() { Save(); }

    // Time.deltaTime stops while the app is suspended and is capped after a long frame,
    // so time away produces nothing - the prototype's "no offline progress" rule.
    private void Update()
    {
        if (rules == null || LoadProblem != null) return;

        bool changed = false;
        foreach (FarmField f in fields)
        {
            if (f == null || !f.Advance(Time.deltaTime)) continue;
            Settle(f);
            Continue(f);
            changed = true;
        }

        sinceSave += Time.deltaTime;
        if (changed || sinceSave >= autosaveSeconds) Save();
    }

    // ---------- the crop cycle ----------

    // Pays out and changes the soil exactly once, from the snapshot frozen at Start.
    private void Settle(FarmField f)
    {
        CropForecast done = f.Finish();
        if (done.kind == CropKind.Cash) Coins += done.income;
    }

    // After a crop: clear the land, wait, or try the next crop, in that order of priority.
    private void Continue(FarmField f)
    {
        if (f.PendingClear) { f.Clear(); return; }
        if (!f.Repeat) { f.BecomeReady(); return; }

        Decision d = Decide(f, f.CashPlan, f.ManualCrop, f.Fertility);
        PauseReason blocked = d.reason;
        if (blocked == PauseReason.None) blocked = MoneyOrEquipmentProblem(f, d);
        if (blocked != PauseReason.None) { f.Hold(blocked, d.recovering); return; }
        Begin(f, d);
    }

    private void Begin(FarmField f, Decision d)
    {
        if (d.kind == CropKind.Cash) Coins -= d.forecast.Cost;
        if (f.ControllerRecovering && !d.recovering && d.byController) AutomaticReturns++;
        CropForecast snapshot = d.forecast.Copy();
        snapshot.automatic = d.byController;
        f.Begin(snapshot, d.recovering);
    }

    private PauseReason MoneyOrEquipmentProblem(FarmField f, Decision d)
    {
        if (f.AssignedSet == 0 && FreeSets <= 0) return PauseReason.NoEquipment;
        if (d.kind == CropKind.Cash && d.forecast.Cost + ReserveOfOthers(f) > Coins) return PauseReason.NoFunds;
        return PauseReason.None;
    }

    /// <summary>
    /// Works out what the field would grow next with these settings, without changing anything.
    /// <paramref name="fertility"/> is the soil the crop would start on: the current soil for an
    /// idle field, the running crop's expected end soil for the next one.
    /// </summary>
    public Decision Decide(FarmField f, FarmPlan plan, CropKind manualCrop, int fertility)
    {
        Decision d = new Decision();
        d.recovering = f.ControllerRecovering;
        CropForecast cash = Forecast(f, CropKind.Cash, plan, fertility);
        CropForecast restore = Forecast(f, CropKind.Recovery, plan, fertility);

        bool controller = f.ControllerInstalled && f.ControllerEnabled;
        d.byController = controller;
        if (!controller)
        {
            d.kind = manualCrop;
            d.forecast = manualCrop == CropKind.Cash ? cash : restore;
            if (manualCrop == CropKind.Recovery) { if (fertility >= 100) d.reason = PauseReason.SoilRestored; }
            else if (plan.density <= 0) d.reason = PauseReason.ChooseDensity;
            else if (cash.Net <= 0) d.reason = PauseReason.Unprofitable;
            return d;
        }

        d.kind = CropKind.Cash;
        d.forecast = cash;
        if (plan.density <= 0) { d.reason = PauseReason.ChooseDensity; return d; }

        bool richSoilProfits = Forecast(f, CropKind.Cash, plan, 100).Net > 0;
        bool recovering = f.ControllerRecovering;

        // Cash mode: restore when the soil is low or the next cabbage would not pay.
        if (!recovering && (fertility < rules.RestoreBelow || cash.Net <= 0))
        {
            if (!richSoilProfits) { d.reason = PauseReason.CannotProfit; return d; }
            recovering = true;
        }

        // Restoring: hand back only above the upper threshold and when cabbage pays there.
        if (recovering)
        {
            if (fertility > rules.ReturnAbove && cash.Net > 0) recovering = false;
            else if (!richSoilProfits) { d.reason = PauseReason.CannotProfit; d.recovering = true; return d; }
            else if (fertility >= 100) { d.reason = PauseReason.SoilRestored; d.recovering = true; return d; }
        }

        d.recovering = recovering;
        d.kind = recovering ? CropKind.Recovery : CropKind.Cash;
        d.forecast = recovering ? restore : cash;
        return d;
    }

    /// <summary>The soil the field's next crop would start on.</summary>
    public int NextStartFertility(FarmField f)
    {
        if (f.Running && f.Active != null && !f.Active.legacy) return f.Active.endFertility;
        return f.Fertility;
    }

    public CropForecast Forecast(FarmField f, CropKind kind, FarmPlan plan, int fertility)
    {
        return rules.Forecast(kind, plan, fertility, f.Moisture, CurrentDiscounts);
    }

    /// <summary>Null when Start would work, otherwise the reason it would not.</summary>
    public string WhyCannotStart(FarmField f, FarmPlan plan, CropKind crop)
    {
        if (!f.Owned) return "Buy this land first.";
        if (!f.Arable) return "This land cannot be farmed. You can build here instead.";
        if (f.LandUse == FarmField.Use.Depot) return "The depot stands here.";
        if (f.Running) return "A crop is already growing.";
        Decision d = Decide(f, plan, crop, f.Fertility);
        if (d.reason != PauseReason.None) return ReasonText(f, d.reason, d);
        PauseReason other = MoneyOrEquipmentProblem(f, d);
        if (other != PauseReason.None) return ReasonText(f, other, d);
        return null;
    }

    /// <summary>
    /// The explicit Start (or Resume). Confirms the settings, takes an equipment set if the land
    /// has none, pays once and begins - all or nothing.
    /// </summary>
    public bool StartField(FarmField f, FarmPlan plan, CropKind crop)
    {
        if (WhyCannotStart(f, plan, crop) != null) return false;
        Decision d = Decide(f, plan, crop, f.Fertility);
        if (!f.Farming) f.Prepare(NextFreeSet());
        f.ConfirmPlan(plan, crop);
        Begin(f, d);
        Save();
        return true;
    }

    /// <summary>Confirms settings for the next crop. The crop already paid for is untouched.</summary>
    public void ApplyNextCrop(FarmField f, FarmPlan plan, CropKind crop)
    {
        if (!f.Farming) return;
        f.ConfirmPlan(plan, crop);
        Save();
    }

    public void SetRepeat(FarmField f, bool repeat)
    {
        if (!f.Farming || f.Repeat == repeat) return;
        f.SetRepeat(repeat);
        Save();
    }

    /// <summary>
    /// Clear field asks to empty the land and release its equipment set (or cancels that).
    /// A crop already growing finishes, and sells if it is cabbage, before the land is cleared.
    /// </summary>
    public void RequestClear(FarmField f, bool clear)
    {
        if (!f.Farming) return;
        if (clear && !f.Running) f.Clear();
        else f.SetPendingClear(clear);
        Save();
    }

    public static bool IsAlarm(PauseReason reason)
    {
        return reason != PauseReason.None && reason != PauseReason.SoilRestored;
    }

    /// <summary>The player-facing sentence for a pause or a refused Start.</summary>
    public string ReasonText(FarmField f, PauseReason reason, Decision d)
    {
        switch (reason)
        {
            case PauseReason.ChooseDensity: return "Choose a planting density above 0%.";
            case PauseReason.Unprofitable:
                return "This cabbage plan would lose money on this soil (net " + Signed(d.forecast.Net) + "). Lower its costs, or restore the soil.";
            case PauseReason.CannotProfit: return "This cabbage plan cannot profit even on rich soil. Lower its operating costs.";
            case PauseReason.NoFunds:
                int others = ReserveOfOthers(f);
                return "Not enough coins: this crop needs " + d.forecast.Cost + (others > 0 ? ", and " + others + " are kept for your other fields." : ".");
            case PauseReason.NoEquipment: return "No free equipment set. An equipment depot brings one more.";
            case PauseReason.SoilRestored: return "Soil restored — choose a cash crop.";
            default: return string.Empty;
        }
    }

    /// <summary>The reason a waiting field is waiting, worked out with today's numbers.</summary>
    public string PauseText(FarmField f)
    {
        if (f.State != FarmField.Status.Paused) return string.Empty;
        Decision d = Decide(f, f.CashPlan, f.ManualCrop, f.Fertility);
        return ReasonText(f, f.Pause, d);
    }

    public static string Signed(int value) { return value >= 0 ? "+" + value : value.ToString(); }

    // ---------- protected operating money ----------

    /// <summary>The next cabbage crop's full cost for a field that holds an equipment set.</summary>
    public int ReserveFor(FarmField f)
    {
        if (f == null || !f.Farming || f.AssignedSet == 0) return 0;
        return Forecast(f, CropKind.Cash, f.CashPlan, f.Fertility).Cost;
    }

    public int ReservedTotal
    {
        get { int n = 0; foreach (FarmField f in fields) n += ReserveFor(f); return n; }
    }

    public int ReserveOfOthers(FarmField f) { return ReservedTotal - ReserveFor(f); }

    /// <summary>Coins kept back from purchases: the fields' reserves, never less than the floor.</summary>
    public int ProtectedTotal { get { return Mathf.Max(rules.MinimumReserve, ReservedTotal); } }

    /// <summary>Coins land, buildings, research and controllers may use.</summary>
    public int Spendable { get { return Mathf.Max(0, Coins - ProtectedTotal); } }

    private string WhyCannotPay(int price)
    {
        if (price <= Spendable) return null;
        return "Needs " + price + " coins to spend. You can spend " + Spendable + " now; " + Mathf.Min(Coins, ProtectedTotal) + " are kept for running your fields.";
    }

    private void Pay(int price) { Coins -= price; }

    // ---------- equipment sets ----------

    public int OwnedSets { get { return rules.StartingSets + (DepotField != null ? rules.DepotSets : 0); } }

    public int SetsInUse
    {
        get { int n = 0; foreach (FarmField f in fields) if (f != null && f.AssignedSet > 0) n++; return n; }
    }

    /// <summary>Sets kept for a field the introduction will still hand over.</summary>
    public int ReservedSets
    {
        get { int n = 0; foreach (FarmField f in fields) if (f != null && f.AwaitsGrant) n++; return n; }
    }

    public int FreeSets { get { return OwnedSets - SetsInUse - ReservedSets; } }

    /// <summary>The number of the set that came with the depot.</summary>
    public int DepotSet { get { return rules.StartingSets + 1; } }

    public FarmField FieldUsingSet(int set)
    {
        foreach (FarmField f in fields) if (f != null && f.AssignedSet == set) return f;
        return null;
    }

    // The lowest free number, so the farm's own sets are used before the depot's.
    private int NextFreeSet()
    {
        for (int set = 1; set <= OwnedSets; set++)
            if (FieldUsingSet(set) == null) return set;
        return 0;
    }

    // ---------- land and buildings ----------
    // Each Why... returns null when the action is allowed, or the reason shown to the player.

    public string WhyCannotBuy(FarmField f)
    {
        if (f.Owned) return "You already own this land.";
        if (!f.ForSale) return "This land is not for sale.";
        return WhyCannotPay(f.PurchasePrice);
    }

    /// <summary>Buys the land. It stays empty: planting is a separate, explicit Start.</summary>
    public bool BuyLand(FarmField f)
    {
        if (WhyCannotBuy(f) != null) return false;
        Pay(f.PurchasePrice);
        f.TakeOwnership();
        Save();
        return true;
    }

    public string WhyCannotBuildDepot(FarmField f)
    {
        if (!f.Owned) return "Buy this land first.";
        if (f.LandUse != FarmField.Use.Empty) return "This land is already in use.";
        if (DepotField != null) return "Your farm already has its depot on " + DepotField.DisplayName + ".";
        return WhyCannotPay(rules.DepotPrice);
    }

    /// <summary>Pays once, puts the building up and adds the set that comes with it.</summary>
    public bool BuildDepot(FarmField f)
    {
        if (WhyCannotBuildDepot(f) != null) return false;
        Pay(rules.DepotPrice);
        f.SetDepot(true);
        DepotField = f;
        if (intro != null) intro.GoalReached = true;
        Save();
        return true;
    }

    public string WhyCannotRemoveDepot()
    {
        if (DepotField == null) return "There is no depot.";
        FarmField user = FieldUsingSet(DepotSet);
        if (user != null)
            return "The depot's equipment set works " + user.DisplayName + ". Use Clear field there to release it; switching Repeat off keeps the set.";
        if (SetsInUse + ReservedSets > rules.StartingSets)
            return "Your fields need more equipment sets than the farm has without the depot.";
        return null;
    }

    /// <summary>Takes the depot down; its set leaves with it. No coins come back.</summary>
    public bool RemoveDepot()
    {
        if (WhyCannotRemoveDepot() != null) return false;
        DepotField.SetDepot(false);
        DepotField = null;
        Save();
        return true;
    }

    /// <summary>The introduction's free field. Granting twice changes nothing; it does not start a crop.</summary>
    public void GrantField(FarmField f)
    {
        if (f.Owned) return;
        f.TakeOwnership();
        if (f.PreparedWhenOwned && f.Arable) f.Prepare(NextFreeSet());
        Save();
    }

    public FarmField FindField(string id)
    {
        foreach (FarmField f in fields)
            if (f != null && f.Id == id) return f;
        return null;
    }

    public int CashCompletedTotal
    {
        get { int n = 0; foreach (FarmField f in fields) if (f != null) n += f.CashCompleted; return n; }
    }

    public int ManualRecoveryTotal
    {
        get { int n = 0; foreach (FarmField f in fields) if (f != null) n += f.ManualRecoveryCompleted; return n; }
    }

    // ---------- research ----------

    public bool Owns(string researchId) { return research.Contains(researchId); }

    public bool ControllersUnlocked
    {
        get
        {
            foreach (FarmRules.ResearchNode n in rules.Research)
                if (n.branch == FarmRules.Branch.SoilControl && Owns(n.id)) return true;
            return false;
        }
    }

    /// <summary>
    /// The cost multipliers of the farm's research. Within a branch the best owned tier counts;
    /// tiers replace each other, they are never multiplied together.
    /// </summary>
    public FarmRules.Discounts CurrentDiscounts
    {
        get
        {
            FarmRules.Discounts d = FarmRules.Discounts.None;
            foreach (FarmRules.ResearchNode n in rules.Research)
            {
                if (!Owns(n.id)) continue;
                if (n.branch == FarmRules.Branch.Sowing) d.seed = Mathf.Min(d.seed, n.costMultiplier);
                if (n.branch == FarmRules.Branch.Fertilizer) d.fertilizer = Mathf.Min(d.fertilizer, n.costMultiplier);
                if (n.branch == FarmRules.Branch.Irrigation) d.water = Mathf.Min(d.water, n.costMultiplier);
            }
            return d;
        }
    }

    public ResearchState StateOf(FarmRules.ResearchNode node, out string requirement)
    {
        requirement = null;
        if (Owns(node.id)) return ResearchState.Owned;
        if (!string.IsNullOrEmpty(node.prerequisite) && !Owns(node.prerequisite))
        {
            FarmRules.ResearchNode before = rules.FindResearch(node.prerequisite);
            requirement = "Needs " + (before != null ? before.title : node.prerequisite) + ".";
            return ResearchState.Locked;
        }
        if (CashCompletedTotal < 1) { requirement = "Opens after your first cabbage harvest."; return ResearchState.Locked; }
        if (node.requirement == FarmRules.Requirement.FirstCashCropAndManualRestoration && ManualRecoveryTotal < 1)
        {
            requirement = "Opens after you restore a field's soil by hand once.";
            return ResearchState.Locked;
        }
        return ResearchState.Available;
    }

    public string WhyCannotResearch(string id)
    {
        FarmRules.ResearchNode node = rules.FindResearch(id);
        if (node == null) return "Unknown research.";
        string requirement;
        ResearchState state = StateOf(node, out requirement);
        if (state == ResearchState.Owned) return "Already researched.";
        if (state == ResearchState.Locked) return requirement;
        return WhyCannotPay(node.price);
    }

    /// <summary>Buys a research node once. Soil control also brings one free controller kit, once.</summary>
    public bool BuyResearch(string id)
    {
        if (WhyCannotResearch(id) != null) return false;
        FarmRules.ResearchNode node = rules.FindResearch(id);
        Pay(node.price);
        research.Add(id);
        if (node.branch == FarmRules.Branch.SoilControl && !CreditGranted)
        {
            CreditGranted = true;
            InstallCredits++;
        }
        Save();
        return true;
    }

    // ---------- soil controllers ----------

    public string WhyCannotInstall(FarmField f)
    {
        if (!ControllersUnlocked) return "Research Soil control first.";
        if (!f.Owned) return "Buy this land first.";
        if (!f.Arable) return "Only farmland can take a soil controller.";
        if (f.ControllerInstalled) return "A soil controller is already installed here.";
        if (InstallCredits > 0) return null;
        return WhyCannotPay(rules.ControllerInstallPrice);
    }

    /// <summary>Uses the free kit if there is one, otherwise pays. It works from the next crop.</summary>
    public bool InstallController(FarmField f)
    {
        if (WhyCannotInstall(f) != null) return false;
        if (InstallCredits > 0) InstallCredits--;
        else Pay(rules.ControllerInstallPrice);
        f.InstallController();
        Save();
        return true;
    }

    public void SetControllerEnabled(FarmField f, bool on)
    {
        if (!f.ControllerInstalled || f.ControllerEnabled == on) return;
        f.SetControllerEnabled(on);
        Save();
    }

    // ---------- saving ----------

    public void Save()
    {
        if (resetting || LoadProblem != null || string.IsNullOrEmpty(saveFileName) || fields == null || rules == null) return;
        sinceSave = 0f;

        FarmSaveV3 data = new FarmSaveV3();
        data.version = FarmSaveFile.Version;
        data.coins = Coins;
        data.introStep = intro != null ? (int)intro.Current : 0;
        data.goalReached = intro != null && intro.GoalReached;
        data.research = new List<string>(research).ToArray();
        data.installCredits = InstallCredits;
        data.creditGranted = CreditGranted;
        data.automaticReturns = AutomaticReturns;
        data.fields = new FieldSaveV3[fields.Length];
        for (int i = 0; i < fields.Length; i++) data.fields[i] = fields[i].Capture();
        FarmSaveFile.Write(saveFileName, data);
    }

    private void Load()
    {
        FarmSaveV3 current;
        LegacySave legacy;
        string problem;
        FarmSaveFile.Outcome outcome = FarmSaveFile.TryLoad(saveFileName, out current, out legacy, out problem);
        if (outcome == FarmSaveFile.Outcome.Missing) return;      // a new farm
        if (outcome == FarmSaveFile.Outcome.Invalid) { LoadProblem = problem; return; }

        if (outcome == FarmSaveFile.Outcome.Legacy)
        {
            FarmSaveFile.KeepMigrationBackup(saveFileName, legacy.version);
            problem = ValidateLegacy(legacy);
            if (problem != null) { LoadProblem = problem; return; }
            ApplyLegacy(legacy);
            return;
        }

        problem = Validate(current);
        if (problem != null) { LoadProblem = problem; return; }
        Coins = current.coins;
        research.Clear();
        if (current.research != null) foreach (string id in current.research) research.Add(id);
        InstallCredits = current.installCredits;
        CreditGranted = current.creditGranted;
        AutomaticReturns = current.automaticReturns;
        foreach (FieldSaveV3 d in current.fields) FindField(d.id).Restore(d);
        if (intro != null) intro.Restore(current.introStep, current.goalReached);
    }

    // Checks that need the scene: every id known and unique, every number in range.
    private string Validate(FarmSaveV3 s)
    {
        const string bad = "The save file contains values this game cannot use.";
        if (s.coins < 0 || s.installCredits < 0 || s.automaticReturns < 0) return bad;
        if (intro != null && !intro.IsValidStep(s.introStep)) return bad;
        if (s.research != null)
            foreach (string id in s.research) if (rules.FindResearch(id) == null) return bad;
        HashSet<string> seen = new HashSet<string>();
        HashSet<int> sets = new HashSet<int>();
        int maxSet = rules.StartingSets + rules.DepotSets;
        foreach (FieldSaveV3 d in s.fields)
        {
            if (d == null || FindField(d.id) == null || !seen.Add(d.id)) return bad;
            if (d.use < 0 || d.use > 2 || d.status < 0 || d.status > 2) return bad;
            if (d.pause < 0 || d.pause > (int)PauseReason.SoilRestored || d.manualCrop < 0 || d.manualCrop > 1) return bad;
            if (d.fertility < 0 || d.fertility > 100 || !d.cashPlan.IsValid) return bad;
            if (d.assignedSet < 0 || d.assignedSet > maxSet) return bad;
            if (d.assignedSet > 0 && !sets.Add(d.assignedSet)) return bad;
            if (d.hasActive && (d.active == null || d.active.CycleSeconds <= 0f || d.elapsed < 0f || d.elapsed > d.active.CycleSeconds + 1f)) return bad;
        }
        return null;
    }

    private string ValidateLegacy(LegacySave s)
    {
        const string bad = "The save file contains values this game cannot use.";
        if (s.coins < 0) return bad;
        HashSet<string> seen = new HashSet<string>();
        HashSet<int> sets = new HashSet<int>();
        int maxSet = rules.StartingSets + rules.DepotSets;
        foreach (LegacyField d in s.fields)
        {
            if (d == null || FindField(d.id) == null || !seen.Add(d.id)) return bad;
            if (!d.plan.IsValid || !d.activePlan.IsValid) return bad;
            if (d.use < 0 || d.use > 2 || d.assignedSet < 0 || d.assignedSet > maxSet) return bad;
            if (d.assignedSet > 0 && !sets.Add(d.assignedSet)) return bad;
            if (d.cycleTime < 0f || d.cycleTime > rules.CycleSeconds + 1f) return bad;
        }
        return null;
    }

    /// <summary>
    /// Upgrades a version 1 or 2 save. Its wallet, land, depot, sets, pending stops, receipts and
    /// sale counts carry over exactly. Soil starts at each field's authored value. Fields that
    /// were farming keep repeating. A crop already paid under the old rules finishes once with
    /// the old numbers: no new charge, no discount, no soil change.
    /// </summary>
    private void ApplyLegacy(LegacySave s)
    {
        Coins = s.coins;
        foreach (LegacyField old in s.fields)
        {
            FarmField f = FindField(old.id);
            int use = s.version >= 2 ? old.use : (old.owned && old.farming ? 1 : 0);
            bool farming = old.owned && use == 1;

            FieldSaveV3 d = new FieldSaveV3();
            d.id = old.id;
            d.owned = old.owned;
            d.use = use;
            d.assignedSet = s.version >= 2 ? old.assignedSet : 0;
            d.fertility = f.StartingFertility;
            d.cashPlan = old.plan.ToPercent();
            d.manualCrop = (int)CropKind.Cash;
            d.repeat = farming;
            d.pendingClear = farming && old.pendingStop;
            d.controllerEnabled = false;
            d.status = farming && old.running ? (int)FarmField.Status.Running : (int)FarmField.Status.Ready;

            if (farming && old.running)
            {
                CropForecast paid = rules.Forecast(CropKind.Cash, old.activePlan.ToPercent(), f.StartingFertility, f.Moisture, FarmRules.Discounts.None);
                paid.legacy = true;
                paid.endFertility = paid.startFertility;
                d.hasActive = true;
                d.active = paid;
                d.elapsed = old.cycleTime;
            }
            else d.active = new CropForecast();

            d.hasLast = old.hasResult;
            CropForecast receipt = new CropForecast();
            receipt.kind = CropKind.Cash;
            receipt.legacy = true;
            receipt.income = old.lastResult.income;
            receipt.seedCost = old.lastResult.seedCost;
            receipt.fertilizerCost = old.lastResult.fertilizerCost;
            receipt.waterCost = old.lastResult.waterCost;
            receipt.startFertility = receipt.endFertility = f.StartingFertility;
            receipt.moisture = f.Moisture;
            d.last = receipt;
            d.cashCompleted = old.cropsSold;
            f.Restore(d);
        }
        if (intro != null) intro.RestoreFromLegacy(s.introStep, s.goalReached);
    }

    /// <summary>
    /// The recovery action offered when a save cannot be read: keep that file under another
    /// name, then reload the map so everything starts from its authored defaults.
    /// </summary>
    public void StartNewFarm()
    {
        resetting = true;
        FarmSaveFile.SetAside(saveFileName);
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
    }

    [ContextMenu("Delete Save File")]
    private void DeleteSaveFile()
    {
        string path = FarmSaveFile.PathFor(saveFileName);
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        Debug.Log("Deleted " + path);
    }
}
