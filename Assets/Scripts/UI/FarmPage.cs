using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Fills the parcel panel with the picked field's farming data and lets the player prepare,
/// start and adjust its crops. <see cref="ParcelPanel"/> keeps the navigation and
/// <see cref="LandPage"/> the land decisions; this binds the farming part.
///
/// Unity's UI has the sliders, toggles and layout but no data binding, so this script is the
/// glue. It never decides anything itself: the forecast comes from <see cref="Farm.Decide"/>,
/// the same call that chooses the real crop, and every button goes through a Farm method that
/// checks its rules again.
///
/// Edits go into a draft first. Start (on an idle field) or Apply next crop (while a crop
/// grows) confirms it; Undo or picking another parcel throws it away.
/// </summary>
[DisallowMultipleComponent]
public class FarmPage : MonoBehaviour
{
    /// <summary>One machine's setting: a slider in steps of five percent and its value label.</summary>
    [System.Serializable]
    public class SettingRow
    {
        public GameObject row;
        [Tooltip("Whole numbers 0..20; each step is five percent.")]
        public Slider slider;
        public TMP_Text value;
    }

    [Header("Selection")]
    [Tooltip("Picks are announced here. The same asset the map scene raises on.")]
    [SerializeField] private ParcelSelectionChannel channel;
    [Tooltip("Where the running farm is found.")]
    [SerializeField] private FarmChannel farmChannel;

    [Header("Sheet")]
    [Tooltip("Hidden for parcels that are not part of the farm, so no reading is invented.")]
    [SerializeField] private GameObject readings;
    [SerializeField] private TMP_Text fertilityValue;
    [SerializeField] private TMP_Text moistureValue;
    [Tooltip("Reads Crop on farmed land and Land everywhere else.")]
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text statusValue;

    [Header("Crop choice")]
    [SerializeField] private Button cashButton;
    [SerializeField] private Button recoveryButton;
    [SerializeField] private TMP_Text cropNote;

    [Header("Cabbage settings")]
    [SerializeField] private SettingRow density;
    [SerializeField] private SettingRow fertilizer;
    [SerializeField] private SettingRow watering;

    [Header("Forecast")]
    [SerializeField] private TMP_Text forecastTitle;
    [SerializeField] private TMP_Text forecastLines;
    [Tooltip("Red text: why this plan cannot start, or will stop before its next crop.")]
    [SerializeField] private TMP_Text forecastWarning;

    [Header("Actions")]
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text startLabel;
    [SerializeField] private Button undoButton;
    [SerializeField] private Toggle repeatToggle;

    [Header("Growing now")]
    [SerializeField] private GameObject currentBox;
    [SerializeField] private TMP_Text currentLines;

    [Header("Soil controller")]
    [SerializeField] private GameObject controllerRow;
    [SerializeField] private Button installButton;
    [SerializeField] private TMP_Text installLabel;
    [SerializeField] private Toggle autoToggle;
    [SerializeField] private TMP_Text controllerNote;

    [Header("History")]
    [SerializeField] private TMP_Text lastLines;

    [Header("Looks")]
    [SerializeField] private Sprite chosenSprite;
    [SerializeField] private Sprite otherSprite;
    [SerializeField] private Color chosenText = new Color32(0xEC, 0xE4, 0xD2, 0xFF);
    [SerializeField] private Color otherText = new Color32(0x35, 0x29, 0x1B, 0xFF);

    private FarmField field;
    private FarmPlan draft;
    private CropKind draftCrop;
    // The field's confirmed settings when the draft was last taken from them. If they change
    // on their own (a cleared field resets) while the player has not edited, the draft follows.
    private FarmPlan synced;
    private CropKind syncedCrop;
    private bool refreshing;

    private Farm CurrentFarm { get { return farmChannel != null ? farmChannel.Current : null; } }

    private void OnEnable()
    {
        if (channel != null) channel.Selected += OnSelected;
        Hook(density, v => draft.density = v);
        Hook(fertilizer, v => draft.fertilizer = v);
        Hook(watering, v => draft.watering = v);
        if (cashButton != null) cashButton.onClick.AddListener(ChooseCash);
        if (recoveryButton != null) recoveryButton.onClick.AddListener(ChooseRecovery);
        if (startButton != null) startButton.onClick.AddListener(OnStart);
        if (undoButton != null) undoButton.onClick.AddListener(Revert);
        if (repeatToggle != null) repeatToggle.onValueChanged.AddListener(OnRepeat);
        if (installButton != null) installButton.onClick.AddListener(OnInstall);
        if (autoToggle != null) autoToggle.onValueChanged.AddListener(OnAuto);
    }

    private void OnDisable()
    {
        if (channel != null) channel.Selected -= OnSelected;
        Unhook(density);
        Unhook(fertilizer);
        Unhook(watering);
        if (cashButton != null) cashButton.onClick.RemoveListener(ChooseCash);
        if (recoveryButton != null) recoveryButton.onClick.RemoveListener(ChooseRecovery);
        if (startButton != null) startButton.onClick.RemoveListener(OnStart);
        if (undoButton != null) undoButton.onClick.RemoveListener(Revert);
        if (repeatToggle != null) repeatToggle.onValueChanged.RemoveListener(OnRepeat);
        if (installButton != null) installButton.onClick.RemoveListener(OnInstall);
        if (autoToggle != null) autoToggle.onValueChanged.RemoveListener(OnAuto);
    }

    private void Hook(SettingRow row, System.Action<int> set)
    {
        if (row == null || row.slider == null) return;
        row.slider.onValueChanged.AddListener(v => { if (!refreshing) set(Mathf.RoundToInt(v) * FarmPlan.Step); });
    }

    // RemoveAllListeners only clears listeners added from code, never ones set in the Inspector.
    private void Unhook(SettingRow row)
    {
        if (row != null && row.slider != null) row.slider.onValueChanged.RemoveAllListeners();
    }

    private void OnSelected(Parcel parcel)
    {
        field = parcel != null ? parcel.GetComponent<FarmField>() : null;
        if (field != null) TakeDraft();
    }

    private void TakeDraft()
    {
        draft = field.CashPlan;
        draftCrop = field.ManualCrop;
        synced = draft;
        syncedCrop = draftCrop;
    }

    private void ChooseCash() { draftCrop = CropKind.Cash; }
    private void ChooseRecovery() { draftCrop = CropKind.Recovery; }

    private bool Edited { get { return field != null && (!draft.Same(field.CashPlan) || draftCrop != field.ManualCrop); } }

    private void OnStart()
    {
        Farm farm = CurrentFarm;
        if (farm == null || field == null) return;
        if (field.Running) farm.ApplyNextCrop(field, draft, draftCrop);
        else farm.StartField(field, draft, draftCrop);
        synced = field.CashPlan;
        syncedCrop = field.ManualCrop;
    }

    private void Revert()
    {
        if (field != null) TakeDraft();
    }

    private void OnRepeat(bool on)
    {
        Farm farm = CurrentFarm;
        if (!refreshing && farm != null && field != null) farm.SetRepeat(field, on);
    }

    private void OnInstall()
    {
        Farm farm = CurrentFarm;
        if (farm != null && field != null) farm.InstallController(field);
    }

    private void OnAuto(bool on)
    {
        Farm farm = CurrentFarm;
        if (!refreshing && farm != null && field != null) farm.SetControllerEnabled(field, on);
    }

    // Everything on the page follows the farm's state each frame while a field is shown; the
    // panel is small and nothing here is expensive.
    private void Update()
    {
        Farm farm = CurrentFarm;
        bool known = field != null && farm != null && farm.Rules != null;
        if (readings != null && readings.activeSelf != known) readings.SetActive(known);
        if (!known) return;

        if (!field.CashPlan.Same(synced) || field.ManualCrop != syncedCrop)
        {
            bool untouched = draft.Same(synced) && draftCrop == syncedCrop;
            synced = field.CashPlan;
            syncedCrop = field.ManualCrop;
            if (untouched) { draft = synced; draftCrop = syncedCrop; }
        }

        refreshing = true;
        ShowSheet(farm);
        ShowSettings(farm);
        ShowForecast(farm);
        ShowActions(farm);
        ShowCurrent();
        ShowController(farm);
        ShowLast();
        refreshing = false;
    }

    // ---------- sheet ----------

    private void ShowSheet(Farm farm)
    {
        if (fertilityValue != null) fertilityValue.text = field.Fertility + "%";
        if (moistureValue != null) moistureValue.text = field.Moisture + "%";
        if (statusLabel != null) statusLabel.text = field.Farming ? "Crop" : "Land";
        if (statusValue != null) statusValue.text = Status(farm);
    }

    private string Status(Farm farm)
    {
        if (field.ForSale) return "For sale · " + field.PurchasePrice + " coins";
        if (!field.Owned) return "Not your land yet";
        if (field.LandUse == FarmField.Use.Depot) return "Equipment depot";
        if (!field.Farming) return field.Arable ? "Empty farmland" : "Building land only";

        switch (field.State)
        {
            case FarmField.Status.Running:
                string time = Mathf.CeilToInt(field.SecondsLeftInStage) + "s";
                string what = field.Active.kind == CropKind.Recovery ? "Restoring soil" : "Producing";
                string stage = field.CurrentStage == FarmField.Stage.Sowing ? "sowing"
                    : field.CurrentStage == FarmField.Stage.Growing ? "growing"
                    : field.Active.kind == CropKind.Recovery ? "ploughing in" : "harvesting";
                if (field.PendingClear) return "Last crop, then clearing · " + time;
                return what + " · " + stage + " · " + time;
            case FarmField.Status.Paused:
                switch (field.Pause)
                {
                    case PauseReason.SoilRestored: return "Soil restored · choose cabbage";
                    case PauseReason.Unprofitable: return "Paused · plan would lose money";
                    case PauseReason.CannotProfit: return "Paused · plan cannot profit";
                    case PauseReason.NoFunds: return "Paused · not enough coins";
                    case PauseReason.NoEquipment: return "Paused · no equipment";
                    default: return "Paused · choose a density";
                }
            default:
                return "Ready · press Start in Farm plan";
        }
    }

    // ---------- settings ----------

    private void ShowSettings(Farm farm)
    {
        bool controller = field.ControllerInstalled && field.ControllerEnabled;
        bool restoring = draftCrop == CropKind.Recovery && !controller;

        ShowChoice(cashButton, draftCrop == CropKind.Cash, !controller);
        ShowChoice(recoveryButton, draftCrop == CropKind.Recovery, !controller);
        if (cropNote != null)
        {
            if (controller) cropNote.text = "The soil controller picks the crop: it restores below " + farm.Rules.RestoreBelow
                + "% and returns to cabbage above " + farm.Rules.ReturnAbove + "%. These are its cabbage settings.";
            else if (restoring) cropNote.text = "Green beans use a fixed recipe: no seed cost, no fertilizer or water, no sale. They put "
                + farm.Rules.RestorationGain + " points back into the soil. Your cabbage settings below are kept for later.";
            else cropNote.text = "Cabbage sells; each crop uses some soil fertility.";
        }

        ShowRow(density, draft.density, !restoring);
        ShowRow(fertilizer, draft.fertilizer, !restoring);
        ShowRow(watering, draft.watering, !restoring);
    }

    private void ShowChoice(Button button, bool chosen, bool usable)
    {
        if (button == null) return;
        button.interactable = usable;
        if (button.image != null) button.image.sprite = chosen ? chosenSprite : otherSprite;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = Faded(chosen ? chosenText : otherText, usable);
    }

    // The buttons swap sprites rather than tint, so a disabled one would look the same as an
    // enabled one; fading its label is what tells the two apart.
    private static void SetUsable(Button button, bool usable)
    {
        if (button == null) return;
        button.interactable = usable;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) { Color c = label.color; c.a = usable ? 1f : 0.4f; label.color = c; }
    }

    private static Color Faded(Color c, bool usable) { c.a = usable ? 1f : 0.55f; return c; }

    private void ShowRow(SettingRow row, int percent, bool usable)
    {
        if (row == null) return;
        if (row.slider != null)
        {
            row.slider.SetValueWithoutNotify(percent / FarmPlan.Step);
            row.slider.interactable = usable;
        }
        if (row.value != null) row.value.text = percent + "%";
    }

    // ---------- forecast ----------

    private void ShowForecast(Farm farm)
    {
        int startSoil = farm.NextStartFertility(field);
        Farm.Decision d = farm.Decide(field, draft, draftCrop, startSoil);
        CropForecast f = d.forecast;

        if (forecastTitle != null) forecastTitle.text = field.Running ? "Next crop forecast" : "Forecast before Start";

        if (forecastLines != null)
        {
            string crop = "<b>" + (d.kind == CropKind.Recovery ? "Green beans (restore soil)" : "Cabbage") + "</b>"
                + (d.byController ? " · chosen by the soil controller" : string.Empty) + "\n";
            if (d.kind == CropKind.Recovery)
                forecastLines.text = crop +
                    "Upfront cost<pos=62%>" + f.Cost + "\n" +
                    "Sale<pos=62%>none\n" +
                    "<b>Soil<pos=62%>" + f.startFertility + "% -> " + f.endFertility + "% (" + Farm.Signed(f.FertilityChange) + ")</b>";
            else
                forecastLines.text = crop +
                    "Seed · fertilizer · water<pos=62%>-" + f.seedCost + " · -" + f.fertilizerCost + " · -" + f.waterCost + "\n" +
                    "Upfront cost<pos=62%>" + f.Cost + "\n" +
                    "Expected sale<pos=62%>+" + f.income + "\n" +
                    "<b>Expected net<pos=62%>" + Farm.Signed(f.Net) + "</b>\n" +
                    "Soil<pos=62%>" + f.startFertility + "% -> " + f.endFertility + "% (" + Farm.Signed(f.FertilityChange) + ")";
        }

        if (forecastWarning != null)
        {
            string warning = null;
            if (d.reason != PauseReason.None) warning = farm.ReasonText(field, d.reason, d);
            else if (!field.Running) warning = farm.WhyCannotStart(field, draft, draftCrop);
            else if (d.kind == CropKind.Cash && f.Cost + farm.ReserveOfOthers(field) > farm.Coins + (field.Active.kind == CropKind.Cash ? field.Active.income : 0))
                warning = "Coins may be short for this plan when the next crop starts.";
            forecastWarning.text = warning ?? string.Empty;
        }
    }

    // ---------- actions ----------

    private void ShowActions(Farm farm)
    {
        if (startButton != null)
        {
            if (field.Running)
            {
                SetUsable(startButton, Edited && field.Farming);
                if (startLabel != null) startLabel.text = "Apply next crop";
            }
            else
            {
                SetUsable(startButton, farm.WhyCannotStart(field, draft, draftCrop) == null);
                if (startLabel != null) startLabel.text = !field.Farming ? "Start farming" : field.State == FarmField.Status.Paused ? "Resume" : "Start";
            }
        }
        SetUsable(undoButton, Edited);
        if (repeatToggle != null)
        {
            if (repeatToggle.gameObject.activeSelf != field.Farming) repeatToggle.gameObject.SetActive(field.Farming);
            repeatToggle.SetIsOnWithoutNotify(field.Repeat);
        }
    }

    private void ShowCurrent()
    {
        bool show = field.Running && field.Active != null;
        if (currentBox != null && currentBox.activeSelf != show) currentBox.SetActive(show);
        if (!show || currentLines == null) return;
        CropForecast a = field.Active;
        string crop = a.kind == CropKind.Recovery ? "Green beans" : "Cabbage " + a.plan.density + "/" + a.plan.fertilizer + "/" + a.plan.watering + "%";
        string body = a.kind == CropKind.Recovery
            ? "Paid " + a.Cost + " · no sale · soil -> " + a.endFertility + "%"
            : a.legacy
                ? "Paid " + a.Cost + " · expected sale +" + a.income
                : "Paid " + a.Cost + " · expected sale +" + a.income + " (net " + Farm.Signed(a.Net) + ") · soil -> " + a.endFertility + "%";
        currentLines.text = "<b>Growing now:</b> " + crop + (a.automatic ? " (soil controller)" : string.Empty) + "\n" + body;
    }

    // ---------- soil controller ----------

    private void ShowController(Farm farm)
    {
        bool show = farm.ControllersUnlocked && field.Owned && field.Arable;
        if (controllerRow != null && controllerRow.activeSelf != show) controllerRow.SetActive(show);
        if (!show) return;

        bool installed = field.ControllerInstalled;
        if (installButton != null)
        {
            if (installButton.gameObject.activeSelf == installed) installButton.gameObject.SetActive(!installed);
            SetUsable(installButton, farm.WhyCannotInstall(field) == null);
        }
        if (installLabel != null)
            installLabel.text = farm.InstallCredits > 0 ? "Install · free kit" : "Install · " + farm.Rules.ControllerInstallPrice + " coins";
        if (autoToggle != null)
        {
            if (autoToggle.gameObject.activeSelf != installed) autoToggle.gameObject.SetActive(installed);
            autoToggle.SetIsOnWithoutNotify(field.ControllerEnabled);
        }
        if (controllerNote != null)
        {
            if (!installed)
            {
                string why = farm.WhyCannotInstall(field);
                controllerNote.text = "Soil controller: restores below " + farm.Rules.RestoreBelow + "%, back to cabbage above "
                    + farm.Rules.ReturnAbove + "%." + (why != null ? "\n<color=#8A3D3B>" + why + "</color>" : string.Empty);
            }
            else if (!field.ControllerEnabled) controllerNote.text = "Soil controller installed, switched off. You choose the crop by hand.";
            else controllerNote.text = field.ControllerRecovering
                ? "Soil controller: restoring. Cabbage returns above " + farm.Rules.ReturnAbove + "% when it pays."
                : "Soil controller: growing cabbage. Restores below " + farm.Rules.RestoreBelow + "% or when cabbage would not pay.";
        }
    }

    // ---------- history ----------

    private void ShowLast()
    {
        if (lastLines == null) return;
        CropForecast r = field.Last;
        if (r == null) { lastLines.text = "<b>Last completed crop</b>\nNone yet."; return; }
        if (r.kind == CropKind.Recovery)
            lastLines.text = "<b>Last completed crop: green beans</b>\nNo sale · soil " + r.startFertility + "% -> " + r.endFertility + "% (" + Farm.Signed(r.FertilityChange) + ")";
        else
            lastLines.text = "<b>Last completed crop: cabbage</b>\nSold +" + r.income + " · seed -" + r.seedCost + " · fertilizer -" + r.fertilizerCost
                + " · water -" + r.waterCost + " · <b>net " + Farm.Signed(r.Net) + "</b>"
                + (r.legacy ? string.Empty : "\nSoil " + r.startFertility + "% -> " + r.endFertility + "%");
    }
}
