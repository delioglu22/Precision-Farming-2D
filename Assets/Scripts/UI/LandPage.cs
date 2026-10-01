using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Decides what the picked parcel offers and what its full page shows: the farming plan
/// (<see cref="FarmPage"/> binds that part), or one land decision - buy, plant, build the
/// depot, look after the depot - and, on a farmed field, stopping.
///
/// The buttons only ask; <see cref="Farm"/> checks money and equipment and does the work,
/// and when it refuses, the reason is shown instead of a button that silently does nothing.
/// Anything that cannot be undone for free needs a second tap to confirm.
/// </summary>
[DisallowMultipleComponent]
public class LandPage : MonoBehaviour
{
    private enum Mode { Plan, Buy, Plant, Build, Depot }

    [Header("Selection")]
    [SerializeField] private ParcelSelectionChannel channel;
    [SerializeField] private FarmChannel farmChannel;
    [SerializeField] private ParcelPanel panel;

    [Header("Sheet actions")]
    [Tooltip("The green header button. Its Inspector call expands the page.")]
    [SerializeField] private Button primaryAction;
    [SerializeField] private TMP_Text primaryLabel;
    [Tooltip("The yellow header button, used for Build.")]
    [SerializeField] private Button secondaryAction;
    [SerializeField] private TMP_Text secondaryLabel;

    [Header("Page content")]
    [Tooltip("The machine rows, plan bar and harvest result.")]
    [SerializeField] private GameObject planContent;
    [Tooltip("The land decision: a description, one button and a line for why it is not possible.")]
    [SerializeField] private GameObject landContent;
    [SerializeField] private TMP_Text landInfo;
    [SerializeField] private Button landButton;
    [SerializeField] private TMP_Text landButtonLabel;
    [SerializeField] private TMP_Text landNote;

    [Header("Stop farming")]
    [SerializeField] private GameObject stopContent;
    [SerializeField] private Button stopButton;
    [SerializeField] private TMP_Text stopButtonLabel;
    [SerializeField] private TMP_Text stopNote;

    [Header("Confirmation")]
    [Tooltip("Seconds a first tap stays armed before the button returns to normal.")]
    [SerializeField] private float confirmSeconds = 4f;
    [Tooltip("Seconds the land button ignores taps after it did something, so a burst of taps cannot run into the next decision.")]
    [SerializeField] private float cooldownSeconds = 1f;

    private FarmField field;
    private Mode mode;
    private bool landArmed, stopArmed;
    private float armedUntil;
    private float quietUntil;

    private void OnEnable()
    {
        if (channel != null) channel.Selected += OnSelected;
        if (primaryAction != null) primaryAction.onClick.AddListener(OnPrimary);
        if (secondaryAction != null) secondaryAction.onClick.AddListener(OnSecondary);
        if (landButton != null) landButton.onClick.AddListener(OnLandButton);
        if (stopButton != null) stopButton.onClick.AddListener(OnStopButton);
    }

    private void OnDisable()
    {
        if (channel != null) channel.Selected -= OnSelected;
        if (primaryAction != null) primaryAction.onClick.RemoveListener(OnPrimary);
        if (secondaryAction != null) secondaryAction.onClick.RemoveListener(OnSecondary);
        if (landButton != null) landButton.onClick.RemoveListener(OnLandButton);
        if (stopButton != null) stopButton.onClick.RemoveListener(OnStopButton);
    }

    private Farm CurrentFarm { get { return farmChannel != null ? farmChannel.Current : null; } }

    private void OnSelected(Parcel parcel)
    {
        field = parcel != null ? parcel.GetComponent<FarmField>() : null;
        Disarm();
        mode = DefaultMode();
    }

    // What the green button opens for this land right now.
    private Mode DefaultMode()
    {
        if (field == null) return Mode.Plan;
        if (field.Farming) return Mode.Plan;
        if (field.LandUse == FarmField.Use.Depot) return Mode.Depot;
        if (field.ForSale) return Mode.Buy;
        return Mode.Plant;
    }

    private void OnPrimary() { Disarm(); mode = DefaultMode(); }

    private void OnSecondary()
    {
        Disarm();
        mode = Mode.Build;
        if (panel != null) panel.SetExpanded(true);
    }

    private void Disarm() { landArmed = false; stopArmed = false; }

    private void Arm(bool land)
    {
        landArmed = land;
        stopArmed = !land;
        armedUntil = Time.unscaledTime + confirmSeconds;
    }

    private void Update()
    {
        if ((landArmed || stopArmed) && Time.unscaledTime > armedUntil) Disarm();

        Farm farm = CurrentFarm;
        bool known = field != null && farm != null;
        ShowActions(known ? farm : null);
        if (!known)
        {
            SetShown(landContent, false);
            SetShown(stopContent, false);
            return;
        }

        // Land use can change under an open page (a crop finishing its last harvest).
        if (mode == Mode.Plan && !field.Farming) mode = DefaultMode();

        SetShown(planContent, mode == Mode.Plan);
        SetShown(landContent, mode != Mode.Plan);
        SetShown(stopContent, mode == Mode.Plan && field.Farming);

        if (mode == Mode.Plan) ShowStop(farm);
        else ShowLand(farm);
    }

    // ---------- the sheet's buttons ----------

    private void ShowActions(Farm farm)
    {
        string primary = null, secondary = null;
        if (farm != null)
        {
            if (field.Farming) primary = "Farm plan";
            else if (field.LandUse == FarmField.Use.Depot) primary = "Depot";
            else if (field.ForSale) primary = "Buy land";
            else if (field.Owned)
            {
                secondary = "Build";
                if (farm.CanEverPlant(field)) primary = "Plant";
            }
        }
        SetShown(primaryAction != null ? primaryAction.gameObject : null, primary != null);
        SetShown(secondaryAction != null ? secondaryAction.gameObject : null, secondary != null);
        if (primary != null && primaryLabel != null) primaryLabel.text = primary;
        if (secondary != null && secondaryLabel != null) secondaryLabel.text = secondary;
    }

    // ---------- land decisions ----------

    private void ShowLand(Farm farm)
    {
        FarmRules rules = farm.Rules;
        string info, button, why;
        bool danger = false;
        switch (mode)
        {
            case Mode.Buy:
                info = "<b>" + field.DisplayName + " is for sale.</b>\nPrice: " + field.PurchasePrice + " coins, paid once. Land is not sold back.\n"
                    + "Once it is yours, plant it (it needs a free equipment set) or build on it.";
                button = "Buy for " + field.PurchasePrice + " coins";
                why = farm.WhyCannotBuy(field);
                break;
            case Mode.Plant:
                int cost = rules.Costs(field.StarterPlan).Cost;
                info = "<b>Grow cabbages here.</b>\nStarts with the standard plan: standard density, no fertilizer, no watering. You can change it after.\n"
                    + "Uses 1 equipment set: " + Mathf.Max(0, farm.FreeSets) + " free of " + farm.OwnedSets + ".\n"
                    + "First crop: " + cost + " coins, paid when sowing starts.";
                button = "Start farming";
                why = farm.WhyCannotPlant(field);
                break;
            case Mode.Build:
                info = "<b>Equipment depot · " + rules.DepotPrice + " coins</b>\nComes with one more equipment set: a seeder, a drone and an irrigation unit, stored here.\n"
                    + "Equipment sets: " + farm.OwnedSets + " -> " + (farm.OwnedSets + rules.DepotSets) + ". One depot per farm.\n"
                    + "No coins come back if you remove it.";
                button = "Build depot";
                why = farm.WhyCannotBuildDepot(field);
                break;
            default:
                FarmField user = farm.FieldUsingSet(farm.DepotSet);
                info = "<b>Equipment depot</b>\nEquipment sets: " + farm.SetsInUse + " working of " + farm.OwnedSets + ".\n"
                    + "The depot's own set is " + (user != null ? "working " + user.DisplayName + "." : "parked here, ready for a field.") + "\n"
                    + "Removing the depot takes its set away too. No coins come back.";
                button = "Remove depot";
                why = farm.WhyCannotRemoveDepot();
                danger = true;
                break;
        }

        bool needsConfirm = mode == Mode.Buy || mode == Mode.Build || mode == Mode.Depot;
        if (landArmed && needsConfirm) button = "Tap again to confirm";
        if (landInfo != null) landInfo.text = info;
        if (landButtonLabel != null) landButtonLabel.text = button;
        if (landButton != null) landButton.interactable = why == null;
        if (landNote != null) landNote.text = why ?? (danger && landArmed ? "This cannot be undone." : string.Empty);
    }

    private void OnLandButton()
    {
        Farm farm = CurrentFarm;
        if (farm == null || field == null) return;

        if (Time.unscaledTime < quietUntil) return;
        bool needsConfirm = mode == Mode.Buy || mode == Mode.Build || mode == Mode.Depot;
        if (needsConfirm && !landArmed) { Arm(true); return; }
        Disarm();

        bool done = false;
        switch (mode)
        {
            case Mode.Buy: done = farm.BuyLand(field); break;
            case Mode.Plant: done = farm.Plant(field); break;
            case Mode.Build: done = farm.BuildDepot(field); break;
            case Mode.Depot: done = farm.RemoveDepot(); break;
        }
        if (!done) return;
        quietUntil = Time.unscaledTime + cooldownSeconds;

        // Buying, building and removing change what the sheet offers, so the page closes to
        // show it (and the next decision's button is not under the finger). Planting opens
        // the new field's plan instead.
        bool backToSheet = mode != Mode.Plant;
        mode = DefaultMode();
        if (backToSheet && panel != null) panel.SetExpanded(false);
    }

    // ---------- stopping a farmed field ----------

    private void ShowStop(Farm farm)
    {
        if (!field.Farming) return;
        if (field.PendingStop)
        {
            if (stopButtonLabel != null) stopButtonLabel.text = "Keep farming";
            if (stopNote != null) stopNote.text = field.Running
                ? "Stopping: the current crop still grows and sells once, then the land is cleared and its equipment set freed."
                : "Stopping now.";
            return;
        }
        if (stopButtonLabel != null) stopButtonLabel.text = stopArmed ? "Tap again to stop" : "Stop farming";
        if (stopNote != null) stopNote.text = stopArmed
            ? "The current crop still sells once. Then the plan is discarded and the set freed. Nothing is refunded."
            : string.Empty;
    }

    private void OnStopButton()
    {
        Farm farm = CurrentFarm;
        if (farm == null || field == null || !field.Farming || Time.unscaledTime < quietUntil) return;

        if (field.PendingStop) { farm.RequestStop(field, false); Disarm(); }
        else if (!stopArmed) { Arm(false); return; }
        else { Disarm(); farm.RequestStop(field, true); }
        quietUntil = Time.unscaledTime + cooldownSeconds;
    }

    private static void SetShown(GameObject target, bool shown)
    {
        if (target != null && target.activeSelf != shown) target.SetActive(shown);
    }
}
