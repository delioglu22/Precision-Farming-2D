using UnityEngine;

/// <summary>What a field grows: cabbage to sell, or green beans that rebuild the soil.</summary>
public enum CropKind { Cash, Recovery }

/// <summary>
/// The player's cabbage settings, one per machine, each a percentage from 0 to 100 in steps of
/// five: seeder planting density, drone fertilizer dose, irrigation watering dose. A plain value:
/// copying it takes a snapshot.
/// </summary>
[System.Serializable]
public struct FarmPlan
{
    public const int Step = 5;

    public int density;
    public int fertilizer;
    public int watering;

    public FarmPlan(int density, int fertilizer, int watering)
    {
        this.density = Snap(density);
        this.fertilizer = Snap(fertilizer);
        this.watering = Snap(watering);
    }

    public bool Same(FarmPlan other)
    {
        return density == other.density && fertilizer == other.fertilizer && watering == other.watering;
    }

    /// <summary>Rounds any percentage to the nearest allowed step, kept within 0..100.</summary>
    public static int Snap(float percent)
    {
        int snapped = Mathf.RoundToInt(percent / Step) * Step;
        return Mathf.Clamp(snapped, 0, 100);
    }

    public bool IsValid
    {
        get { return Valid(density) && Valid(fertilizer) && Valid(watering); }
    }

    private static bool Valid(int v) { return v >= 0 && v <= 100 && v % Step == 0; }
}

/// <summary>
/// One crop worked out in full: what it costs, what it earns and what it does to the soil.
/// The same numbers serve three purposes, which is why they are one type:
///   - a forecast, shown before Start and never charged;
///   - the snapshot of a paid crop, frozen at Start and settled exactly as frozen;
///   - the receipt of the last completed crop, kept as history.
/// Fertility is in whole percentage points so threshold checks are exact.
/// </summary>
[System.Serializable]
public class CropForecast
{
    public CropKind kind;
    public FarmPlan plan;

    public int startFertility;
    public int endFertility;
    public int moisture;

    public int seedCost;
    public int fertilizerCost;
    public int waterCost;
    public int income;
    [Tooltip("What this density would earn with fully supported soil; used to draw how full the crop grows.")]
    public int fullIncome;

    public float sowSeconds;
    public float growSeconds;
    public float finishSeconds;

    /// <summary>Started by a soil controller rather than by the player's own choice.</summary>
    public bool automatic;
    /// <summary>A crop paid under the old rules before an update: it settles as it was and leaves the soil alone.</summary>
    public bool legacy;

    public int Cost { get { return seedCost + fertilizerCost + waterCost; } }
    public int Net { get { return income - Cost; } }
    public int FertilityChange { get { return endFertility - startFertility; } }
    public float CycleSeconds { get { return sowSeconds + growSeconds + finishSeconds; } }

    public CropForecast Copy()
    {
        return (CropForecast)MemberwiseClone();
    }
}
