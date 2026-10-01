using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Fills the parcel panel with the picked field's farming data and lets the player edit its
/// plan. <see cref="ParcelPanel"/> keeps the navigation (open, expand, close); this only binds.
///
/// Unity's UI has the buttons and layout but no data binding, so this script is the glue: it
/// reads the <see cref="FarmField"/> on the picked parcel and writes text and button looks.
///
/// Edits go into a draft first. Save copies the draft onto the field; picking another parcel
/// or closing the panel throws the draft away, so an unsaved edit never leaks into a field.
/// </summary>
[DisallowMultipleComponent]
public class FarmPage : MonoBehaviour
{
    /// <summary>One machine's three choices, shown as three buttons in a row.</summary>
    [System.Serializable]
    public class OptionRow
    {
        public Button[] options = new Button[3];
    }

    [Header("Selection")]
    [Tooltip("Picks are announced here. The same asset the map scene raises on.")]
    [SerializeField] private ParcelSelectionChannel channel;
    [Tooltip("Where the running farm (wallet, rules) is found.")]
    [SerializeField] private FarmChannel farmChannel;

    [Header("Sheet")]
    [Tooltip("Hidden for parcels that are not part of the farm, so no reading is invented.")]
    [SerializeField] private GameObject readings;
    [SerializeField] private TMP_Text fertilityValue;
    [SerializeField] private TMP_Text moistureValue;
    [Tooltip("One line about the crop: what it is doing and how long is left.")]
    [SerializeField] private TMP_Text statusValue;
    [Tooltip("Reads Crop on a farmed field and Land everywhere else.")]
    [SerializeField] private TMP_Text statusLabel;

    [Header("Plan")]
    [Tooltip("Rows revealed step by step during the introduction.")]
    [SerializeField] private GameObject densityRow;
    [SerializeField] private GameObject fertilizerRow;
    [SerializeField] private OptionRow density;
    [SerializeField] private OptionRow fertilizer;
    [SerializeField] private OptionRow watering;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button revertButton;
    [SerializeField] private TMP_Text planNote;

    [Header("Result")]
    [SerializeField] private TMP_Text resultTitle;
    [Tooltip("The income/cost breakdown, one line each, numbers lined up with a <pos> tag.")]
    [SerializeField] private TMP_Text resultLines;

    [Header("Looks")]
    [SerializeField] private Sprite chosenSprite;
    [SerializeField] private Sprite otherSprite;
    [SerializeField] private Color chosenText = new Color32(0xEC, 0xE4, 0xD2, 0xFF);
    [SerializeField] private Color otherText = new Color32(0x35, 0x29, 0x1B, 0xFF);

    private FarmField field;
    private FarmPlan draft;
    // The field's plan when the draft was last taken from it. If the field's plan changes on
    // its own (replanting resets it) while the player has not edited, the draft follows.
    private FarmPlan synced;

    private void OnEnable()
    {
        if (channel != null) channel.Selected += OnSelected;
        Hook(density, i => draft.density = (Density)i);
        Hook(fertilizer, i => draft.fertilizer = (Care)i);
        Hook(watering, i => draft.watering = (Care)i);
        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (revertButton != null) revertButton.onClick.AddListener(Revert);
        Refresh();
    }

    private void OnDisable()
    {
        if (channel != null) channel.Selected -= OnSelected;
        Unhook(density);
        Unhook(fertilizer);
        Unhook(watering);
        if (saveButton != null) saveButton.onClick.RemoveListener(Save);
        if (revertButton != null) revertButton.onClick.RemoveListener(Revert);
    }

    // Each option button reports its own index. The lambda has to copy the loop variable,
    // or every button would report the last index.
    private void Hook(OptionRow row, System.Action<int> choose)
    {
        if (row == null) return;
        for (int i = 0; i < row.options.Length; i++)
        {
            int index = i;
            if (row.options[i] != null)
                row.options[i].onClick.AddListener(() => { choose(index); Refresh(); });
        }
    }

    // RemoveAllListeners only clears listeners added from code, never ones set in the Inspector.
    private void Unhook(OptionRow row)
    {
        if (row == null) return;
        foreach (Button b in row.options)
            if (b != null) b.onClick.RemoveAllListeners();
    }

    // Text that changes with time (the crop's stage, the last harvest) is rewritten every frame
    // while a field is shown. Buttons are only touched when the draft or the pick changes.
    private void Update()
    {
        if (field != null) ShowLive();
    }

    private void OnSelected(Parcel parcel)
    {
        field = parcel != null ? parcel.GetComponent<FarmField>() : null;
        if (field != null) { draft = field.Plan; synced = field.Plan; }
        Refresh();
    }

    private void Save()
    {
        if (field == null) return;
        field.ConfirmPlan(draft);
        synced = draft;
        Refresh();
    }

    private void Revert()
    {
        if (field == null) return;
        draft = field.Plan;
        synced = field.Plan;
        Refresh();
    }

    private void Refresh()
    {
        bool known = field != null;
        if (readings != null) readings.SetActive(known);
        if (!known) return;

        if (fertilityValue != null) fertilityValue.text = Percent(field.Fertility);
        if (moistureValue != null) moistureValue.text = Percent(field.Moisture);

        Show(density, (int)draft.density);
        Show(fertilizer, (int)draft.fertilizer);
        Show(watering, (int)draft.watering);

        ShowLive();
    }

    private void ShowLive()
    {
        Farm farm = farmChannel != null ? farmChannel.Current : null;
        if (farm == null || farm.Rules == null) return;
        FarmRules rules = farm.Rules;

        if (statusValue != null) statusValue.text = Status(farm);
        if (statusLabel != null) statusLabel.text = field.Farming ? "Crop" : "Land";

        if (!field.Plan.Same(synced))
        {
            bool untouched = draft.Same(synced);
            synced = field.Plan;
            if (untouched) { draft = field.Plan; Refresh(); return; }
        }

        FarmIntro intro = farm.Intro;
        if (fertilizerRow != null) fertilizerRow.SetActive(intro == null || intro.FertilizerUnlocked);
        if (densityRow != null) densityRow.SetActive(intro == null || intro.DensityUnlocked);

        bool edited = !draft.Same(field.Plan);
        if (saveButton != null) saveButton.interactable = edited;
        if (revertButton != null) revertButton.interactable = edited;

        if (planNote != null)
        {
            int cost = rules.Costs(draft).Cost;
            if (edited) planNote.text = "Unsaved · " + cost + " coins per crop";
            else if (field.Running && !field.ActivePlan.Same(field.Plan)) planNote.text = "Saved · starts with the next crop";
            else planNote.text = "Saved · " + cost + " coins per crop";
        }

        if (resultTitle != null) resultTitle.text = "Last harvest · farm coins " + farm.Coins;
        if (resultLines == null) return;
        if (!field.HasResult)
        {
            resultLines.text = "The first crop has not been sold yet.";
            return;
        }
        HarvestResult r = field.LastResult;
        resultLines.text =
            "Harvest sold<pos=75%>+" + r.income + "\n" +
            "Seed<pos=75%>-" + r.seedCost + "\n" +
            "Fertilizer<pos=75%>-" + r.fertilizerCost + "\n" +
            "Water<pos=75%>-" + r.waterCost + "\n" +
            "<b>Net<pos=75%>" + (r.Net >= 0 ? "+" : "") + r.Net + "</b>";
    }

    private string Status(Farm farm)
    {
        FarmRules rules = farm.Rules;
        if (field.ForSale) return "For sale · " + field.PurchasePrice + " coins";
        if (!field.Owned) return "Not your land yet";
        if (field.LandUse == FarmField.Use.Depot) return "Equipment depot";
        if (!field.Farming) return farm.CanEverPlant(field) ? "Empty land" : "Empty land · too poor to farm";
        if (field.PendingStop && field.Running) return "Last crop · " + field.CurrentStage(rules);
        if (field.PendingStop) return "Stopping";
        if (field.WaitingForMoney) return "Waiting for " + rules.Costs(field.Plan).Cost + " coins";

        FarmField.Stage stage = field.CurrentStage(rules);
        float progress = field.StageProgress(rules);
        switch (stage)
        {
            case FarmField.Stage.Sowing: return "Sowing · " + SecondsLeft(progress, rules.SowSeconds);
            case FarmField.Stage.Growing: return "Growing · " + SecondsLeft(progress, rules.GrowSeconds);
            case FarmField.Stage.Harvesting: return "Harvesting · " + SecondsLeft(progress, rules.HarvestSeconds);
            default: return "Starting";
        }
    }

    private static string SecondsLeft(float progress, float stageSeconds)
    {
        return Mathf.CeilToInt((1f - progress) * stageSeconds) + "s";
    }

    private void Show(OptionRow row, int chosen)
    {
        if (row == null) return;
        for (int i = 0; i < row.options.Length; i++)
        {
            if (row.options[i] == null) continue;
            Image image = row.options[i].image;
            if (image != null) image.sprite = i == chosen ? chosenSprite : otherSprite;
            TMP_Text label = row.options[i].GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = i == chosen ? chosenText : otherText;
        }
    }

    private static string Percent(float value)
    {
        return Mathf.RoundToInt(value * 100f) + "%";
    }
}
