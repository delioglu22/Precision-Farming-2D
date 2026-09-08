using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The seeder's playfield: the parcel it was sent to, seen from a pure top-down perspective.
///
/// Unlike the map, the ground here is not a picture placed on the grass - it is a window cut into
/// it. <see cref="Build"/> bakes a torn-edged, shadowed crop of <see cref="dirtSource"/> sized to the
/// parcel's own footprint (<c>footprint * cellPixels</c> pixels, clamped so it can never grow past the
/// canvas), leaving the rest of the texture transparent so the scene's own grass shows through. The
/// grass is a separate, static object behind this one - this script never touches it.
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
    [Tooltip("RawImage the baked window is drawn into. Its RectTransform is resized to match every time.")]
    [SerializeField] RawImage ground;

    [Header("UI & Scoring")]
    [Tooltip("Percentage text display (top right).")]
    [SerializeField] TMP_Text result;

    [Tooltip("Battery gauge image at the bottom.")]
    [SerializeField] Image battery;

    [Tooltip("Optional field title.")]
    [SerializeField] TMP_Text title;

    [Header("Field Parameters")]
    [Tooltip("The soil photo the window is cropped from (Read/Write must be enabled).")]
    [SerializeField] Texture2D dirtSource;

    [Tooltip("Pixels per cell, both axes - the same unit the map's canvas already uses.")]
    [SerializeField, Min(1f)] float cellPixels = 120f;

    [Tooltip("Grass left showing on the window's left and right, in canvas pixels.")]
    [SerializeField, Min(0f)] float sideMargin = 60f;

    [Tooltip("Grass left showing above and below the window, in canvas pixels.")]
    [SerializeField, Min(0f)] float topBottomMargin = 120f;

    [Tooltip("How far the torn edge's teeth can reach past the window's clean rectangle.")]
    [SerializeField, Min(0f)] float tornBleed = 40f;

    [Tooltip("How much line the machine carries, in grid cells.")]
    [SerializeField, Min(1f)] float batteryCells = 48f;

    [Header("Torn Edge")]
    [SerializeField] float noiseFreq1 = 0.03f;
    [SerializeField] float noiseWeight1 = 0.35f;
    [SerializeField] float noiseFreq2 = 0.13f;
    [SerializeField] float noiseWeight2 = 1.0f;
    [Tooltip("How far the noise displaces the edge, in pixels.")]
    [SerializeField] float noiseAmplitude = 15f;
    [Tooltip("Width of the antialiased band at the torn edge, in pixels.")]
    [SerializeField, Min(0.5f)] float edgeSoftness = 4f;

    [Header("Ambient Occlusion")]
    [Tooltip("Shadow tint the torn edge fades to, cast by the raised grass lip.")]
    [SerializeField] Color shadowColor = new Color(0.094f, 0.063f, 0.043f, 1f);
    [Tooltip("How far the shadow reaches in from the edge, in pixels.")]
    [SerializeField] float aoFade = 105f;
    [Tooltip("Shadow strength at the very edge, 0-1.")]
    [SerializeField, Range(0f, 1f)] float aoMax = 0.38f;

    [Header("Drawing Style (Phosphor & Center Line)")]
    [Tooltip("How wide the phosphor glow aura is, in grid cells.")]
    [SerializeField, Range(0.2f, 2f)] float bandCells = 0.95f;

    [Tooltip("Thickness of the central matte yellow line in pixels/texels.")]
    [SerializeField, Range(0.5f, 5f)] float centerLineWidth = 1.4f;

    [Tooltip("Color of the central matte yellow line.")]
    [SerializeField] Color centerLineColor = new Color(0.780f, 0.680f, 0.215f, 1f);

    [Tooltip("Color of the lighter yellow transparent phosphor band.")]
    [SerializeField] Color phosphorGlowColor = new Color(1.000f, 0.990f, 0.650f, 0.44f);

    [Tooltip("Stroke color that spilled past the parcel, onto the torn edge or bare grass.")]
    [SerializeField] Color spilledGlow = new Color(1.000f, 0.990f, 0.650f, 0.28f);

    const float CanvasWidth = 1080f;
    const float CanvasHeight = 1920f;

    Texture2D field;
    Color32[] pixels;
    Color32[] basePixels;

    bool[] fenced;
    int fencedCount;

    bool[] opaque;
    bool[] seeded;
    bool[] isCoreLine;
    byte[] glowLevel;
    int sownCount;

    int width;
    int height;
    float texelsPerCell;

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
    /// marker band with a thin matte yellow center line. Where the base is the baked dirt (or one of its
    /// torn teeth) the stroke blends into it; where the base is bare, transparent grass it paints directly,
    /// since there is nothing there to blend with.
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
                        if (opaque[i])
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
                        else
                        {
                            // Nothing baked here but transparent grass - paint the mark directly, own alpha and all.
                            Color32 c = spilled32;
                            c.a = curAlpha;
                            pixels[i] = c;
                        }
                    }
                }

                // Thin matte yellow pen line on top
                if (dist <= coreRadius)
                {
                    if (isCoreLine != null) isCoreLine[i] = true;
                    Color32 c = core32;
                    c.a = 255;
                    pixels[i] = c;
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

    /// <summary>Two octaves of Perlin noise, normalised to roughly [-1, 1], sampled along one edge.</summary>
    float EdgeNoise(float pos, float seed)
    {
        float n1 = (Mathf.PerlinNoise(pos * noiseFreq1, seed) - 0.5f) * 2f * noiseWeight1;
        float n2 = (Mathf.PerlinNoise(pos * noiseFreq2, seed + 50f) - 0.5f) * 2f * noiseWeight2;
        float weightSum = noiseWeight1 + noiseWeight2;
        return weightSum > 0f ? (n1 + n2) / weightSum : 0f;
    }

    /// <summary>
    /// Bakes the parcel's window: a torn-edged, shadowed crop of <see cref="dirtSource"/> sized to
    /// <c>footprint * cellPixels</c> (clamped to fit the canvas), everything past the torn edge left
    /// transparent so the scene's own grass shows through.
    /// </summary>
    public void Build(Vector2Int footprint)
    {
        if (footprint.x <= 0 || footprint.y <= 0 || dirtSource == null || ground == null) return;

        float maxWinW = CanvasWidth - sideMargin * 2f;
        float maxWinH = CanvasHeight - topBottomMargin * 2f;
        float cellPx = cellPixels;
        if (footprint.x * cellPx > maxWinW) cellPx = Mathf.Min(cellPx, maxWinW / footprint.x);
        if (footprint.y * cellPx > maxWinH) cellPx = Mathf.Min(cellPx, maxWinH / footprint.y);
        texelsPerCell = cellPx;

        float winW = footprint.x * cellPx;
        float winH = footprint.y * cellPx;

        width = Mathf.Max(1, Mathf.RoundToInt(winW + tornBleed * 2f));
        height = Mathf.Max(1, Mathf.RoundToInt(winH + tornBleed * 2f));

        // Cover-crop the dirt photo to the window's own aspect, same rule as the grass background.
        float winAspect = winW / winH;
        float srcAspect = (float)dirtSource.width / dirtSource.height;
        float dW = 1f, dH = 1f, dX = 0f, dY = 0f;
        if (srcAspect < winAspect) { dH = srcAspect / winAspect; dY = (1f - dH) * 0.5f; }
        else { dW = winAspect / srcAspect; dX = (1f - dW) * 0.5f; }
        float uPerPx = dW / winW;
        float vPerPx = dH / winH;

        if (field == null || field.width != width || field.height != height)
        {
            Discard(field);
            field = new Texture2D(width, height, TextureFormat.RGBA32, false);
            field.filterMode = FilterMode.Bilinear;
            field.wrapMode = TextureWrapMode.Clamp;
        }

        int n = width * height;
        fenced = new bool[n];
        opaque = new bool[n];
        seeded = new bool[n];
        isCoreLine = new bool[n];
        glowLevel = new byte[n];
        pixels = new Color32[n];
        basePixels = new Color32[n];
        fencedCount = 0;
        sownCount = 0;
        driving = false;
        spent = false;
        lineLeft = batteryCells;
        if (result != null) result.text = "0%";

        for (int y = 0; y < height; y++)
        {
            float ry = y - tornBleed;
            float distTop = winH - 1f - ry;
            float distBottom = ry;

            for (int x = 0; x < width; x++)
            {
                float rx = x - tornBleed;
                float distLeft = rx;
                float distRight = winW - 1f - rx;

                int i = y * width + x;

                bool withinNominal = rx >= 0f && rx < winW && ry >= 0f && ry < winH;
                fenced[i] = withinNominal;
                if (withinNominal) fencedCount++;

                float dLeft = distLeft + EdgeNoise(ry, 11.3f) * noiseAmplitude;
                float dRight = distRight + EdgeNoise(ry, 47.9f) * noiseAmplitude;
                float dTop = distTop + EdgeNoise(rx, 91.7f) * noiseAmplitude;
                float dBottom = distBottom + EdgeNoise(rx, 5.2f) * noiseAmplitude;
                float inside = Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dTop, dBottom));

                float contentT = Mathf.Clamp01((inside + edgeSoftness * 0.5f) / edgeSoftness);
                contentT = contentT * contentT * (3f - 2f * contentT);
                opaque[i] = contentT > 0.001f;

                float su = Mathf.Clamp01(dX + rx * uPerPx);
                float sv = Mathf.Clamp01(dY + ry * vPerPx);
                Color baseCol = dirtSource.GetPixelBilinear(su, sv);

                float aoT = inside > 0f ? Mathf.Clamp01(1f - inside / aoFade) : 1f;
                aoT = aoT * aoT * (3f - 2f * aoT);
                float aoAlpha = aoT * aoMax;

                Color outc = Color.Lerp(baseCol, shadowColor, aoAlpha);
                outc.a = contentT;
                pixels[i] = outc;
            }
        }

        // Cache base pixels for smooth glow blending
        Array.Copy(pixels, basePixels, pixels.Length);

        Show();
        ShowBattery();
        ShowResult();

        RectTransform rt = ground.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = Vector2.zero;

        ground.texture = field;
    }

    static void Discard(UnityEngine.Object doomed)
    {
        if (doomed == null) return;
        if (Application.isPlaying) Destroy(doomed);
        else DestroyImmediate(doomed);
    }
}
