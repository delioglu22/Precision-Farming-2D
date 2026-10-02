using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro;

/// <summary>
/// Shows a field's crop cycle on the map: sowing, growth, the drone and irrigation at work,
/// the cabbage harvest filling crates or the green beans being ploughed back in, the result,
/// an installed soil controller and a warning when the field needs the player. It only reads
/// <see cref="FarmField"/> and <see cref="Farm"/>; nothing here moves money, soil or time.
///
/// Why a script and not an Animator: every frame's picture depends on game state - how far
/// the paid crop is, which crop and settings it was paid with, how big the harvest will be -
/// and on the parcel's own size. The drone's rotors are still an Animator; this script only
/// decides where things are and which cells show which stage.
///
/// Everything drawn comes from the crop's snapshot (<see cref="FarmField.Active"/>), so a plan
/// changed mid-crop never changes the crop already growing. Machines live under the parcel's
/// Grid so they rise with it when the parcel is picked. Crop cells change through
/// <see cref="Parcel.SetCropTile"/>, which keeps the pick's tint.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(FarmField))]
public class FieldView : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private FarmRules rules;
    [SerializeField] private Farm farm;
    [Tooltip("Asks whether this field is the one the introduction is talking about.")]
    [SerializeField] private FarmIntro intro;

    [Header("Equipment")]
    [Tooltip("All machines of this field. Shown only while an equipment set is assigned to it.")]
    [SerializeField] private GameObject machines;
    [Tooltip("The depot building, if this land can hold it. Shown while the depot stands here.")]
    [SerializeField] private GameObject depotBuilding;
    [Tooltip("The depot's own set, parked beside it. Shown only while that set is not working a field.")]
    [SerializeField] private GameObject depotParkedSet;

    [Header("Markers")]
    [Tooltip("Bobs above the field while the introduction is about it.")]
    [SerializeField] private Transform hintMarker;
    [Tooltip("A red exclamation shown while the field is stopped by a problem the player must solve.")]
    [SerializeField] private Transform attentionMarker;
    [Tooltip("The installed soil sensor and control module.")]
    [SerializeField] private GameObject controller;
    [Tooltip("The module's small light: green while it works, amber while it restores, grey when switched off.")]
    [SerializeField] private SpriteRenderer controllerLight;

    [Header("Cabbage stages")]
    [SerializeField] private TileBase seeded;
    [SerializeField] private TileBase young;
    [SerializeField] private TileBase growing;
    [Tooltip("The full crop. Cells the plan could not fully support stay one stage smaller.")]
    [SerializeField] private TileBase ready;

    [Header("Green-bean restoration stages")]
    [SerializeField] private TileBase beanSeeded;
    [SerializeField] private TileBase beanYoung;
    [SerializeField] private TileBase beanGrown;
    [Tooltip("Beans cut and worked into the soil, shown cell by cell before the field clears.")]
    [SerializeField] private TileBase beanMulch;

    [Header("Seeder")]
    [SerializeField] private SpriteRenderer seeder;
    [Tooltip("Facing +x, +y, -x, -y in cell terms (up-right, up-left, down-left, down-right on screen).")]
    [SerializeField] private Sprite[] seederFacings = new Sprite[4];

    [Header("Drone")]
    [SerializeField] private Transform drone;
    [SerializeField] private Animator droneAnimator;
    [SerializeField] private Transform droneShadow;
    [SerializeField] private ParticleSystem fertilizerSpray;
    [SerializeField] private float flightHeight = 1.1f;

    [Header("Irrigation")]
    [SerializeField] private ParticleSystem waterSpray;

    [Header("Harvest and result")]
    [Tooltip("Stacked in order as the cabbage harvest comes in; how many show depends on its size.")]
    [SerializeField] private SpriteRenderer[] crates;
    [SerializeField] private int coinsPerCrate = 10;
    [SerializeField] private TMP_Text saleText;
    [SerializeField] private float saleTextSeconds = 3f;

    [Header("When the machines work, as parts of the growing time")]
    [SerializeField] private Vector2 droneWindow = new Vector2(0.05f, 0.45f);
    [SerializeField] private Vector2 waterWindow = new Vector2(0.5f, 0.9f);

    [Header("Particles per second at a 0, 50 and 100% dose")]
    [SerializeField] private float[] fertilizerRates = { 0f, 45f, 100f };
    [SerializeField] private float[] waterRates = { 0f, 40f, 90f };

    private static readonly int Flying = Animator.StringToHash("Flying");

    private FarmField field;
    private Parcel parcel;
    private Tilemap soil;
    private RectInt cells;
    private Vector3 seederHome, droneHome;
    // -1 until the first frame, which adopts the current count silently: a crop finished
    // before a save was loaded is not a new result to announce.
    private int finishedSeen = -1;
    private float saleTimer;
    private Vector3 saleStart;
    private Vector3 hintStart, attentionStart;

    private void Awake()
    {
        field = GetComponent<FarmField>();
        parcel = GetComponent<Parcel>();
        Transform grid = transform.Find("Grid");
        soil = grid != null ? grid.Find("Field").GetComponent<Tilemap>() : null;
        cells = parcel.Cells;
        if (seeder != null) seederHome = seeder.transform.localPosition;
        if (drone != null) droneHome = drone.localPosition;
        if (saleText != null) { saleStart = saleText.transform.localPosition; saleText.gameObject.SetActive(false); }
        if (hintMarker != null) hintStart = hintMarker.localPosition;
        if (attentionMarker != null) attentionStart = attentionMarker.localPosition;
    }

    private void Update()
    {
        if (rules == null || soil == null) return;

        ShowMarkers();
        SetShown(machines, field.AssignedSet > 0);
        bool depotHere = field.LandUse == FarmField.Use.Depot;
        SetShown(depotBuilding, depotHere);
        SetShown(depotParkedSet, depotHere && farm != null && farm.FieldUsingSet(farm.DepotSet) == null);
        ShowController();

        CropForecast crop = field.Running ? field.Active : null;
        FarmField.Stage stage = field.CurrentStage;
        float progress = field.StageProgress;

        PaintCrop(crop, stage, progress);
        MoveSeeder(stage, progress);
        MoveDrone(crop, stage, progress);
        RunIrrigation(crop, stage, progress);
        ShowCrates(crop, stage, progress);
        ShowResult();
    }

    private static void SetShown(GameObject target, bool shown)
    {
        if (target != null && target.activeSelf != shown) target.SetActive(shown);
    }

    // ---------- crop ----------

    // Every frame asks the parcel for the wanted tile in every cell. The parcel skips cells
    // that already show it, so this is cheap, and it repairs anything that repainted the layer
    // (the parcel's own rebuild when Play starts) without this script having to notice.
    private void PaintCrop(CropForecast crop, FarmField.Stage stage, float progress)
    {
        int count = cells.width * cells.height;
        bool beans = crop != null && crop.kind == CropKind.Recovery;
        float quality = crop != null && crop.fullIncome > 0 ? (float)crop.income / crop.fullIncome : 1f;

        for (int i = 0; i < count; i++)
        {
            Vector2Int c = PathCell(i);
            float here = (i + 0.5f) / count;
            TileBase wanted = null;

            switch (stage)
            {
                case FarmField.Stage.Sowing:
                    if (progress >= here) wanted = beans ? beanSeeded : seeded;
                    break;
                case FarmField.Stage.Growing:
                    if (beans) wanted = progress < 0.35f ? beanSeeded : progress < 0.7f ? beanYoung : beanGrown;
                    else if (progress < 0.25f) wanted = seeded;
                    else if (progress < 0.5f) wanted = young;
                    else if (progress < 0.8f || !Thrives(c, quality)) wanted = growing;
                    else wanted = ready;
                    break;
                case FarmField.Stage.Finishing:
                    if (beans) wanted = progress < here ? beanGrown : (progress < 0.85f ? beanMulch : null);
                    else if (progress < here) wanted = Thrives(c, quality) ? ready : growing;
                    break;
            }
            parcel.SetCropTile(new Vector3Int(c.x, c.y, 0), wanted);
        }
    }

    // A fixed, scattered choice of which cells reach full size, so the same plan always
    // draws the same field and a better plan fills in more of it.
    private static bool Thrives(Vector2Int c, float quality)
    {
        int h = (c.x * 73856093) ^ (c.y * 19349663);
        float roll = ((h & 0x7fffffff) % 1000) / 1000f;
        return roll < quality - 0.0005f || quality >= 0.999f;
    }

    // Cells in the order the machines visit them: row by row along the parcel's x axis,
    // turning back at each end like a real field pass.
    private Vector2Int PathCell(int index)
    {
        int row = index / cells.width;
        int along = index % cells.width;
        if (row % 2 == 1) along = cells.width - 1 - along;
        return new Vector2Int(cells.xMin + along, cells.yMin + row);
    }

    // A smooth point along that same path, 0 to 1, in the Grid's local space.
    private Vector3 PathPoint(float t, out int facing)
    {
        float f = Mathf.Clamp01(t) * cells.height;
        int row = Mathf.Min((int)f, cells.height - 1);
        float u = f - row;
        bool back = row % 2 == 1;
        float x = back ? Mathf.Lerp(cells.xMax - 0.5f, cells.xMin + 0.5f, u) : Mathf.Lerp(cells.xMin + 0.5f, cells.xMax - 0.5f, u);
        facing = back ? 2 : 0;
        return soil.CellToLocalInterpolated(new Vector3(x, cells.yMin + row + 0.5f, 0f));
    }

    // ---------- machines ----------

    private void MoveSeeder(FarmField.Stage stage, float progress)
    {
        if (seeder == null) return;
        int facing = 3;
        Vector3 at = seederHome;
        if (stage == FarmField.Stage.Sowing) at = PathPoint(progress, out facing);
        seeder.transform.localPosition = at;
        if (facing < seederFacings.Length && seederFacings[facing] != null) seeder.sprite = seederFacings[facing];
    }

    private void MoveDrone(CropForecast crop, FarmField.Stage stage, float progress)
    {
        if (drone == null) return;
        int dose = crop != null && crop.kind == CropKind.Cash ? crop.plan.fertilizer : 0;
        bool working = stage == FarmField.Stage.Growing && dose > 0 && progress >= droneWindow.x && progress <= droneWindow.y;

        Vector3 ground = droneHome;
        float height = 0f;
        if (working)
        {
            float t = Mathf.InverseLerp(droneWindow.x, droneWindow.y, progress);
            int unused;
            ground = PathPoint(t, out unused);
            // Rise at the start and settle at the end instead of teleporting into the air.
            height = flightHeight * Mathf.Clamp01(Mathf.Min(t, 1f - t) * 12f);
        }
        drone.localPosition = ground + Vector3.up * height;
        if (droneShadow != null) droneShadow.localPosition = ground;
        // A hidden field (no equipment set) has an inactive Animator, which warns if told anything.
        if (droneAnimator != null && droneAnimator.isActiveAndEnabled) droneAnimator.SetBool(Flying, working);
        Spray(fertilizerSpray, working && height > flightHeight * 0.9f, fertilizerRates, dose);
    }

    private void RunIrrigation(CropForecast crop, FarmField.Stage stage, float progress)
    {
        int dose = crop != null && crop.kind == CropKind.Cash ? crop.plan.watering : 0;
        bool working = stage == FarmField.Stage.Growing && dose > 0 && progress >= waterWindow.x && progress <= waterWindow.y;
        Spray(waterSpray, working, waterRates, dose);
    }

    // The rates are given at 0, 50 and 100 percent; a dose in between blends the two nearest.
    private static void Spray(ParticleSystem system, bool on, float[] rates, int dose)
    {
        if (system == null) return;
        float rate = 0f;
        if (on && rates.Length >= 3)
            rate = dose <= 50 ? Mathf.Lerp(rates[0], rates[1], dose / 50f) : Mathf.Lerp(rates[1], rates[2], (dose - 50) / 50f);
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;
        if (!system.isPlaying) system.Play();
    }

    // ---------- harvest and result ----------

    private void ShowCrates(CropForecast crop, FarmField.Stage stage, float progress)
    {
        if (crates == null) return;
        int visible = 0;
        if (crop != null && crop.kind == CropKind.Cash && stage == FarmField.Stage.Finishing)
        {
            int total = Mathf.Clamp(Mathf.CeilToInt(crop.income / (float)Mathf.Max(1, coinsPerCrate)), 1, crates.Length);
            visible = Mathf.CeilToInt(total * progress);
        }
        for (int i = 0; i < crates.Length; i++)
            if (crates[i] != null) crates[i].enabled = i < visible;
    }

    private void ShowResult()
    {
        if (saleText == null) return;
        int finished = field.CashCompleted + field.RecoveryCompleted;
        if (finishedSeen < 0) finishedSeen = finished;
        if (finished != finishedSeen)
        {
            finishedSeen = finished;
            CropForecast r = field.Last;
            if (r != null && r.kind == CropKind.Cash)
                saleText.text = "+" + r.income + " sold\n<size=70%>net " + Farm.Signed(r.Net) + "</size>";
            else if (r != null)
                saleText.text = "<color=#B9E07A>Soil " + Farm.Signed(r.FertilityChange) + "</color>\n<size=70%>now " + r.endFertility + "%</size>";
            saleTimer = saleTextSeconds;
            saleText.gameObject.SetActive(true);
        }
        if (saleTimer <= 0f) return;

        saleTimer -= Time.deltaTime;
        float t = 1f - saleTimer / saleTextSeconds;
        saleText.transform.localPosition = saleStart + Vector3.up * (0.6f * t);
        saleText.alpha = t < 0.75f ? 1f : Mathf.InverseLerp(1f, 0.75f, t);
        if (saleTimer <= 0f) saleText.gameObject.SetActive(false);
    }

    // ---------- markers and the controller ----------

    private void ShowMarkers()
    {
        float bob = 0.12f * Mathf.Sin(Time.time * 4f);
        bool hint = intro != null && intro.Target == field;
        bool alarm = field.State == FarmField.Status.Paused && Farm.IsAlarm(field.Pause);
        if (hintMarker != null)
        {
            // The alarm takes the spot above the field when both would show.
            SetShown(hintMarker.gameObject, hint && !alarm);
            if (hint && !alarm) hintMarker.localPosition = hintStart + Vector3.up * bob;
        }
        if (attentionMarker != null)
        {
            SetShown(attentionMarker.gameObject, alarm);
            if (alarm) attentionMarker.localPosition = attentionStart + Vector3.up * bob;
        }
    }

    private void ShowController()
    {
        bool installed = field.ControllerInstalled && field.Owned && field.LandUse != FarmField.Use.Depot;
        SetShown(controller, installed);
        if (!installed || controllerLight == null) return;

        Color off = new Color(0.45f, 0.45f, 0.45f);
        Color working = new Color(0.55f, 0.9f, 0.35f);
        Color restoring = new Color(0.95f, 0.75f, 0.25f);
        if (!field.ControllerActive) { controllerLight.color = off; return; }
        Color on = field.ControllerRecovering ? restoring : working;
        // A slow pulse says "this is running" without drawing the eye like the alarm does.
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.5f);
        controllerLight.color = new Color(on.r * pulse, on.g * pulse, on.b * pulse, 1f);
    }
}
