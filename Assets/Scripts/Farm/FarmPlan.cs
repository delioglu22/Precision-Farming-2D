/// <summary>How thickly the seeder sows. More seed can grow more crop, and asks more of the land.</summary>
public enum Density { Low, Standard, High }

/// <summary>How much the drone (fertilizer) or the irrigation (water) adds on top of the land.</summary>
public enum Care { Off, Moderate, High }

/// <summary>
/// The three settings a farmed parcel runs with, one per machine. A plain value: copying it
/// takes a snapshot, which is how a running crop keeps the plan it was paid for.
/// </summary>
[System.Serializable]
public struct FarmPlan
{
    public Density density;
    public Care fertilizer;
    public Care watering;

    public FarmPlan(Density density, Care fertilizer, Care watering)
    {
        this.density = density;
        this.fertilizer = fertilizer;
        this.watering = watering;
    }

    public bool Same(FarmPlan other)
    {
        return density == other.density && fertilizer == other.fertilizer && watering == other.watering;
    }
}

/// <summary>
/// What one finished crop paid and earned. Costs are charged when the crop is sown;
/// income arrives when it is sold. Net is what the player actually kept.
/// </summary>
[System.Serializable]
public struct HarvestResult
{
    public int income;
    public int seedCost;
    public int fertilizerCost;
    public int waterCost;

    public int Cost { get { return seedCost + fertilizerCost + waterCost; } }
    public int Net { get { return income - Cost; } }
}
