using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The seeder's playfield: the parcel it was sent to, seen straight down, and the one
/// unbroken line the player drives over it.
///
/// The map draws a parcel in isometric, but the field itself is the rectangle of cells it
/// always was, and the rectangle is what the mini game hands the player. A diamond would
/// spend half the screen on dead corners.
///
/// The field is a texture rather than a mesh because this mini game is scored on how much of
/// the parcel got seed. A grid of texels answers that exactly - covered, missed, seeded twice
/// and thrown over the fence are all just counts - and it is the same grid the player is
/// looking at, so one structure draws the picture and keeps the score. A mesh band would look
/// crisper and would still have to be rasterised before it could be scored, which is two
/// systems where one does.
///
/// Unity does the rest. The drag arrives from the EventSystem through the canvas raycaster,
/// which means no polling, no drag threshold of our own and no "did this land on the UI"
/// check. A RawImage takes a Texture straight off where an Image would want a Sprite, and an
/// AspectRatioFitter holds the parcel's proportions inside whatever room the screen leaves.
/// </summary>
[RequireComponent(typeof(RawImage))]
[DisallowMultipleComponent]
public class SeederField : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("The run this scene is playing. Everything the field needs is in here, which is what lets this scene be opened and played on its own.")]
    [SerializeField] SeederRun run;

    [Tooltip("Where the ground is shown. A RawImage, because the texture is made at runtime.")]
    [SerializeField] RawImage ground;

    [Tooltip("Holds the parcel's proportions whatever room the screen leaves it.")]
    [SerializeField] AspectRatioFitter fitter;

    [Tooltip("Names the field being worked. Optional.")]
    [SerializeField] Text title;

    [Tooltip("Texels along the parcel's longer side. The grain of the score as much as of the picture.")]
    [SerializeField, Range(64, 512)] int resolution = 256;

    [Tooltip("Bare ground ringing the parcel, as a fraction of its longer side. Seed thrown over the fence lands here.")]
    [SerializeField, Range(0f, 0.5f)] float margin = 0.12f;

    [Tooltip("How wide a band the machine sows, in grid cells. The seeder's own measurement.")]
    [SerializeField, Range(0.2f, 4f)] float bandCells = 0.9f;

    // Measured rather than guessed: driving a perfect boustrophedon at a 0.9 cell band costs
    // 18 cells of line on a 3x4, 28 on a 4x5, 46 on a 5x7 and 78 on a 9x7. Forty-eight puts
    // the commonest footprint on the map about two cells short of comfortable - a clean route
    // reaches the whole field, a wasteful one does not - and leaves the big parcels out of
    // reach until a bigger machine is bought.
    [Tooltip("How much line the machine carries, in grid cells. A machine stat and not a parcel one: this is what decides which fields it can finish at all.")]
    [SerializeField, Min(1f)] float batteryCells = 48f;

    [Tooltip("What is left in the tank. A Filled image, so the length of line remaining is something seen rather than read.")]
    [SerializeField] Image battery;

    // All four are measured out of docs/art.md rather than picked by eye, so the field sits in
    // the same palette as the map it was opened from. The texture is sRGB, which is what
    // Unity samples a RGBA32 into, so these go in as written and not as .linear.

    [Tooltip("Ground inside the fence, waiting for seed. The plowed earth of art.md.")]
    [SerializeField] Color soil = new Color(0.455f, 0.353f, 0.235f, 1f);

    [Tooltip("The verge outside the fence, in the shadowed earth of a slab's left face. Seed laid there is wasted, so it reads as ground you do not own.")]
    [SerializeField] Color verge = new Color(0.251f, 0.180f, 0.122f, 1f);

    [Tooltip("Ground that got seed and will grow. The grass green of art.md.")]
    [SerializeField] Color sown = new Color(0.486f, 0.663f, 0.333f, 1f);

    [Tooltip("Seed that landed over the fence: the same green gone dull, because it grows but not for you.")]
    [SerializeField] Color spilled = new Color(0.259f, 0.388f, 0.184f, 1f);

    Texture2D field;
    Color32[] pixels;

    // Which texels fall inside the fence, and how many of them there are: the denominator
    // every run is scored against, worked out once when the field is built.
    bool[] fenced;
    int fencedCount;

    // Which texels have had seed on them. Set once and never cleared during a run, so driving
    // back over ground already sown adds nothing - it only costs.
    bool[] seeded;
    int sownCount;

    int width;
    int height;
    float texelsPerCell;

    // The stroke is unbroken by rule: lifting a finger ends the run rather than pausing it.
    bool driving;
    bool spent;
    Vector2 lastTexel;

    // What is left of the tank, in cells of line. Distance is distance: driving over the
    // verge, or back over ground already sown, costs exactly what driving over bare soil does.
    float lineLeft;

    /// <summary>The share of the parcel that has seed on it, from 0 to 1.</summary>
    public float Coverage
    {
        get { return fencedCount <= 0 ? 0f : (float)sownCount / fencedCount; }
    }

    /// <summary>Cells of line still in the tank.</summary>
    public float LineLeft { get { return lineLeft; } }

    /// <summary>Whether the run is over, by a lifted finger or by a dry tank.</summary>
    public bool Spent { get { return spent; } }

    // The scene stands on its own, so it lays its field out as soon as it opens rather than
    // waiting to be told to by the map.
    void Start()
    {
        Begin();
    }

    void OnDestroy()
    {
        Discard(field);
    }

    /// <summary>Lays the run's parcel out as ground, ready to be driven over.</summary>
    public void Begin()
    {
        if (run == null) return;

        if (title != null) title.text = run.ParcelName;
        Build(run.Footprint);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (spent || field == null) return;

        Vector2 texel;
        if (!TexelAt(eventData, out texel)) return;

        driving = true;
        lastTexel = texel;

        // Setting the machine down sows where it stands and costs nothing; line is spent on
        // travel, and it has not travelled yet.
        Stamp(texel, texel);
        Show();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!driving) return;

        Vector2 texel;
        if (!TexelAt(eventData, out texel)) return;

        Drive(texel);
    }

    /// <summary>
    /// Runs the machine out to a point, sowing the band behind it and spending a cell of line
    /// for every cell travelled. The tank empties in the middle of a stroke rather than at the
    /// end of one, so the run stops where the machine stands and not where the finger got to.
    /// A dry tank is not a failure - whatever was covered still counts.
    /// </summary>
    void Drive(Vector2 to)
    {
        if (lineLeft <= 0f || texelsPerCell <= 0f) return;

        float cells = Vector2.Distance(lastTexel, to) / texelsPerCell;
        if (cells <= 0f) return;

        Vector2 reached = to;
        if (cells > lineLeft)
        {
            reached = Vector2.Lerp(lastTexel, to, lineLeft / cells);
            cells = lineLeft;
        }

        Stamp(lastTexel, reached);
        lastTexel = reached;

        lineLeft -= cells;
        if (lineLeft <= 0f)
        {
            lineLeft = 0f;
            driving = false;
            spent = true;
        }

        Show();
        ShowBattery();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!driving) return;

        // One unbroken line: letting go is the end of the run, not a pause in it.
        driving = false;
        spent = true;
    }

    /// <summary>
    /// Where a pointer is on the field, in texels. The rect is the whole picture - parcel and
    /// verge together - so a finger over the verge lands outside the fence but still on the
    /// field, which is exactly what wasting seed looks like.
    /// </summary>
    bool TexelAt(PointerEventData eventData, out Vector2 texel)
    {
        texel = Vector2.zero;

        RectTransform rect = transform as RectTransform;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out local)) return false;

        Rect r = rect.rect;
        if (r.width <= 0f || r.height <= 0f) return false;

        texel = new Vector2(
            (local.x - r.xMin) / r.width * width,
            (local.y - r.yMin) / r.height * height);
        return true;
    }

    /// <summary>
    /// Sows the band swept between two points: every texel within half a band's width of the
    /// segment. A texel already sown is left alone, so crossing your own line adds nothing to
    /// the count - which is what makes overlap a waste rather than a wash.
    /// </summary>
    void Stamp(Vector2 from, Vector2 to)
    {
        float radius = 0.5f * bandCells * texelsPerCell;
        if (radius <= 0f) return;

        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.x, to.x) - radius));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(from.x, to.x) + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.y, to.y) - radius));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(from.y, to.y) + radius));

        Vector2 along = to - from;
        float lengthSq = along.sqrMagnitude;
        float radiusSq = radius * radius;

        Color32 sown32 = sown;
        Color32 spilled32 = spilled;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 here = new Vector2(x + 0.5f, y + 0.5f);

                // The nearest point on the segment, so the band has round ends and leaves no
                // gap between one frame's stamp and the next.
                float t = lengthSq <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(here - from, along) / lengthSq);
                if ((here - (from + along * t)).sqrMagnitude > radiusSq) continue;

                int i = y * width + x;
                if (seeded[i]) continue;

                seeded[i] = true;
                if (fenced[i]) sownCount++;
                pixels[i] = fenced[i] ? sown32 : spilled32;
            }
        }
    }

    void Show()
    {
        field.SetPixels32(pixels);
        field.Apply(false);
    }

    /// <summary>
    /// Rasterises the parcel into the field: the fence encloses its cells, and a verge of
    /// bare ground rings it so seed thrown over the fence has somewhere to land and can be
    /// seen going to waste.
    /// </summary>
    void Build(Vector2Int footprint)
    {
        if (footprint.x <= 0 || footprint.y <= 0) return;

        // Measured off the longer side, so a long thin parcel gets the same band of spare
        // ground all the way round rather than a wider one along its short side.
        float pad = margin * Mathf.Max(footprint.x, footprint.y);
        float boxWidth = footprint.x + pad * 2f;
        float boxHeight = footprint.y + pad * 2f;
        float longest = Mathf.Max(boxWidth, boxHeight);

        width = Mathf.Max(1, Mathf.RoundToInt(resolution * boxWidth / longest));
        height = Mathf.Max(1, Mathf.RoundToInt(resolution * boxHeight / longest));
        texelsPerCell = width / boxWidth;

        if (field == null || field.width != width || field.height != height)
        {
            Discard(field);
            field = new Texture2D(width, height, TextureFormat.RGBA32, false);
            field.filterMode = FilterMode.Point;
            field.wrapMode = TextureWrapMode.Clamp;
        }

        fenced = new bool[width * height];
        seeded = new bool[width * height];
        pixels = new Color32[width * height];
        fencedCount = 0;
        sownCount = 0;
        driving = false;
        spent = false;
        lineLeft = batteryCells;

        Color32 soil32 = soil;
        Color32 verge32 = verge;

        for (int y = 0; y < height; y++)
        {
            // Texel centres, so the fence falls between texels rather than along one.
            float py = boxHeight * (y + 0.5f) / height;
            bool inRows = py >= pad && py <= pad + footprint.y;

            for (int x = 0; x < width; x++)
            {
                float px = boxWidth * (x + 0.5f) / width;

                int i = y * width + x;
                bool within = inRows && px >= pad && px <= pad + footprint.x;

                fenced[i] = within;
                if (within) fencedCount++;
                pixels[i] = within ? soil32 : verge32;
            }
        }

        Show();
        ShowBattery();

        if (ground != null) ground.texture = field;
        if (fitter != null) fitter.aspectRatio = boxWidth / boxHeight;
    }

    void ShowBattery()
    {
        if (battery != null) battery.fillAmount = batteryCells <= 0f ? 0f : lineLeft / batteryCells;
    }

    /// <summary>
    /// Lets go of a texture made at runtime. Which call does that depends on whether the game
    /// is running, and keeping the field drivable from the editor is worth the branch: it is
    /// how the rasteriser gets checked without entering play mode.
    /// </summary>
    static void Discard(UnityEngine.Object doomed)
    {
        if (doomed == null) return;
        if (Application.isPlaying) Destroy(doomed);
        else DestroyImmediate(doomed);
    }
}
