using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Decides what the picked parcel offers and what its full page shows: the farming page
/// (<see cref="FarmPage"/> binds that, also for preparing an empty field before its first
/// Start), or one land decision - buy, build the depot, look after the depot - and, on a farmed
/// field, Clear field.
///
/// The buttons only ask; <see cref="Farm"/> checks money, equipment and rules and does the
/// work, and when it refuses, the reason is shown instead of a button that does nothing.
/// Anything that cannot be undone for free needs a second tap to confirm.
/// </summary>
[DisallowMultipleComponent]
public class LandPage : MonoBehaviour
{
    private enum Mode { Plan, Buy, Build, Depot }

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
    [Tooltip("The farming page: crop, settings, forecast, actions, history.")]
    [SerializeField] private GameObject planContent;
    [Tooltip("The land decision: a description, one button and a line for why it is not possible.")]
    [SerializeField] private GameObject landContent;
    [SerializeField] private TMP_Text landInfo;
    [SerializeField] private Button landButton;
    [SerializeField] private TMP_Text landButtonLabel;
    [SerializeField] private TMP_Text landNote;

    [Header("Clear field")]
    [SerializeField] private GameObject clearContent;
    [SerializeField] private Button clearButton;
    [SerializeField] private TMP_Text clearButtonLabel;
    [SerializeField] private TMP_Text clearNote;

    [Header("Confirmation")]
    [Tooltip("Seconds a first tap stays armed before the button returns to normal.")]
    [SerializeField] private float confirmSeconds = 4f;
    [Tooltip("Seconds the buttons ignore taps after doing something, so a burst of taps cannot run into the next decision.")]
    [SerializeField] private float cooldownSeconds = 1f;

    private FarmField field;
    private Mode mode;
    private bool landArmed, clearArmed;
    private float armedUntil;
    private float quietUntil;

    private void OnEnable()
    {
        if (channel != null) channel.Selected += OnSelected;
        if (primaryAction != null) primaryAction.onClick.AddListener(OnPrimary);
        if (secondaryAction != null) secondaryAction.onClick.AddListener(OnSecondary);
        if (landButton != null) landButton.onClick.AddListener(OnLandButton);
        if (clearButton != null) clearButton.onClick.AddListener(OnClearButton);
    }

    private void OnDisable()
    {
        if (channel != null) channel.Selected -= OnSelected;
        if (primaryAction != null) primaryAction.onClick.RemoveListener(OnPrimary);
        if (secondaryAction != null) secondaryAction.onClick.RemoveListener(OnSecondary);
        if (landButton != null) landButton.onClick.RemoveListener(OnLandButton);
        if (clearButton != null) clearButton.onClick.RemoveListener(OnClearButton);
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
        if (field.Owned && !field.Arable) return Mode.Build;
        return Mode.Plan;
    }

    private void OnPrimary() { Disarm(); mode = DefaultMode(); }

    private void OnSecondary()
    {
        Disarm();
        mode = Mode.Build;
        if (panel != null) panel.SetExpanded(true);
    }

    private void Disarm() { landArmed = false; clearArmed = false; }

    private void Arm(bool land)
    {
        landArmed = land;
        clearArmed = !land;
        armedUntil = Time.unscaledTime + confirmSeconds;
    }

    private void Update()
    {
        if ((landArmed || clearArmed) && Time.unscaledTime > armedUntil) Disarm();

        Farm farm = CurrentFarm;
        bool known = field != null && farm != null;
        ShowActions(known ? farm : null);
        if (!known)
        {
            SetShown(planContent, false);
            SetShown(landContent, false);
            SetShown(clearContent, false);
            return;
        }

        // Land use can change under an open page (a field cleared after its last crop).
        if (mode == Mode.Plan && !CanPlan) mode = DefaultMode();
        if (mode == Mode.Depot && field.LandUse != FarmField.Use.Depot) mode = DefaultMode();

        SetShown(planContent, mode == Mode.Plan && CanPlan);
        SetShown(landContent, mode != Mode.Plan);
        SetShown(clearContent, mode == Mode.Plan && field.Farming);

        if (mode == Mode.Plan) ShowClear();
        else ShowLand(farm);
    }

    // Farmed land, or owned empty farmland being prepared for its first Start.
    private bool CanPlan
    {
        get { return field.Farming || (field.Owned && field.Arable && field.LandUse == FarmField.Use.Empty); }
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
                if (field.Arable) primary = "Plant";
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
        string money = "\nCoins " + farm.Coins + " · kept for running fields " + Mathf.Min(farm.Coins, farm.ProtectedTotal) + " · free to spend " + farm.Spendable + ".";
        string info, button, why;
        bool danger = false;
        switch (mode)
        {
            case Mode.Buy:
                info = "<b>" + field.DisplayName + " is for sale.</b>\nPrice: " + field.PurchasePrice + " coins, paid once. Land is not sold back.\n"
                    + "Once it is yours, prepare a crop and Start it (it needs a free equipment set), or build on it." + money;
                button = "Buy for " + field.PurchasePrice + " coins";
                why = farm.WhyCannotBuy(field);
                break;
            case Mode.Build:
                info = "<b>Equipment depot · " + rules.DepotPrice + " coins</b>\nComes with one more equipment set: a seeder, a drone and an irrigation unit, stored here.\n"
                    + "Equipment sets: " + farm.OwnedSets + " -> " + (farm.OwnedSets + rules.DepotSets) + ". One depot per farm. No coins come back if you remove it." + money;
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

        if (landArmed) button = "Tap again to confirm";
        if (landInfo != null) landInfo.text = info;
        if (landButtonLabel != null) landButtonLabel.text = button;
        if (landButton != null) landButton.interactable = why == null;
        if (landNote != null) landNote.text = why ?? (danger && landArmed ? "This cannot be undone." : string.Empty);
    }

    private void OnLandButton()
    {
        Farm farm = CurrentFarm;
        if (farm == null || field == null || Time.unscaledTime < quietUntil) return;
        if (!landArmed) { Arm(true); return; }
        Disarm();

        bool done = false;
        switch (mode)
        {
            case Mode.Buy: done = farm.BuyLand(field); break;
            case Mode.Build: done = farm.BuildDepot(field); break;
            case Mode.Depot: done = farm.RemoveDepot(); break;
        }
        if (!done) return;
        quietUntil = Time.unscaledTime + cooldownSeconds;

        // Buying, building and removing change what the sheet offers, so the page closes to
        // show it (and the next decision's button is not under the finger).
        mode = DefaultMode();
        if (panel != null) panel.SetExpanded(false);
    }

    // ---------- clearing a farmed field ----------

    private void ShowClear()
    {
        if (!field.Farming) return;
        if (field.PendingClear)
        {
            if (clearButtonLabel != null) clearButtonLabel.text = "Keep farming";
            if (clearNote != null) clearNote.text = "Clearing: the current crop finishes once, then the land is emptied, its plan reset and its equipment set freed.";
            return;
        }
        if (clearButtonLabel != null) clearButtonLabel.text = clearArmed ? "Tap again to clear" : "Clear field";
        if (clearNote != null) clearNote.text = clearArmed
            ? "Empties the land and frees its equipment set after any growing crop. Nothing is refunded."
            : "To stop after one crop but keep the set, switch Repeat off instead.";
    }

    private void OnClearButton()
    {
        Farm farm = CurrentFarm;
        if (farm == null || field == null || !field.Farming || Time.unscaledTime < quietUntil) return;

        if (field.PendingClear) { farm.RequestClear(field, false); Disarm(); }
        else if (!clearArmed) { Arm(false); return; }
        else { Disarm(); farm.RequestClear(field, true); }
        quietUntil = Time.unscaledTime + cooldownSeconds;
    }

    private static void SetShown(GameObject target, bool shown)
    {
        if (target != null && target.activeSelf != shown) target.SetActive(shown);
    }
}
