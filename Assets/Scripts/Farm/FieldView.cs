using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro;

/// <summary>
/// Shows a field's crop cycle on the map: the seeder sowing, the crop growing, the drone and
/// irrigation working, the harvest filling crates, the sale. It only reads
/// <see cref="FarmField"/> and <see cref="FarmRules"/>; nothing here moves money or time.
///
/// Why a script and not an Animator: every frame's picture depends on game state - how far
/// the crop is, which settings the paid-for plan has, how big the harvest will be - and on
/// the parcel's own size. The machines' own looping motion (the drone's rotors) is still an
/// Animator; this script only decides where things are and which cells show which stage.
///
/// The machines live under the parcel's Grid so they rise with it when the parcel is picked.
/// Crop cells change through <see cref="Parcel.SetCropTile"/>, which keeps the pick's tint.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(FarmField))]
public class FieldView : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private FarmRules rules;
    [SerializeField] private Farm farm;
    [Tooltip("Asks whether this field is the one the introduction wants tapped.")]
    [SerializeField] private FarmIntro intro;

    [Header("Equipment")]
    [Tooltip("All machines of this field. Shown only while an equipment set is assigned to it.")]
    [SerializeField] private GameObject machines;
    [Tooltip("The depot building, if this land can hold it. Shown while the depot stands here.")]
    [SerializeField] private GameObject depotBuilding;
    [Tooltip("The depot's own set, parked beside it. Shown only while that set is not working a field.")]
    [SerializeField] private GameObject depotParkedSet;
    [Tooltip("Bobs above the field while the introduction asks the player to tap it.")]
    [SerializeField] private Transform hintMarker;

    [Header("Crop stages")]
    [SerializeField] private TileBase seeded;
    [SerializeField] private TileBase young;
    [SerializeField] private TileBase growing;
    [Tooltip("The full crop. Cells that the plan could not fully support stay one stage smaller.")]
    [SerializeField] private TileBase ready;

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

    [Header("Harvest")]
    [Tooltip("Stacked in order as the harvest comes in; how many show depends on the harvest's size.")]
    [SerializeField] private SpriteRenderer[] crates;
    [SerializeField] private int coinsPerCrate = 10;
    [SerializeField] private TMP_Text saleText;
    [SerializeField] private float saleTextSeconds = 3f;

    [Header("When the machines work, as parts of the growing time")]
    [SerializeField] private Vector2 droneWindow = new Vector2(0.05f, 0.45f);
    [SerializeField] private Vector2 waterWindow = new Vector2(0.5f, 0.9f);

    [Header("Care intensity: particles per second for Off, Moderate, High")]
    [SerializeField] private float[] fertilizerRates = { 0f, 45f, 100f };
    [SerializeField] private float[] waterRates = { 0f, 40f, 90f };

    private static readonly int Flying = Animator.StringToHash("Flying");

    private FarmField field;
    private Parcel parcel;
    private Tilemap soil;
    private RectInt cells;
    private TileBase[] shown;
    private Vector3 seederHome, droneHome;
    // -1 until the first frame, which adopts the current count silently: a crop sold before
    // a save was loaded is not a new sale.
    private int soldSeen = -1;
    private float saleTimer;
    private Vector3 saleStart;
    private Vector3 hintStart;

    private void Awake()
    {
        field = GetComponent<FarmField>();
        parcel = GetComponent<Parcel>();
        Transform grid = transform.Find("Grid");
        soil = grid != null ? grid.Find("Field").GetComponent<Tilemap>() : null;
        cells = parcel.Cells;
        shown = new TileBase[cells.width * cells.height];
        if (seeder != null) seederHome = seeder.transform.localPosition;
        if (drone != null) droneHome = drone.localPosition;
        if (saleText != null) { saleStart = saleText.transform.localPosition; saleText.gameObject.SetActive(false); }
        if (hintMarker != null) hintStart = hintMarker.localPosition;
    }

    private void Start()
    {
        // The authored scene shows a fallow field; start the picture from the same place.
        parcel.FillCrop(null);
    }

    private void Update()
    {
        if (rules == null || soil == null) return;

        ShowHint();
        SetShown(machines, field.AssignedSet > 0);
        bool depotHere = field.LandUse == FarmField.Use.Depot;
        SetShown(depotBuilding, depotHere);
        SetShown(depotParkedSet, depotHere && farm != null && farm.FieldUsingSet(farm.DepotSet) == null);

        FarmField.Stage stage = field.CurrentStage(rules);
        float progress = field.StageProgress(rules);
        float quality = Quality();

        PaintCrop(stage, progress, quality);
        MoveSeeder(stage, progress);
        MoveDrone(stage, progress);
        RunIrrigation(stage, progress);
        ShowCrates(stage, progress);
        ShowSale();
    }

    // Share of the potential harvest this crop will reach: 1 when the land plus the machines
    // satisfy the density, less when they do not. Drawn as full-size versus stunted cells.
    private float Quality()
    {
        if (!field.Running) return 1f;
        HarvestResult r = rules.Evaluate(field.ActivePlan, field.Fertility, field.Moisture);
        HarvestResult best = rules.Evaluate(field.ActivePlan, 1f, 1f);
        return best.income > 0 ? (float)r.income / best.income : 1f;
    }

    // ---------- crop ----------

    private void PaintCrop(FarmField.Stage stage, float progress, float quality)
    {
        int count = cells.width * cells.height;
        for (int i = 0; i < count; i++)
        {
            Vector2Int c = PathCell(i);
            TileBase wanted = null;
            float here = (i + 0.5f) / count;

            switch (stage)
            {
                case FarmField.Stage.Sowing:
                    if (progress >= here) wanted = seeded;
                    break;
                case FarmField.Stage.Growing:
                    if (progress < 0.25f) wanted = seeded;
                    else if (progress < 0.5f) wanted = young;
                    else if (progress < 0.8f || !Thrives(c, quality)) wanted = growing;
                    else wanted = ready;
                    break;
                case FarmField.Stage.Harvesting:
                    if (progress < here) wanted = Thrives(c, quality) ? ready : growing;
                    break;
            }

            if (shown[i] == wanted) continue;
            shown[i] = wanted;
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
        float rows = cells.height;
        float f = Mathf.Clamp01(t) * rows;
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

    private void MoveDrone(FarmField.Stage stage, float progress)
    {
        if (drone == null) return;
        bool working = stage == FarmField.Stage.Growing && field.ActivePlan.fertilizer != Care.Off
            && progress >= droneWindow.x && progress <= droneWindow.y;

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
        Spray(fertilizerSpray, working && height > flightHeight * 0.9f, fertilizerRates, field.ActivePlan.fertilizer);
    }

    private void RunIrrigation(FarmField.Stage stage, float progress)
    {
        bool working = stage == FarmField.Stage.Growing && field.ActivePlan.watering != Care.Off
            && progress >= waterWindow.x && progress <= waterWindow.y;
        Spray(waterSpray, working, waterRates, field.ActivePlan.watering);
    }

    private static void Spray(ParticleSystem system, bool on, float[] rates, Care care)
    {
        if (system == null) return;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = on && (int)care < rates.Length ? rates[(int)care] : 0f;
        if (!system.isPlaying) system.Play();
    }

    private static void SetShown(GameObject target, bool shown)
    {
        if (target != null && target.activeSelf != shown) target.SetActive(shown);
    }

    private void ShowHint()
    {
        if (hintMarker == null) return;
        bool wanted = intro != null && intro.Target == field;
        if (hintMarker.gameObject.activeSelf != wanted) hintMarker.gameObject.SetActive(wanted);
        if (wanted) hintMarker.localPosition = hintStart + Vector3.up * (0.12f * Mathf.Sin(Time.time * 4f));
    }

    // ---------- harvest and sale ----------

    private void ShowCrates(FarmField.Stage stage, float progress)
    {
        if (crates == null) return;
        int visible = 0;
        if (stage == FarmField.Stage.Harvesting)
        {
            HarvestResult r = rules.Evaluate(field.ActivePlan, field.Fertility, field.Moisture);
            int total = Mathf.Clamp(Mathf.CeilToInt(r.income / (float)Mathf.Max(1, coinsPerCrate)), 1, crates.Length);
            visible = Mathf.CeilToInt(total * progress);
        }
        for (int i = 0; i < crates.Length; i++)
            if (crates[i] != null) crates[i].enabled = i < visible;
    }

    private void ShowSale()
    {
        if (saleText == null) return;
        if (soldSeen < 0) soldSeen = field.CropsSold;
        if (field.CropsSold != soldSeen)
        {
            soldSeen = field.CropsSold;
            HarvestResult r = field.LastResult;
            saleText.text = "+" + r.income + " sold\n<size=70%>net " + (r.Net >= 0 ? "+" : "") + r.Net + "</size>";
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
}
