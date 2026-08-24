using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The seeder's playfield: the parcel it was sent to, seen straight down, as ground to be
/// covered.
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
/// Unity does the rest: a RawImage takes a Texture straight off where an Image would want a
/// Sprite, and an AspectRatioFitter holds the parcel's proportions inside whatever room the
/// panel leaves it.
/// </summary>
[DisallowMultipleComponent]
public class SeederField : MonoBehaviour
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

    [Tooltip("Ground inside the fence, waiting for seed.")]
    [SerializeField] Color soil = new Color(0.34f, 0.25f, 0.17f, 1f);

    [Tooltip("The verge outside the fence. Seed laid there is wasted, so it reads as ground you do not own.")]
    [SerializeField] Color verge = new Color(0.16f, 0.15f, 0.13f, 1f);

    Texture2D field;

    // Which texels fall inside the fence, and how many of them there are: the denominator
    // every run is scored against, worked out once when the field is built.
    bool[] fenced;
    int width;
    int height;
    int fencedCount;

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

        if (field == null || field.width != width || field.height != height)
        {
            Discard(field);
            field = new Texture2D(width, height, TextureFormat.RGBA32, false);
            field.filterMode = FilterMode.Point;
            field.wrapMode = TextureWrapMode.Clamp;
        }

        fenced = new bool[width * height];
        fencedCount = 0;

        Color32 soil32 = soil;
        Color32 verge32 = verge;
        Color32[] pixels = new Color32[width * height];

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

        field.SetPixels32(pixels);
        field.Apply(false);

        if (ground != null) ground.texture = field;
        if (fitter != null) fitter.aspectRatio = boxWidth / boxHeight;
    }

    /// <summary>
    /// Lets go of a texture made at runtime. Which call does that depends on whether the
    /// game is running, and keeping the field drivable from the editor is worth the branch:
    /// it is how the rasteriser gets checked without entering play mode.
    /// </summary>
    static void Discard(UnityEngine.Object doomed)
    {
        if (doomed == null) return;
        if (Application.isPlaying) Destroy(doomed);
        else DestroyImmediate(doomed);
    }
}
