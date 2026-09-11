using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the parcel's ground for the drone's field - the same bake as the seeder's own ground,
/// sized to the run's footprint - and a plain white grid over it, one line per cell boundary,
/// in the parcel's own proportions. Picture only: no tiles, no taps, no score. The
/// tap-to-fertilize grid described in docs/design.md ("The drone") is a separate piece, built
/// once its own numbers are ready.
/// </summary>
[DisallowMultipleComponent]
public class DroneGroundView : MonoBehaviour
{
    [Tooltip("The run this scene is playing. Only its footprint is read here.")]
    [SerializeField] private DroneRun run;

    [Tooltip("RawImage the baked window is drawn into.")]
    [SerializeField] private RawImage ground;

    [Tooltip("The soil photo the window is cropped from. The seeder's own Seeder_Dirt.jpg, so both machines work the same dirt.")]
    [SerializeField] private Texture2D dirtSource;

    [Header("Grid")]
    [Tooltip("Holds the generated line bars. Resized to frame the grid, same as the ground window minus its torn-edge bleed.")]
    [SerializeField] private RectTransform gridLines;

    [Tooltip("Line thickness, in canvas pixels.")]
    [SerializeField, Min(1f)] private float lineThickness = 4f;

    [SerializeField] private Color lineColor = Color.white;

    Texture2D baked;

    void Start()
    {
        Vector2Int footprint = run != null ? run.Footprint : new Vector2Int(5, 4);
        ParcelGroundBaker.Settings settings = ParcelGroundBaker.Settings.Default(dirtSource);
        ParcelGroundBaker.Result result = ParcelGroundBaker.Bake(footprint, settings, baked);
        baked = result.texture;
        if (ground != null && baked != null)
        {
            RectTransform rt = ground.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(result.width, result.height);
            rt.anchoredPosition = Vector2.zero;
            ground.texture = baked;
        }

        DrawGrid(footprint, result.texelsPerCell);
    }

    void DrawGrid(Vector2Int footprint, float cellPx)
    {
        if (gridLines == null) return;

        for (int i = gridLines.childCount - 1; i >= 0; i--) Destroy(gridLines.GetChild(i).gameObject);

        float winW = footprint.x * cellPx;
        float winH = footprint.y * cellPx;

        gridLines.anchorMin = new Vector2(0.5f, 0.5f);
        gridLines.anchorMax = new Vector2(0.5f, 0.5f);
        gridLines.pivot = new Vector2(0.5f, 0.5f);
        gridLines.sizeDelta = new Vector2(winW, winH);
        gridLines.anchoredPosition = Vector2.zero;

        for (int x = 1; x < footprint.x; x++) Line(new Vector2(lineThickness, winH), new Vector2(-winW * 0.5f + x * cellPx, 0f));
        for (int y = 1; y < footprint.y; y++) Line(new Vector2(winW, lineThickness), new Vector2(0f, winH * 0.5f - y * cellPx));
    }

    void Line(Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject("Line", typeof(RectTransform));
        go.transform.SetParent(gridLines, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        Image img = go.AddComponent<Image>();
        img.color = lineColor;
        img.raycastTarget = false;
    }

    void OnDestroy()
    {
        if (baked != null) Destroy(baked);
    }
}
