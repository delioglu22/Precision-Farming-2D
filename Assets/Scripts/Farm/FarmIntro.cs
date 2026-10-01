using UnityEngine;

/// <summary>
/// The first minutes of a new farm: one short hint at a time, advanced by what the player
/// actually does rather than by a "Next" button.
///
///   1. Tap the first field.          2. Give it some watering.
///   3. Watch it sell a crop  ->  the second field is granted, free, once.
///   4. Tap the second field.         5. Give it some fertilizer.
///   6. Watch it sell a fertilized crop  ->  planting density unlocks; the depot is the next goal.
///
/// Each frame the current step's condition is checked, and the flow moves on as far as the
/// farm already allows, so doing things early or out of order can never get it stuck.
/// Unity has no tutorial system; this is a small state machine over the farm's own state.
/// </summary>
[DisallowMultipleComponent]
public class FarmIntro : MonoBehaviour
{
    public enum Step { TapFirst, TryWatering, WaitFirstHarvest, TapSecond, TryFertilizer, WaitSecondHarvest, Done }

    [Header("Fields")]
    [Tooltip("The field the player starts with (fertile, dry).")]
    [SerializeField] private FarmField first;
    [Tooltip("The field granted after the first lesson (moist, less fertile).")]
    [SerializeField] private FarmField second;

    [Header("After the introduction")]
    [Tooltip("Where the equipment depot can go (poor land, Build only).")]
    [SerializeField] private FarmField depotSite;
    [Tooltip("The field to buy and plant once the depot brings a third equipment set.")]
    [SerializeField] private FarmField third;

    [Header("Selection")]
    [SerializeField] private ParcelSelectionChannel selection;
    [SerializeField] private Farm farm;

    private Parcel picked;

    public Step Current { get; private set; }

    /// <summary>Set by whoever reaches the goal (the depot purchase) to retire the goal hint.</summary>
    public bool GoalReached { get; set; }

    /// <summary>The field the current hint asks the player to tap, or null.</summary>
    public FarmField Target
    {
        get
        {
            if (Current == Step.TapFirst) return first;
            if (Current == Step.TapSecond) return second;
            if (Current != Step.Done) return null;
            if (!GoalReached) return depotSite;
            if (third != null && !third.Farming) return third;
            return null;
        }
    }

    public bool FertilizerUnlocked { get { return Current >= Step.TapSecond; } }
    public bool DensityUnlocked { get { return Current >= Step.Done; } }

    private void OnEnable() { if (selection != null) selection.Selected += OnSelected; }
    private void OnDisable() { if (selection != null) selection.Selected -= OnSelected; }

    private void OnSelected(Parcel parcel) { picked = parcel; }

    /// <summary>Restores a saved step. The second field's grant is restored by its own save data.</summary>
    public void Restore(Step step, bool goalReached)
    {
        Current = step;
        GoalReached = goalReached;
    }

    private void Update()
    {
        if (first == null || second == null) return;

        // Move forward as many steps as the farm already satisfies.
        for (int guard = 0; guard < 8; guard++)
        {
            Step before = Current;
            Advance();
            if (Current == before) break;
        }
    }

    private void Advance()
    {
        switch (Current)
        {
            case Step.TapFirst:
                if (IsPicked(first) || first.Plan.watering != Care.Off) Current = Step.TryWatering;
                break;
            case Step.TryWatering:
                if (first.Plan.watering != Care.Off) Current = Step.WaitFirstHarvest;
                break;
            case Step.WaitFirstHarvest:
                if (first.CropsSold > 0 && first.Plan.watering != Care.Off)
                {
                    if (farm != null) farm.GrantField(second);
                    Current = Step.TapSecond;
                }
                break;
            case Step.TapSecond:
                if (IsPicked(second) || second.Plan.fertilizer != Care.Off) Current = Step.TryFertilizer;
                break;
            case Step.TryFertilizer:
                if (second.Plan.fertilizer != Care.Off) Current = Step.WaitSecondHarvest;
                break;
            case Step.WaitSecondHarvest:
                if (second.HasResult && second.LastResult.fertilizerCost > 0) Current = Step.Done;
                break;
        }
    }

    private bool IsPicked(FarmField field)
    {
        return picked != null && picked == field.Parcel;
    }

    /// <summary>The hint for the current step, or empty when there is nothing to say.</summary>
    public string Message
    {
        get
        {
            string a = first != null ? first.DisplayName : "";
            string b = second != null ? second.DisplayName : "";
            switch (Current)
            {
                case Step.TapFirst:
                    return a + " already grows cabbages on its own. Tap it to see how it is doing.";
                case Step.TryWatering:
                    return a + " is fertile but dry: only " + Percent(first.Moisture) + " moisture. Open Farm plan and give it some watering.";
                case Step.WaitFirstHarvest:
                    return "Plan saved. The machines use it from the next crop on, with no more taps. Watch the harvest.";
                case Step.TapSecond:
                    return b + " is yours too. Its soil is moist but less fertile. Tap it.";
                case Step.TryFertilizer:
                    return b + " has water to spare but only " + Percent(second.Fertility) + " fertility. Try the drone's fertilizer instead.";
                case Step.WaitSecondHarvest:
                    if (second.Running && second.ActivePlan.fertilizer == Care.Off)
                        return "The fertilizer starts with " + b + "'s next crop. Two fields, two plans: compare their harvests.";
                    return "Two fields, two plans. Compare their harvests once " + b + " sells.";
                default:
                    if (!GoalReached && depotSite != null)
                        return "Planting density is yours to tune now. Next goal: an equipment depot on " + depotSite.DisplayName
                            + " brings a third set of machines.";
                    if (third != null && !third.Owned)
                        return "The depot's machines are ready. Buy " + third.DisplayName + " and plant it.";
                    if (third != null && !third.Farming && third.LandUse == FarmField.Use.Empty)
                        return third.DisplayName + " is yours. Tap it and choose Plant.";
                    return string.Empty;
            }
        }
    }

    private static string Percent(float value)
    {
        return Mathf.RoundToInt(value * 100f) + "%";
    }
}
