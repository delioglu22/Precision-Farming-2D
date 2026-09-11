using UnityEngine;

/// <summary>
/// Bakes a parcel's window the same way <see cref="SeederField"/> does: a torn-edged, shadowed
/// crop of a dirt photo, sized to <c>footprint * cellPixels</c> and clamped to fit the canvas,
/// everything past the torn edge left transparent so the scene's own grass shows through.
///
/// Ported out of SeederField rather than called into it - the seeder keeps painting into this
/// same pixel buffer while the player drags (seed marks, the drawn line), which this baker has
/// no part of. Pulling the whole of SeederField apart to share that live state was more risk to
/// working code than a second copy of the one static bake step is worth. If the two ever drift,
/// this is the one to update - SeederField.Build is the original.
/// </summary>
public static class ParcelGroundBaker
{
    public struct Settings
    {
        public Texture2D dirtSource;
        public float cellPixels;
        public float sideMargin;
        public float topBottomMargin;
        public float tornBleed;
        public float noiseFreq1;
        public float noiseWeight1;
        public float noiseFreq2;
        public float noiseWeight2;
        public float noiseAmplitude;
        public float edgeSoftness;
        public Color shadowColor;
        public float aoFade;
        public float aoMax;

        /// <summary>The seeder's own numbers, so a caller only overrides what it means to change.</summary>
        public static Settings Default(Texture2D dirtSource)
        {
            Settings s = new Settings();
            s.dirtSource = dirtSource;
            s.cellPixels = 120f;
            s.sideMargin = 60f;
            s.topBottomMargin = 120f;
            s.tornBleed = 40f;
            s.noiseFreq1 = 0.03f;
            s.noiseWeight1 = 0.35f;
            s.noiseFreq2 = 0.13f;
            s.noiseWeight2 = 1f;
            s.noiseAmplitude = 15f;
            s.edgeSoftness = 4f;
            s.shadowColor = new Color(0.094f, 0.063f, 0.043f, 1f);
            s.aoFade = 105f;
            s.aoMax = 0.38f;
            return s;
        }
    }

    public struct Result
    {
        public Texture2D texture;
        public float texelsPerCell;
        public int width;
        public int height;
    }

    const float CanvasWidth = 1080f;
    const float CanvasHeight = 1920f;

    static float EdgeNoise(float pos, float seed, Settings s)
    {
        float n1 = (Mathf.PerlinNoise(pos * s.noiseFreq1, seed) - 0.5f) * 2f * s.noiseWeight1;
        float n2 = (Mathf.PerlinNoise(pos * s.noiseFreq2, seed + 50f) - 0.5f) * 2f * s.noiseWeight2;
        float weightSum = s.noiseWeight1 + s.noiseWeight2;
        return weightSum > 0f ? (n1 + n2) / weightSum : 0f;
    }

    public static Result Bake(Vector2Int footprint, Settings s, Texture2D reuse)
    {
        Result result = new Result();
        if (footprint.x <= 0 || footprint.y <= 0 || s.dirtSource == null) return result;

        float maxWinW = CanvasWidth - s.sideMargin * 2f;
        float maxWinH = CanvasHeight - s.topBottomMargin * 2f;
        float cellPx = s.cellPixels;
        if (footprint.x * cellPx > maxWinW) cellPx = Mathf.Min(cellPx, maxWinW / footprint.x);
        if (footprint.y * cellPx > maxWinH) cellPx = Mathf.Min(cellPx, maxWinH / footprint.y);

        float winW = footprint.x * cellPx;
        float winH = footprint.y * cellPx;

        int width = Mathf.Max(1, Mathf.RoundToInt(winW + s.tornBleed * 2f));
        int height = Mathf.Max(1, Mathf.RoundToInt(winH + s.tornBleed * 2f));

        float winAspect = winW / winH;
        float srcAspect = (float)s.dirtSource.width / s.dirtSource.height;
        float dW = 1f, dH = 1f, dX = 0f, dY = 0f;
        if (srcAspect < winAspect) { dH = srcAspect / winAspect; dY = (1f - dH) * 0.5f; }
        else { dW = winAspect / srcAspect; dX = (1f - dW) * 0.5f; }
        float uPerPx = dW / winW;
        float vPerPx = dH / winH;

        Texture2D field = reuse;
        if (field == null || field.width != width || field.height != height)
        {
            if (field != null) Object.Destroy(field);
            field = new Texture2D(width, height, TextureFormat.RGBA32, false);
            field.filterMode = FilterMode.Bilinear;
            field.wrapMode = TextureWrapMode.Clamp;
        }

        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float ry = y - s.tornBleed;
            float distTop = winH - 1f - ry;
            float distBottom = ry;

            for (int x = 0; x < width; x++)
            {
                float rx = x - s.tornBleed;
                float distLeft = rx;
                float distRight = winW - 1f - rx;
                int i = y * width + x;

                float dLeft = distLeft + EdgeNoise(ry, 11.3f, s) * s.noiseAmplitude;
                float dRight = distRight + EdgeNoise(ry, 47.9f, s) * s.noiseAmplitude;
                float dTop = distTop + EdgeNoise(rx, 91.7f, s) * s.noiseAmplitude;
                float dBottom = distBottom + EdgeNoise(rx, 5.2f, s) * s.noiseAmplitude;
                float inside = Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dTop, dBottom));

                float contentT = Mathf.Clamp01((inside + s.edgeSoftness * 0.5f) / s.edgeSoftness);
                contentT = contentT * contentT * (3f - 2f * contentT);

                float su = Mathf.Clamp01(dX + rx * uPerPx);
                float sv = Mathf.Clamp01(dY + ry * vPerPx);
                Color baseCol = s.dirtSource.GetPixelBilinear(su, sv);

                float aoT = inside > 0f ? Mathf.Clamp01(1f - inside / s.aoFade) : 1f;
                aoT = aoT * aoT * (3f - 2f * aoT);
                float aoAlpha = aoT * s.aoMax;

                Color outc = Color.Lerp(baseCol, s.shadowColor, aoAlpha);
                outc.a = contentT;
                pixels[i] = outc;
            }
        }

        field.SetPixels32(pixels);
        field.Apply(false);

        result.texture = field;
        result.texelsPerCell = cellPx;
        result.width = width;
        result.height = height;
        return result;
    }
}
