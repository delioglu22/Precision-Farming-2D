using UnityEngine;

/// <summary>
/// Every tuneable number of the farming loop in one asset: money, timing, how each machine
/// setting costs and helps, the restoration recipe, the soil controller's thresholds and the
/// research nodes. It is read, never written, while the game runs - progress lives in
/// <see cref="Farm"/> and <see cref="FarmField"/>, so Play Mode cannot change it.
///
/// <see cref="Forecast"/> is the only place a crop's outcome is worked out. The preview before
/// Start, the snapshot frozen at Start and the settlement at the end all use its numbers.
///
/// A cabbage harvest is the density's potential scaled by two satisfactions, water and
/// fertility: each is (what the land has + what the machine adds) / what the density demands,
/// capped at 1. Settings between the anchors are interpolated in a straight line.
/// </summary>
[CreateAssetMenu(fileName = "FarmRules", menuName = "Precision Farming/Farm Rules")]
public class FarmRules : ScriptableObject
{
    [System.Serializable]
    public class DensityAnchor
    {
        [Range(0, 100)] public int percent;
        public float seedCost;
        public float potentialIncome;
        [Tooltip("Water the crop asks for, on the 0-1 scale of moisture.")]
        public float waterDemand;
        [Tooltip("Fertility the crop asks for, on the 0-1 scale of fertility.")]
        public float fertilityDemand;
        [Tooltip("Fertility points one finished crop takes out of the soil.")]
        public float fertilityUsed;
    }

    [System.Serializable]
    public class CareAnchor
    {
        [Range(0, 100)] public int percent;
        public float cost;
        [Tooltip("Added to the land's own moisture or fertility while this crop grows (0-1 scale).")]
        public float support;
    }

    public enum Branch { Sowing, Fertilizer, Irrigation, SoilControl }
    public enum Requirement { FirstCashCrop, FirstCashCropAndManualRestoration }

    [System.Serializable]
    public class ResearchNode
    {
        [Tooltip("Stable id used in saves. Do not rename once saves exist.")]
        public string id;
        public string title;
        [TextArea] public string effect;
        public Branch branch;
        [Tooltip("Id of the node that must be owned first, or empty.")]
        public string prerequisite;
        public Requirement requirement;
        public int price;
        [Tooltip("Cost multiplier for this branch once owned. A later tier replaces it, it is not multiplied in.")]
        public float costMultiplier = 1f;
    }

    [Header("Money")]
    [SerializeField] private int startingCoins = 120;
    [SerializeField] private int coinsPerUnit = 1;
    [Tooltip("Coins always kept back from discretionary purchases, so the farm can restart a crop.")]
    [SerializeField] private int minimumReserve = 2;

    [Header("Land and equipment")]
    [Tooltip("Equipment sets (seeder + drone + irrigation) a new farm owns and can store.")]
    [SerializeField] private int startingSets = 2;
    [SerializeField] private int depotPrice = 120;
    [Tooltip("Sets the depot adds: it comes with them and stores them.")]
    [SerializeField] private int depotSets = 1;

    [Header("Cycle, in seconds")]
    [SerializeField] private float sowSeconds = 8f;
    [SerializeField] private float growSeconds = 40f;
    [Tooltip("Harvest for cabbage, ploughing the beans in for restoration.")]
    [SerializeField] private float finishSeconds = 12f;

    [Header("Cabbage: seeder density anchors (0, 25, 50, 100 %)")]
    [SerializeField] private DensityAnchor[] densities = new DensityAnchor[0];

    [Header("Cabbage: drone fertilizer anchors (0, 50, 100 %)")]
    [SerializeField] private CareAnchor[] fertilizers = new CareAnchor[0];

    [Header("Cabbage: irrigation watering anchors (0, 50, 100 %)")]
    [SerializeField] private CareAnchor[] waterings = new CareAnchor[0];

    [Header("Green-bean restoration (fixed recipe)")]
    [SerializeField] private int restorationCost = 0;
    [Tooltip("Fertility points one restoration crop puts back, up to 100.")]
    [SerializeField] private int restorationGain = 25;

    [Header("Soil controller")]
    [Tooltip("Cash mode switches to restoration when fertility is below this.")]
    [SerializeField] private int restoreBelow = 30;
    [Tooltip("Restoration may hand back to cabbage only when fertility is above this.")]
    [SerializeField] private int returnAbove = 70;
    [Tooltip("Coins for each installation after the free kit.")]
    [SerializeField] private int controllerInstallPrice = 40;

    [Header("Research")]
    [SerializeField] private ResearchNode[] research = new ResearchNode[0];

    public int StartingCoins { get { return startingCoins; } }
    public int MinimumReserve { get { return minimumReserve; } }
    public int StartingSets { get { return startingSets; } }
    public int DepotPrice { get { return depotPrice; } }
    public int DepotSets { get { return depotSets; } }
    public float CycleSeconds { get { return sowSeconds + growSeconds + finishSeconds; } }
    public int RestorationGain { get { return restorationGain; } }
    public int RestoreBelow { get { return restoreBelow; } }
    public int ReturnAbove { get { return returnAbove; } }
    public int ControllerInstallPrice { get { return controllerInstallPrice; } }
    public ResearchNode[] Research { get { return research; } }

    /// <summary>Cost multipliers the farm's research gives each machine. 1 means no discount.</summary>
    public struct Discounts
    {
        public float seed;
        public float fertilizer;
        public float water;
        public static Discounts None { get { Discounts d; d.seed = 1f; d.fertilizer = 1f; d.water = 1f; return d; } }
    }

    public ResearchNode FindResearch(string id)
    {
        foreach (ResearchNode n in research) if (n.id == id) return n;
        return null;
    }

    /// <summary>
    /// Works out one crop. Fertility and moisture are whole percentage points at the crop's start.
    /// Nothing here changes any state: calling it as often as a slider moves is free.
    /// </summary>
    public CropForecast Forecast(CropKind kind, FarmPlan plan, int fertility, int moisture, Discounts discounts)
    {
        CropForecast f = new CropForecast();
        f.kind = kind;
        f.plan = plan;
        f.startFertility = fertility;
        f.moisture = moisture;
        f.sowSeconds = sowSeconds;
        f.growSeconds = growSeconds;
        f.finishSeconds = finishSeconds;

        if (kind == CropKind.Recovery)
        {
            f.seedCost = restorationCost;
            f.endFertility = Mathf.Min(100, fertility + restorationGain);
            return f;
        }

        DensityAnchor d = Interpolate(densities, plan.density);
        CareAnchor feed = Interpolate(fertilizers, plan.fertilizer);
        CareAnchor water = Interpolate(waterings, plan.watering);

        f.seedCost = Price(d.seedCost, discounts.seed);
        f.fertilizerCost = Price(feed.cost, discounts.fertilizer);
        f.waterCost = Price(water.cost, discounts.water);

        if (plan.density > 0)
        {
            double waterSat = Satisfaction(moisture / 100.0 + water.support, d.waterDemand);
            double feedSat = Satisfaction(fertility / 100.0 + feed.support, d.fertilityDemand);
            f.income = RoundHalfUp(d.potentialIncome * waterSat * feedSat) * coinsPerUnit;
            f.fullIncome = RoundHalfUp(d.potentialIncome) * coinsPerUnit;
        }
        f.endFertility = Mathf.Max(0, fertility - RoundHalfUp(d.fertilityUsed));
        return f;
    }

    // Each cost line is rounded up separately. The small tolerance keeps 0.85 * 20 = 17.0000001
    // from becoming 18 because of how floats store decimals.
    private static int Price(float baseCost, float multiplier)
    {
        return (int)System.Math.Ceiling(baseCost * (double)multiplier - 1e-6);
    }

    private static int RoundHalfUp(double value)
    {
        return (int)System.Math.Floor(value + 0.5 + 1e-6);
    }

    private static double Satisfaction(double available, double demand)
    {
        if (demand <= 0) return 1;
        return System.Math.Min(1.0, available / demand);
    }

    // Straight-line blend between the two anchors around the setting.
    private static DensityAnchor Interpolate(DensityAnchor[] anchors, int percent)
    {
        DensityAnchor below = anchors[0], above = anchors[anchors.Length - 1];
        for (int i = 0; i < anchors.Length - 1; i++)
            if (percent >= anchors[i].percent && percent <= anchors[i + 1].percent) { below = anchors[i]; above = anchors[i + 1]; break; }
        float t = above.percent == below.percent ? 0f : (percent - below.percent) / (float)(above.percent - below.percent);
        DensityAnchor r = new DensityAnchor();
        r.percent = percent;
        r.seedCost = Mathf.Lerp(below.seedCost, above.seedCost, t);
        r.potentialIncome = Mathf.Lerp(below.potentialIncome, above.potentialIncome, t);
        r.waterDemand = Mathf.Lerp(below.waterDemand, above.waterDemand, t);
        r.fertilityDemand = Mathf.Lerp(below.fertilityDemand, above.fertilityDemand, t);
        r.fertilityUsed = Mathf.Lerp(below.fertilityUsed, above.fertilityUsed, t);
        return r;
    }

    private static CareAnchor Interpolate(CareAnchor[] anchors, int percent)
    {
        CareAnchor below = anchors[0], above = anchors[anchors.Length - 1];
        for (int i = 0; i < anchors.Length - 1; i++)
            if (percent >= anchors[i].percent && percent <= anchors[i + 1].percent) { below = anchors[i]; above = anchors[i + 1]; break; }
        float t = above.percent == below.percent ? 0f : (percent - below.percent) / (float)(above.percent - below.percent);
        CareAnchor r = new CareAnchor();
        r.percent = percent;
        r.cost = Mathf.Lerp(below.cost, above.cost, t);
        r.support = Mathf.Lerp(below.support, above.support, t);
        return r;
    }
}
