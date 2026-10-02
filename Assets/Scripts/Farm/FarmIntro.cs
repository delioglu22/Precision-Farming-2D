using UnityEngine;

/// <summary>
/// The farm's lessons, one short hint at a time, advanced by what actually happens on the farm
/// rather than by "Next" buttons:
///
///   1. Start the first field's crop (and switch Repeat on).   2. Watch it sell -> second field granted.
///   3. Start the second field.                                 4. Notice the soil running down.
///   5. Restore a field's soil by hand once.                    6. Research Soil control.
///   7. Install the controller on a field.                      8. See it restore and return.
///   Then: the depot and the third field as the next investments.
///
/// Each frame the flow moves forward as far as the farm already allows, so acting early or out
/// of order never gets it stuck. Unity has no tutorial system; this is a small state machine.
/// </summary>
[DisallowMultipleComponent]
public class FarmIntro : MonoBehaviour
{
    // Saved by number: append new steps at the end of a future version, never reorder.
    public enum Step
    {
        StartFirst, WatchFirst, StartSecond, SoilLesson, RestoreByHand,
        ResearchControl, InstallControl, WatchAutomation, Done
    }

    [Header("Fields")]
    [Tooltip("The field the player starts with (fertile, dry).")]
    [SerializeField] private FarmField first;
    [Tooltip("The field granted after the first sale (moist, less fertile).")]
    [SerializeField] private FarmField second;

    [Header("After the introduction")]
    [Tooltip("Where the equipment depot can go (building-only land).")]
    [SerializeField] private FarmField depotSite;
    [Tooltip("The field to buy and plant once the depot brings a third equipment set.")]
    [SerializeField] private FarmField third;

    [Header("References")]
    [SerializeField] private Farm farm;

    [Header("Lesson tuning")]
    [Tooltip("The soil lesson starts once a farmed field's next crop would start at or below this fertility.")]
    [SerializeField, Range(0, 100)] private int tiredSoil = 45;

    public Step Current { get; private set; }

    /// <summary>Set by the depot purchase to retire the depot hint.</summary>
    public bool GoalReached { get; set; }

    public bool IsValidStep(int step) { return step >= 0 && step <= (int)Step.Done; }

    public void Restore(int step, bool goalReached)
    {
        Current = (Step)Mathf.Clamp(step, 0, (int)Step.Done);
        GoalReached = goalReached;
    }

    /// <summary>
    /// Translates an old (version 1 or 2) introduction step by what the save really contains,
    /// never by casting the number into this list:
    ///   old 0-2 (second field not granted yet): StartFirst, or WatchFirst if the first field ran;
    ///   old 3-5 (second field granted): StartSecond, or SoilLesson if it has already farmed;
    ///   old 6 (finished): SoilLesson, keeping the depot goal as it was.
    /// The forward pass in Update then skips any lesson the farm already satisfies.
    /// </summary>
    public void RestoreFromLegacy(int oldStep, bool goalReached)
    {
        GoalReached = goalReached;
        if (oldStep >= 6) Current = Step.SoilLesson;
        else if (second != null && second.Owned)
            Current = second.Running || second.CashCompleted > 0 ? Step.SoilLesson : Step.StartSecond;
        else
            Current = first != null && (first.Running || first.CashCompleted > 0) ? Step.WatchFirst : Step.StartFirst;
    }

    private void Update()
    {
        if (first == null || second == null || farm == null || farm.LoadProblem != null) return;
        for (int guard = 0; guard < 10; guard++)
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
            case Step.StartFirst:
                if (Started(first)) Current = Step.WatchFirst;
                break;
            case Step.WatchFirst:
                if (first.CashCompleted > 0)
                {
                    farm.GrantField(second);
                    Current = Step.StartSecond;
                }
                break;
            case Step.StartSecond:
                // A player who skips the second field but already meets tired soil moves on too.
                if (Started(second) || TiredField() != null || farm.ManualRecoveryTotal > 0) Current = Step.SoilLesson;
                break;
            case Step.SoilLesson:
                if (farm.ManualRecoveryTotal > 0 || TiredField() != null) Current = Step.RestoreByHand;
                break;
            case Step.RestoreByHand:
                if (farm.ManualRecoveryTotal > 0) Current = Step.ResearchControl;
                break;
            case Step.ResearchControl:
                if (farm.ControllersUnlocked) Current = Step.InstallControl;
                break;
            case Step.InstallControl:
                if (InstalledField() != null) Current = Step.WatchAutomation;
                break;
            case Step.WatchAutomation:
                if (farm.AutomaticReturns > 0 || GoalReached) Current = Step.Done;
                break;
        }
    }

    private static bool Started(FarmField f)
    {
        return f.Running || f.CashCompleted > 0 || f.RecoveryCompleted > 0;
    }

    // The farmed field with the poorest soil, if it has run down enough to talk about.
    private FarmField TiredField()
    {
        FarmField worst = null;
        foreach (FarmField f in farm.Fields)
        {
            if (f == null || !f.Farming) continue;
            bool tired = farm.NextStartFertility(f) <= tiredSoil || f.Pause == PauseReason.Unprofitable;
            if (tired && (worst == null || f.Fertility < worst.Fertility)) worst = f;
        }
        return worst;
    }

    private FarmField PoorestFarmedField()
    {
        FarmField worst = null;
        foreach (FarmField f in farm.Fields)
            if (f != null && f.Farming && (worst == null || f.Fertility < worst.Fertility)) worst = f;
        return worst;
    }

    private FarmField InstalledField()
    {
        foreach (FarmField f in farm.Fields) if (f != null && f.ControllerInstalled) return f;
        return null;
    }

    /// <summary>The field the current hint is about, marked with the bobbing arrow; or null.</summary>
    public FarmField Target
    {
        get
        {
            if (farm == null || first == null || second == null) return null;
            switch (Current)
            {
                case Step.StartFirst: return first.Running ? null : first;
                case Step.StartSecond: return second.Running ? null : second;
                case Step.RestoreByHand:
                case Step.InstallControl:
                    FarmField f = TiredField();
                    return f != null ? f : PoorestFarmedField();
                case Step.Done:
                    if (!GoalReached) return depotSite;
                    if (third != null && !third.Farming) return third;
                    return null;
                default: return null;
            }
        }
    }

    /// <summary>The hint for the current step, or empty when there is nothing to say.</summary>
    public string Message
    {
        get
        {
            if (farm == null || first == null || second == null) return string.Empty;
            string a = first.DisplayName, b = second.DisplayName;
            switch (Current)
            {
                case Step.StartFirst:
                    return a + " is dry but fertile. Open Farm plan: density 50%, fertilizer 0%, watering 50% suits it. Check the forecast, then Start.";
                case Step.WatchFirst:
                    return first.Repeat
                        ? "Repeat is on: " + a + " keeps farming with this plan. Watch the harvest and the soil change."
                        : "Turn Repeat on to keep " + a + " farming without another tap, then watch the harvest.";
                case Step.StartSecond:
                    return b + " is yours. It is moist but less fertile: try density 50%, fertilizer 50%, watering 0%, then Start.";
                case Step.SoilLesson:
                    return "Every cabbage crop uses soil. Watch the forecast: lower fertility means a smaller harvest and less profit.";
                case Step.RestoreByHand:
                    FarmField tired = TiredField();
                    if (tired == null) tired = PoorestFarmedField();
                    return (tired != null ? tired.DisplayName + "'s" : "A field's") + " soil is running low. Choose Restore soil: green beans cost nothing and put back "
                        + farm.Rules.RestorationGain + " points. Start it once.";
                case Step.ResearchControl:
                    return "You restored soil by hand. Open Research and buy Soil control: it comes with one free controller kit.";
                case Step.InstallControl:
                    return "Install the soil controller on a field. It restores the soil below " + farm.Rules.RestoreBelow + "% and returns to cabbage above " + farm.Rules.ReturnAbove + "%.";
                case Step.WatchAutomation:
                    return "The controller now looks after its field's soil on its own. Meanwhile, research can cut costs, or save up for an equipment depot.";
                default:
                    if (!GoalReached && depotSite != null)
                        return "Next goal: an equipment depot on " + depotSite.DisplayName + " brings a third set of machines.";
                    if (third != null && !third.Owned)
                        return "The depot's machines are ready. Buy " + third.DisplayName + " and plant it.";
                    if (third != null && !third.Farming && third.LandUse == FarmField.Use.Empty)
                        return third.DisplayName + " is yours. Tap it, choose Plant, check the forecast and Start.";
                    return string.Empty;
            }
        }
    }
}
