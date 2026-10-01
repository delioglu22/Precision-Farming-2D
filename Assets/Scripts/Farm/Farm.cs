using UnityEngine;

/// <summary>
/// The farm as a business: one wallet, the equipment sets, the land decisions and the clock
/// that moves every field's crop along. Unity has no production or accounting system to lean
/// on, so this is plain game logic.
///
/// Money moves in only a few places, all in this class or in <see cref="FarmField.Tick"/>:
/// crop costs when sowing starts, income when a crop sells, and the one-off prices of land
/// and the depot. Each purchase re-checks its own conditions before paying, so a second tap
/// on a button finds the land already owned or the depot already built and does nothing.
///
/// An equipment set is one seeder, one drone and one irrigation unit, and works one field.
/// Sets are numbered: 1..StartingSets come with the farm, the next one comes with the depot.
/// </summary>
[DisallowMultipleComponent]
public class Farm : MonoBehaviour
{
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
    [Tooltip("Seconds between saves of the crops' progress. Money changes save at once.")]
    [SerializeField] private float autosaveSeconds = 5f;

    public FarmRules Rules { get { return rules; } }
    public FarmIntro Intro { get { return intro; } }
    public FarmField[] Fields { get { return fields; } }
    public int Coins { get; private set; }

    /// <summary>The parcel holding the equipment depot, or null.</summary>
    public FarmField DepotField { get; private set; }

    /// <summary>
    /// Set when a save file exists but cannot be used. The farm then stands still and never
    /// saves, so the file is not overwritten, until the player chooses to start a new farm.
    /// </summary>
    public string LoadProblem { get; private set; }

    private float sinceSave;
    private bool resetting;

    private void Awake()
    {
        Coins = rules != null ? rules.StartingCoins : 0;
    }

    // Start, not Awake: every field has set up its own defaults in Awake by now, and the
    // saved state is laid over those defaults. Start still runs before any Update.
    private void Start()
    {
        Load();
        foreach (FarmField f in fields)
            if (f != null && f.LandUse == FarmField.Use.Depot) DepotField = f;
        // A fresh farm (or an older save) has fields farming without a set yet.
        foreach (FarmField f in fields)
            if (f != null && f.Farming && f.AssignedSet == 0) f.AssignSet(NextFreeSet());
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
            if (f != null && f.Tick(this, Time.deltaTime)) changed = true;

        sinceSave += Time.deltaTime;
        if (changed || sinceSave >= autosaveSeconds) Save();
    }

    // ---------- wallet ----------

    /// <summary>Takes the money if there is enough, and reports whether it did.</summary>
    public bool TrySpend(int amount)
    {
        if (amount > Coins) return false;
        Coins -= amount;
        return true;
    }

    public void Earn(int amount)
    {
        Coins += amount;
    }

    // ---------- equipment sets ----------

    public int OwnedSets { get { return rules.StartingSets + (DepotField != null ? rules.DepotSets : 0); } }

    public int SetsInUse
    {
        get
        {
            int n = 0;
            foreach (FarmField f in fields) if (f != null && f.AssignedSet > 0) n++;
            return n;
        }
    }

    /// <summary>Sets kept for a field the introduction will still hand over.</summary>
    public int ReservedSets
    {
        get
        {
            int n = 0;
            foreach (FarmField f in fields) if (f != null && f.AwaitsGrant) n++;
            return n;
        }
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

    // ---------- land decisions ----------
    // Each Why... returns null when the action is allowed, or the reason shown to the player.

    public string WhyCannotBuy(FarmField f)
    {
        if (f.Owned) return "You already own this land.";
        if (!f.ForSale) return "This land is not for sale.";
        if (Coins < f.PurchasePrice) return "Needs " + f.PurchasePrice + " coins.";
        return null;
    }

    public bool BuyLand(FarmField f)
    {
        if (WhyCannotBuy(f) != null) return false;
        TrySpend(f.PurchasePrice);
        f.TakeOwnership(false);
        Save();
        return true;
    }

    public bool CanEverPlant(FarmField f) { return f.Fertility >= rules.PlantingThreshold; }

    public string WhyCannotPlant(FarmField f)
    {
        if (!f.Owned) return "Buy this land first.";
        if (f.LandUse != FarmField.Use.Empty) return "This land is already in use.";
        if (!CanEverPlant(f)) return "The soil is too poor to farm. You can build here instead.";
        if (FreeSets <= 0) return "No free equipment set. An equipment depot brings one more.";
        int cost = rules.Costs(f.StarterPlan).Cost;
        if (Coins < cost) return "The first crop needs " + cost + " coins.";
        return null;
    }

    /// <summary>Starts farming with the starter plan. The first crop is paid when sowing starts.</summary>
    public bool Plant(FarmField f)
    {
        if (WhyCannotPlant(f) != null) return false;
        f.StartFarming(NextFreeSet());
        Save();
        return true;
    }

    public string WhyCannotBuildDepot(FarmField f)
    {
        if (!f.Owned) return "Buy this land first.";
        if (f.LandUse != FarmField.Use.Empty) return "This land is already in use.";
        if (DepotField != null) return "Your farm already has its depot on " + DepotField.DisplayName + ".";
        if (Coins < rules.DepotPrice) return "Needs " + rules.DepotPrice + " coins.";
        return null;
    }

    /// <summary>Pays once, puts the building up and adds the set that comes with it.</summary>
    public bool BuildDepot(FarmField f)
    {
        if (WhyCannotBuildDepot(f) != null) return false;
        TrySpend(rules.DepotPrice);
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
            return "The depot's equipment set is working " + user.DisplayName + ". Stop farming there first, and let its crop finish.";
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

    /// <summary>
    /// Asks a field to stop farming, or cancels that request. A crop that is already paid for
    /// still grows and sells once; the land is cleared and its set freed after that.
    /// </summary>
    public void RequestStop(FarmField f, bool stop)
    {
        if (!f.Farming) return;
        f.RequestStop(stop);
        Save();
    }

    /// <summary>The introduction's free field. Granting twice changes nothing.</summary>
    public void GrantField(FarmField f)
    {
        if (f.Owned) return;
        f.TakeOwnership(false);
        if (f.FarmsWhenOwned) f.StartFarming(NextFreeSet());
        Save();
    }

    public FarmField FindField(string id)
    {
        foreach (FarmField f in fields)
            if (f != null && f.Id == id) return f;
        return null;
    }

    // ---------- saving ----------

    public void Save()
    {
        if (resetting || LoadProblem != null || string.IsNullOrEmpty(saveFileName) || fields == null) return;
        sinceSave = 0f;

        FarmSaveData data = new FarmSaveData();
        data.version = FarmSaveFile.Version;
        data.coins = Coins;
        data.introStep = intro != null ? (int)intro.Current : 0;
        data.goalReached = intro != null && intro.GoalReached;
        data.fields = new FieldSaveData[fields.Length];
        for (int i = 0; i < fields.Length; i++) data.fields[i] = fields[i].Capture();
        FarmSaveFile.Write(saveFileName, data);
    }

    private void Load()
    {
        FarmSaveData data;
        string problem;
        FarmSaveFile.Outcome outcome = FarmSaveFile.TryLoad(saveFileName, out data, out problem);
        if (outcome == FarmSaveFile.Outcome.Missing) return;      // a new farm
        if (outcome == FarmSaveFile.Outcome.Invalid) { LoadProblem = problem; return; }

        Coins = data.coins;
        if (intro != null) intro.Restore((FarmIntro.Step)data.introStep, data.goalReached);
        foreach (FieldSaveData d in data.fields)
        {
            FarmField f = FindField(d.id);
            if (f != null) f.Restore(d);
        }
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
