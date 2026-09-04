using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The seeder's playfield: the parcel it was sent to, seen from a pure top-down perspective.
///
/// The ground covers the entire screen in 2D orthogonal top-down view, laid out dynamically into discrete
/// square soil tiles matching the parcel's cell footprint (e.g. 5x4 = 20 tiles).
///
/// As the player drags across the soil, a thin bright neon central line is drawn with a glowing
/// phosphor (highlighter) aura around it.
/// </summary>
[DisallowMultipleComponent]
public class SeederField : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("The run this scene is playing. Everything the field needs is in here.")]
    [SerializeField] SeederRun run;

    [Header("Ground Display")]
    [Tooltip("2D RawImage covering the screen displaying the soil texture.")]
    [SerializeField] RawImage ground;

    [Header("UI & Scoring")]
    [Tooltip("Percentage text display (top right).")]
    [SerializeField] TMP_Text result;

    [Tooltip("Battery gauge image at the bottom.")]
    [SerializeField] Image battery;

    [Tooltip("Optional field title.")]
    [SerializeField] TMP_Text title;

    [Header("Field Parameters")]
    [Tooltip("Texels along the parcel's longer side.")]
    [SerializeField, Range(128, 1024)] int resolution = 512;

    [Tooltip("Bare ground ringing the parcel, as a fraction of its longer side.")]
    [SerializeField, Range(0.02f, 0.2f)] float margin = 0.05f;

    [Tooltip("How much line the machine carries, in grid cells.")]
    [SerializeField, Min(1f)] float batteryCells = 48f;

    [Tooltip("Number of parallel horizontal plowed furrows per tile.")]
    [SerializeField, Range(2f, 8f)] float furrowsPerTile = 4.0f;

    [Header("Drawing Style (Phosphor & Center Line)")]
    [Tooltip("How wide the phosphor glow aura is, in grid cells.")]
    [SerializeField, Range(0.2f, 2f)] float bandCells = 0.95f;

    [Tooltip("Thickness of the central matte yellow line in pixels/texels.")]
    [SerializeField, Range(0.5f, 5f)] float centerLineWidth = 1.4f;

    [Tooltip("Color of the central matte yellow line.")]
    [SerializeField] Color centerLineColor = new Color(0.780f, 0.680f, 0.215f, 1f);

    [Tooltip("Color of the lighter yellow transparent phosphor band.")]
    [SerializeField] Color phosphorGlowColor = new Color(1.000f, 0.990f, 0.650f, 0.44f);

    [Header("Soil Palette")]
    [Tooltip("Unplowed soil base color.")]
    [SerializeField] Color soil = new Color(0.220f, 0.157f, 0.118f, 1f);

    [Tooltip("Soil clod dark shadow.")]
    [SerializeField] Color clodDark = new Color(0.094f, 0.063f, 0.043f, 1f);

    [Tooltip("Soil clod lit highlight.")]
    [SerializeField] Color clodLight = new Color(0.294f, 0.220f, 0.165f, 1f);

    [Tooltip("Tile seam ditch color.")]
    [SerializeField] Color tileSeam = new Color(0.071f, 0.047f, 0.031f, 1f);

    [Tooltip("The verge outside the fence.")]
    [SerializeField] Color verge = new Color(0.149f, 0.106f, 0.078f, 1f);

    [Tooltip("Stroke color that spilled over the fence onto the verge.")]
    [SerializeField] Color spilledGlow = new Color(1.000f, 0.990f, 0.650f, 0.28f);

    Texture2D field;
    Color32[] pixels;
    Color32[] basePixels;

    bool[] fenced;
    int fencedCount;

    bool[] seeded;
    bool[] isCoreLine;
    byte[] glowLevel;
    int sownCount;

    int width;
    int height;
    float texelsPerCell;
    float boxWidth;
    float boxHeight;
    float padX;
    float padY;

    bool driving;
    bool spent;
    Vector2 lastTexel;
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

    void Start()
    {
        Begin();
    }

    void OnDestroy()
    {
        if (run != null && sownCount > 0 && !spent)
        {
            run.Report(Coverage);
        }
        Discard(field);
    }

    /// <summary>Lays the run's parcel out as ground, ready to be driven over.</summary>
    public void Begin()
    {
        Vector2Int footprint = run != null ? run.Footprint : new Vector2Int(5, 4);
        if (title != null && run != null) title.text = run.ParcelName;
        Build(footprint);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (spent || field == null) return;

        Vector2 texel;
        if (!ScreenPointToTexel(eventData.position, out texel)) return;

        driving = true;
        lastTexel = texel;

        Stamp(texel, texel);
        Show();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (spent || field == null) return;
        if (driving) return;

        Vector2 texel;
        if (!ScreenPointToTexel(eventData.position, out texel)) return;

        driving = true;
        lastTexel = texel;

        Stamp(texel, texel);
        Show();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!driving || spent) return;

        Vector2 texel;
        if (!ScreenPointToTexel(eventData.position, out texel)) return;

        Drive(texel);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EndDrive();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        EndDrive();
    }

    void EndDrive()
    {
        if (!driving) return;

        driving = false;
        spent = true;
        Finish();
    }

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
            Finish();
        }

        Show();
        ShowBattery();
        ShowResult();
    }

    void Finish()
    {
        if (run != null) run.Report(Coverage);
        ShowResult();
    }

    void ShowResult()
    {
        if (result != null) result.text = Mathf.RoundToInt(Coverage * 100f) + "%";
    }

    void ShowBattery()
    {
        if (battery != null) battery.fillAmount = batteryCells <= 0f ? 0f : lineLeft / batteryCells;
    }

    bool ScreenPointToTexel(Vector2 screenPos, out Vector2 texel)
    {
        texel = Vector2.zero;

        RectTransform grt = ground != null ? ground.rectTransform : transform as RectTransform;
        if (grt != null)
        {
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(grt, screenPos, null, out local))
            {
                Rect r = grt.rect;
                if (r.width > 0f && r.height > 0f)
                {
                    float u = Mathf.Clamp01((local.x - r.xMin) / r.width);
                    float v = Mathf.Clamp01((local.y - r.yMin) / r.height);
                    texel = new Vector2(u * width, v * height);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Sows the band swept between two points: draws a thick transparent lighter-colored yellow phosphor
    /// marker band with a thin matte yellow center line.
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
        float coreRadius = centerLineWidth * 0.5f;

        Color32 core32 = centerLineColor;
        core32.a = 255;
        Color32 glow32 = phosphorGlowColor;
        glow32.a = 255;
        Color32 spilled32 = spilledGlow;
        spilled32.a = 255;
        float maxGlowBlend = phosphorGlowColor.a > 0f ? phosphorGlowColor.a : 0.70f;
        float maxSpillBlend = spilledGlow.a > 0f ? spilledGlow.a : 0.45f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int i = y * width + x;

                Vector2 here = new Vector2(x + 0.5f, y + 0.5f);
                float t = lengthSq <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(here - from, along) / lengthSq);
                Vector2 nearest = from + along * t;
                float distSq = (here - nearest).sqrMagnitude;
                if (distSq > radiusSq) continue;

                if (!seeded[i])
                {
                    seeded[i] = true;
                    if (fenced[i]) sownCount++;
                }

                float dist = Mathf.Sqrt(distSq);

                // Phosphor highlighter band with natural soft marker edge on outer 30%
                float normDist = dist / radius; // 0..1
                float falloff = 1f;
                if (normDist > 0.70f)
                {
                    float ft = (normDist - 0.70f) / 0.30f;
                    falloff = 1f - (ft * ft * (3f - 2f * ft));
                }

                byte curAlpha = (byte)Mathf.RoundToInt(falloff * 255f);
                if (glowLevel == null || curAlpha > glowLevel[i])
                {
                    if (glowLevel != null) glowLevel[i] = curAlpha;
                    if (isCoreLine == null || !isCoreLine[i])
                    {
                        float baseBlend = fenced[i] ? maxGlowBlend : maxSpillBlend;
                        float blend = baseBlend * (curAlpha / 255f);
                        Color32 baseColor = basePixels != null ? basePixels[i] : pixels[i];
                        baseColor.a = 255;
                        Color32 targetGlow = fenced[i] ? glow32 : spilled32;
                        Color32 blended = Color32.Lerp(baseColor, targetGlow, blend);
                        blended.a = 255;
                        pixels[i] = blended;
                    }
                }

                // Thin matte yellow pen line on top
                if (dist <= coreRadius)
                {
                    if (isCoreLine != null) isCoreLine[i] = true;
                    pixels[i] = core32;
                }
                else if (dist <= coreRadius + 0.5f)
                {
                    if (isCoreLine == null || !isCoreLine[i])
                    {
                        float lineEdge = 1f - (dist - coreRadius) / 0.5f;
                        Color32 c = Color32.Lerp(pixels[i], core32, lineEdge * 0.65f);
                        c.a = 255;
                        pixels[i] = c;
                    }
                }
            }
        }
    }

    void Show()
    {
        field.SetPixels32(pixels);
        field.Apply(false);
    }

    /// <summary>
    /// Builds the parcel field divided dynamically into discrete soil tiles (footprint.x x footprint.y).
    /// </summary>
    public void Build(Vector2Int footprint)
    {
        if (footprint.x <= 0 || footprint.y <= 0) return;

        // Screen aspect ratio (width / height)
        float screenAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 9f / 16f;

        // Ensure square tiles fit cleanly on the screen
        float minMargin = Mathf.Max(0.04f, margin);
        float reqWidth = footprint.x / (1f - minMargin * 2f);
        float reqHeight = footprint.y / (1f - minMargin * 2f);

        if (reqWidth / screenAspect >= reqHeight)
        {
            boxWidth = reqWidth;
            boxHeight = boxWidth / screenAspect;
        }
        else
        {
            boxHeight = reqHeight;
            boxWidth = boxHeight * screenAspect;
        }

        padX = (boxWidth - footprint.x) * 0.5f;
        padY = (boxHeight - footprint.y) * 0.5f;

        width = resolution;
        height = Mathf.Max(1, Mathf.RoundToInt(resolution / screenAspect));
        texelsPerCell = width / boxWidth;

        if (field == null || field.width != width || field.height != height)
        {
            Discard(field);
            field = new Texture2D(width, height, TextureFormat.RGBA32, false);
            field.filterMode = FilterMode.Bilinear;
            field.wrapMode = TextureWrapMode.Clamp;
        }

        fenced = new bool[width * height];
        seeded = new bool[width * height];
        isCoreLine = new bool[width * height];
        glowLevel = new byte[width * height];
        pixels = new Color32[width * height];
        basePixels = new Color32[width * height];
        fencedCount = 0;
        sownCount = 0;
        driving = false;
        spent = false;
        lineLeft = batteryCells;
        if (result != null) result.text = "0%";

        Color32 soil32 = soil;
        Color32 verge32 = verge;
        Color32 seam32 = tileSeam;
        Color32 cDark = clodDark;
        Color32 cLight = clodLight;

        float furrowsPerCell = Mathf.Max(1f, furrowsPerTile);
        float furrowHeight = texelsPerCell / furrowsPerCell;

        // Fill soil with clean square tile grid
        for (int y = 0; y < height; y++)
        {
            float py = boxHeight * (y + 0.5f) / height;
            bool inRows = py >= padY && py <= padY + footprint.y;
            float cellY = inRows ? (py - padY) : -1f;

            for (int x = 0; x < width; x++)
            {
                float px = boxWidth * (x + 0.5f) / width;
                bool inCols = px >= padX && px <= padX + footprint.x;
                float cellX = inCols ? (px - padX) : -1f;

                int i = y * width + x;
                bool within = inRows && inCols;
                fenced[i] = within;

                if (within)
                {
                    fencedCount++;

                    // Distance to cell boundaries (seams)
                    float fx = cellX - Mathf.Floor(cellX);
                    float fy = cellY - Mathf.Floor(cellY);
                    float seamDistX = Mathf.Min(fx, 1f - fx) * texelsPerCell;
                    float seamDistY = Mathf.Min(fy, 1f - fy) * texelsPerCell;
                    float seamDist = Mathf.Min(seamDistX, seamDistY);

                    float edgeDistX = Mathf.Min(cellX, footprint.x - cellX) * texelsPerCell;
                    float edgeDistY = Mathf.Min(cellY, footprint.y - cellY) * texelsPerCell;
                    float edgeDist = Mathf.Min(edgeDistX, edgeDistY);

                    // Delicate tile seam grooves
                    if (edgeDist < 1.0f || seamDist < 0.75f)
                    {
                        pixels[i] = Color32.Lerp(soil32, seam32, 0.62f);
                    }
                    else if (seamDist < 1.35f)
                    {
                        pixels[i] = Color32.Lerp(soil32, seam32, 0.25f);
                    }
                    else
                    {
                        // Parallel horizontal plowed furrows across minigame tiles
                        float waver = Mathf.Sin(x * 0.04f) * 0.7f + (Mathf.PerlinNoise(x * 0.025f, 10.5f) - 0.5f) * 1.6f;
                        float fyPix = ((cellY * texelsPerCell) + waver) % furrowHeight;
                        if (fyPix < 0f) fyPix += furrowHeight;
                        float furrowPhase = fyPix / furrowHeight; // 0..1

                        // Horizontal longitudinal soil grain
                        float grain = (Mathf.PerlinNoise(x * 0.06f, y * 0.25f) - 0.5f) * 14f;

                        Color baseColor;
                        if (furrowPhase < 0.18f)
                        {
                            // Furrow trench groove (deep shadow)
                            float t = Mathf.Abs(furrowPhase - 0.09f) / 0.09f;
                            baseColor = Color.Lerp(cDark, soil32, t);
                        }
                        else if (furrowPhase < 0.65f)
                        {
                            // Furrow ridge mound with lit crest
                            float t = Mathf.Sin((furrowPhase - 0.18f) / 0.47f * Mathf.PI);
                            baseColor = Color.Lerp(soil32, cLight, t * 0.85f);
                        }
                        else
                        {
                            // Furrow shadow side transitioning to next trench
                            float t = (furrowPhase - 0.65f) / 0.35f;
                            baseColor = Color.Lerp(soil32, cDark, t);
                        }

                        int r = Mathf.Clamp(Mathf.RoundToInt(baseColor.r * 255f + grain), 0, 255);
                        int g = Mathf.Clamp(Mathf.RoundToInt(baseColor.g * 255f + grain), 0, 255);
                        int b = Mathf.Clamp(Mathf.RoundToInt(baseColor.b * 255f + grain), 0, 255);
                        pixels[i] = new Color32((byte)r, (byte)g, (byte)b, 255);
                    }
                }
                else
                {
                    float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
                    int v = Mathf.RoundToInt((n - 0.5f) * 10f);
                    byte r = (byte)Mathf.Clamp(verge32.r + v, 0, 255);
                    byte g = (byte)Mathf.Clamp(verge32.g + v, 0, 255);
                    byte b = (byte)Mathf.Clamp(verge32.b + v, 0, 255);
                    pixels[i] = new Color32(r, g, b, 255);
                }
            }
        }

        // Add fine crumbs and tilled earth speckles along the furrows
        System.Random rnd = new System.Random(2026);
        int crumbCount = footprint.x * footprint.y * 40;
        for (int k = 0; k < crumbCount; k++)
        {
            float rx = (float)rnd.NextDouble() * footprint.x;
            float ry = (float)rnd.NextDouble() * footprint.y;
            int px = Mathf.RoundToInt((padX + rx) * texelsPerCell);
            int py = Mathf.RoundToInt((padY + ry) * texelsPerCell);
            if (px >= 0 && px < width && py >= 0 && py < height)
            {
                int idx = py * width + px;
                if (fenced[idx])
                {
                    pixels[idx] = rnd.Next(0, 2) == 0 ? cDark : cLight;
                    if (px + 1 < width && rnd.Next(0, 3) == 0) pixels[idx + 1] = cDark;
                }
            }
        }

        // Cache base soil pixels for smooth glow blending
        Array.Copy(pixels, basePixels, pixels.Length);

        Show();
        ShowBattery();
        ShowResult();

        if (ground != null)
        {
            ground.texture = field;
        }
    }

    static void Discard(UnityEngine.Object doomed)
    {
        if (doomed == null) return;
        if (Application.isPlaying) Destroy(doomed);
        else DestroyImmediate(doomed);
    }
}
