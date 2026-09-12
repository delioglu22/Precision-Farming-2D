using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Spawns the drone's field as a grid of tappable tiles, one per cell, sized and placed to
/// match the grid <see cref="DroneGroundView"/> draws over the same run, and plays out the
/// run described in docs/design.md ("The drone"): some tiles light red for a look, then go
/// dark - only once they do can the player tap, from memory, and a tap leaves the same wet
/// mark whether it was right or wrong. The run ends once every lit group has been found.
///
/// A tile's "needed" flag is set once at <see cref="Start"/> and never moves - grouping only
/// controls which needed tiles are lit red *right now*, not which tiles count. A tile tapped
/// before its group ever lights (pure luck) still counts as found: the score only asks whether
/// a needed tile got tapped, not when. <see cref="ActivateNextGroup"/> prunes tiles like that
/// out of the group they belong to before lighting it, so a group already satisfied by an
/// early guess closes itself instead of waiting for a tap that will never come.
/// </summary>
[DisallowMultipleComponent]
public class DroneField : MonoBehaviour
{
    [Tooltip("The run this scene is playing. Everything the field needs is in here.")]
    [SerializeField] private DroneRun run;

    [Tooltip("Holds the generated tiles. Resized to frame the grid, same window the ground and grid lines use.")]
    [SerializeField] private RectTransform tiles;

    [Tooltip("The tile prefab instantiated once per cell - its colours and components live on the prefab, not in code.")]
    [SerializeField] private DroneTile tilePrefab;

    [Tooltip("Pixels per cell, both axes - must match DroneGroundView's own so the tap grid lines up with what it draws.")]
    [SerializeField, Min(1f)] private float cellPixels = 120f;
    [SerializeField, Min(0f)] private float sideMargin = 60f;
    [SerializeField, Min(0f)] private float topBottomMargin = 120f;

    [Tooltip("How long a lit tile stays red before going dark again - still open in docs/design.md, tune by playing.")]
    [SerializeField, Min(0.1f)] private float litSeconds = 2f;

    [Header("UI")]
    [SerializeField] private TMP_Text result;

    [Tooltip("Flies to a tile and sprays it on a correct tap. Optional - purely visual.")]
    [SerializeField] private DroneFlight flight;

    readonly List<DroneTile> tileList = new List<DroneTile>();
    readonly List<List<int>> groups = new List<List<int>>();
    readonly HashSet<int> remainingInGroup = new HashSet<int>();
    Coroutine dimRoutine;
    int activeGroup;
    int totalTaps;
    int correctTaps;
    bool spent;
    bool accepting;

    float Score { get { return totalTaps > 0 ? (float)correctTaps / totalTaps : 0f; } }

    void Start()
    {
        Vector2Int footprint = run != null ? run.Footprint : new Vector2Int(5, 4);
        int tilesNeeded = run != null ? run.TilesNeeded : 8;
        int maxOnScreen = run != null ? Mathf.Max(1, run.MaxOnScreen) : 5;
        if (result != null) result.text = "";
        Build(footprint, tilesNeeded, maxOnScreen);
    }

    void Build(Vector2Int footprint, int tilesNeeded, int maxOnScreen)
    {
        if (tiles == null || tilePrefab == null || footprint.x <= 0 || footprint.y <= 0) return;

        if (dimRoutine != null) { StopCoroutine(dimRoutine); dimRoutine = null; }
        for (int i = tiles.childCount - 1; i >= 0; i--) Destroy(tiles.GetChild(i).gameObject);
        tileList.Clear();
        groups.Clear();
        remainingInGroup.Clear();
        activeGroup = -1;
        totalTaps = 0;
        correctTaps = 0;
        spent = false;
        accepting = false;

        float cellPx = ParcelGroundBaker.FitCellPixels(footprint, cellPixels, sideMargin, topBottomMargin);
        float winW = footprint.x * cellPx;
        float winH = footprint.y * cellPx;

        if (flight != null) flight.Configure(new Vector2(winW, winH));

        tiles.anchorMin = new Vector2(0.5f, 0.5f);
        tiles.anchorMax = new Vector2(0.5f, 0.5f);
        tiles.pivot = new Vector2(0.5f, 0.5f);
        tiles.sizeDelta = new Vector2(winW, winH);
        tiles.anchoredPosition = Vector2.zero;

        int total = footprint.x * footprint.y;
        for (int y = 0; y < footprint.y; y++)
        {
            for (int x = 0; x < footprint.x; x++)
            {
                DroneTile tile = Instantiate(tilePrefab, tiles);
                RectTransform rt = (RectTransform)tile.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cellPx, cellPx);
                rt.anchoredPosition = new Vector2(
                    -winW * 0.5f + (x + 0.5f) * cellPx,
                    winH * 0.5f - (y + 0.5f) * cellPx);
                tile.Init(this, tileList.Count);
                tileList.Add(tile);
            }
        }

        int needed = Mathf.Clamp(tilesNeeded, 0, total);
        List<int> indices = new List<int>(total);
        for (int i = 0; i < total; i++) indices.Add(i);
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int swap = indices[i];
            indices[i] = indices[j];
            indices[j] = swap;
        }

        for (int i = 0; i < needed; i++) tileList[indices[i]].SetNeeded(true);

        int size = Mathf.Max(1, maxOnScreen);
        for (int start = 0; start < needed; start += size)
        {
            int count = Mathf.Min(size, needed - start);
            groups.Add(indices.GetRange(start, count));
        }

        ActivateNextGroup();
    }

    void ActivateNextGroup()
    {
        activeGroup++;
        if (activeGroup >= groups.Count)
        {
            Finish();
            return;
        }

        accepting = false;
        remainingInGroup.Clear();
        foreach (int i in groups[activeGroup])
        {
            // Tapped early, by luck, before this group ever lit - already found.
            if (tileList[i].Wet) continue;
            remainingInGroup.Add(i);
            tileList[i].SetLit(true);
        }

        if (remainingInGroup.Count == 0)
        {
            ActivateNextGroup();
            return;
        }

        dimRoutine = StartCoroutine(DimAfterDelay(activeGroup));
    }

    /// <summary>
    /// The tile goes dark, not the requirement - an untapped tile still needs a tap, the
    /// player just has to remember where it was. A tile already found by the time this fires
    /// is left alone, whether that happened before the group lit or during it. Taps are shut
    /// out for the whole field until this fires - looking is not the same as acting, or the
    /// player never has to remember anything, just watch and tap.
    /// </summary>
    IEnumerator DimAfterDelay(int groupIndex)
    {
        yield return new WaitForSeconds(litSeconds);
        foreach (int i in groups[groupIndex])
        {
            if (!tileList[i].Wet) tileList[i].SetLit(false);
        }
        dimRoutine = null;
        accepting = true;
    }

    /// <summary>A tile calls this on itself when tapped. Index is its position in <see cref="tileList"/>.</summary>
    public void OnTileTapped(int index)
    {
        if (spent || !accepting) return;

        DroneTile tile = tileList[index];
        if (tile.Wet)
        {
            // Not prevented, but the mark is already down - it only costs the click.
            totalTaps++;
            return;
        }

        tile.MarkWet();
        totalTaps++;
        if (flight != null) flight.Visit((RectTransform)tile.transform);

        if (!tile.IsNeeded) return;

        correctTaps++;
        if (remainingInGroup.Remove(index) && remainingInGroup.Count == 0) ActivateNextGroup();
    }

    void Finish()
    {
        spent = true;
        foreach (DroneTile tile in tileList)
        {
            if (tile.Wet) tile.Reveal(tile.IsNeeded);
        }

        float score = Score;
        if (result != null) result.text = Mathf.RoundToInt(score * 100f) + "%";
        if (run != null) run.Report(score);
    }

    void OnDestroy()
    {
        if (run != null && !spent && totalTaps > 0) run.Report(Score);
    }
}
