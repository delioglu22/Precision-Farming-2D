using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns the drone's field as a grid of tappable tiles, one per cell, sized and placed to
/// match the grid <see cref="DroneGroundView"/> draws over the same run. Tapping is all it
/// does for now - which tiles need fertilizing, grouping and score are a separate piece
/// (docs/design.md, "The drone"), waiting on numbers that are still being tuned by playing.
/// </summary>
[DisallowMultipleComponent]
public class DroneField : MonoBehaviour
{
    [Tooltip("The run this scene is playing. Only its footprint is read here.")]
    [SerializeField] private DroneRun run;

    [Tooltip("Holds the generated tiles. Resized to frame the grid, same window the ground and grid lines use.")]
    [SerializeField] private RectTransform tiles;

    [Tooltip("Pixels per cell, both axes - must match DroneGroundView's own so the tap grid lines up with what it draws.")]
    [SerializeField, Min(1f)] private float cellPixels = 120f;
    [SerializeField, Min(0f)] private float sideMargin = 60f;
    [SerializeField, Min(0f)] private float topBottomMargin = 120f;

    void Start()
    {
        Vector2Int footprint = run != null ? run.Footprint : new Vector2Int(5, 4);
        if (tiles == null || footprint.x <= 0 || footprint.y <= 0) return;

        for (int i = tiles.childCount - 1; i >= 0; i--) Destroy(tiles.GetChild(i).gameObject);

        float cellPx = ParcelGroundBaker.FitCellPixels(footprint, cellPixels, sideMargin, topBottomMargin);
        float winW = footprint.x * cellPx;
        float winH = footprint.y * cellPx;

        tiles.anchorMin = new Vector2(0.5f, 0.5f);
        tiles.anchorMax = new Vector2(0.5f, 0.5f);
        tiles.pivot = new Vector2(0.5f, 0.5f);
        tiles.sizeDelta = new Vector2(winW, winH);
        tiles.anchoredPosition = Vector2.zero;

        for (int y = 0; y < footprint.y; y++)
        {
            for (int x = 0; x < footprint.x; x++)
            {
                GameObject go = new GameObject("Tile", typeof(RectTransform));
                go.transform.SetParent(tiles, false);
                RectTransform rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cellPx, cellPx);
                rt.anchoredPosition = new Vector2(
                    -winW * 0.5f + (x + 0.5f) * cellPx,
                    winH * 0.5f - (y + 0.5f) * cellPx);
                go.AddComponent<Image>();
                go.AddComponent<Button>();
                go.AddComponent<DroneTile>();
            }
        }
    }
}
