using UnityEngine;

/// <summary>
/// Every tuneable number of the farming loop in one asset: prices, cycle timing and what each
/// machine setting costs and adds. It is read, never written, while the game runs - progress
/// lives in <see cref="Farm"/> and <see cref="FarmField"/>, so Play Mode cannot change it.
///
/// A harvest is the density's potential crop scaled by two satisfactions, water and fertility:
/// each is (what the land has + what the machine adds) / what the density demands, capped at 1.
/// Raising a setting past the point of satisfaction only adds cost.
/// </summary>
[CreateAssetMenu(fileName = "FarmRules", menuName = "Precision Farming/Farm Rules")]
public class FarmRules : ScriptableObject
{
    [System.Serializable]
    public class DensityLevel
    {
        public int seedCost;
        public int potentialHarvest;
        [Tooltip("How much water this density asks of the land, on the same 0-1 scale as moisture.")]
        public float waterDemand;
        [Tooltip("How much fertility this density asks of the land, on the same 0-1 scale as fertility.")]
        public float fertilityDemand;
    }

    [System.Serializable]
    public class CareLevel
    {
        public int cost;
        [Tooltip("Added to the land's own moisture or fertility.")]
        public float support;
    }

    [Header("Money")]
    [SerializeField] private int startingCoins = 120;
    [SerializeField] private int coinsPerUnit = 1;

    [Header("Land and equipment")]
    [Tooltip("Land below this fertility can be built on but not planted.")]
    [SerializeField, Range(0f, 1f)] private float plantingThreshold = 0.5f;
    [Tooltip("Equipment sets (seeder + drone + irrigation) a new farm owns and can store.")]
    [SerializeField] private int startingSets = 2;
    [SerializeField] private int depotPrice = 120;
    [Tooltip("Sets the depot adds: it comes with them and stores them.")]
    [SerializeField] private int depotSets = 1;

    [Header("Cycle, in seconds")]
    [SerializeField] private float sowSeconds = 8f;
    [SerializeField] private float growSeconds = 40f;
    [SerializeField] private float harvestSeconds = 12f;

    [Header("Seeder: Low, Standard, High")]
    [SerializeField] private DensityLevel[] densities = new DensityLevel[3];

    [Header("Drone: Off, Moderate, High")]
    [SerializeField] private CareLevel[] fertilizers = new CareLevel[3];

    [Header("Irrigation: Off, Moderate, High")]
    [SerializeField] private CareLevel[] waterings = new CareLevel[3];

    public int StartingCoins { get { return startingCoins; } }
    public float PlantingThreshold { get { return plantingThreshold; } }
    public int StartingSets { get { return startingSets; } }
    public int DepotPrice { get { return depotPrice; } }
    public int DepotSets { get { return depotSets; } }
    public float SowSeconds { get { return sowSeconds; } }
    public float GrowSeconds { get { return growSeconds; } }
    public float HarvestSeconds { get { return harvestSeconds; } }
    public float CycleSeconds { get { return sowSeconds + growSeconds + harvestSeconds; } }

    /// <summary>What sowing this plan costs, before anything has grown.</summary>
    public HarvestResult Costs(FarmPlan plan)
    {
        HarvestResult r = new HarvestResult();
        r.seedCost = densities[(int)plan.density].seedCost;
        r.fertilizerCost = fertilizers[(int)plan.fertilizer].cost;
        r.waterCost = waterings[(int)plan.watering].cost;
        return r;
    }

    /// <summary>The full result of growing this plan on land with this fertility and moisture.</summary>
    public HarvestResult Evaluate(FarmPlan plan, float fertility, float moisture)
    {
        DensityLevel d = densities[(int)plan.density];
        double water = Satisfaction(moisture + waterings[(int)plan.watering].support, d.waterDemand);
        double feed = Satisfaction(fertility + fertilizers[(int)plan.fertilizer].support, d.fertilityDemand);

        // Nearest whole unit, a half rounding up. The tiny epsilon keeps 0.25 + 0.30 (which a
        // float stores as a hair under 0.55) from turning a full harvest into one unit less.
        int units = (int)System.Math.Floor(d.potentialHarvest * water * feed + 0.5 + 1e-6);

        HarvestResult r = Costs(plan);
        r.income = units * coinsPerUnit;
        return r;
    }

    private static double Satisfaction(double available, double demand)
    {
        if (demand <= 0) return 1;
        return System.Math.Min(1.0, available / demand);
    }
}
