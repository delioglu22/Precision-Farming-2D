using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Lays a parcel's outline out on the isometric grid from its size in cells.
///
/// SpriteShapeController does the drawing — it builds the mesh from the spline,
/// runs the rim and side sprites along the edges and bakes the collider. What it
/// has no notion of is an isometric grid, so this is the one piece left over:
/// turning a footprint in cells into the four corners that stand for it.
///
/// The parcel is a rectangle of cells seen at 2:1, which puts its corners at
/// combinations of the two axis vectors. Every footprint therefore projects to a
/// parallelogram whose bounding box is (W+H) cells wide - the reason a stretch
/// can never turn one footprint into another, and the reason this is data rather
/// than a sprite per size.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteShapeController))]
[DisallowMultipleComponent]
public class ParcelFootprint : MonoBehaviour
{
    [Tooltip("The parcel's size in grid cells, along the two isometric axes.")]
    [SerializeField] Vector2Int footprint = new Vector2Int(5, 7);

    [Tooltip("World size of one cell. The 2:1 ratio is what reads as isometric.")]
    [SerializeField] Vector2 cell = new Vector2(1f, 0.5f);

    public Vector2Int Footprint
    {
        get { return footprint; }
        set { footprint = value; Rebuild(); }
    }

    void OnEnable() { Rebuild(); }

    /// <summary>
    /// Walks the four corners counter-clockwise, into this parcel's own outline
    /// and into every layer hanging off it. A parcel is three stacked fills - the
    /// skirt, the rim and the crop - and all three trace the same parallelogram,
    /// so the footprint stays one number in one place.
    /// </summary>
    public void Rebuild()
    {
        // The two isometric axes, half a cell across and a quarter of one down.
        Vector2 down = new Vector2(cell.x, -cell.y) * 0.5f * footprint.x;
        Vector2 up = new Vector2(cell.x, cell.y) * 0.5f * footprint.y;

        // Centred on the parallelogram's middle, which is where the old sprites
        // put their pivot, so a converted parcel keeps its position.
        Vector2 half = (down + up) * 0.5f;
        Vector2[] corners = { -half, down - half, half, up - half };

        SpriteShapeController[] layers = GetComponentsInChildren<SpriteShapeController>(true);
        for (int s = 0; s < layers.Length; s++)
        {
            ParcelLayer layer = layers[s].GetComponent<ParcelLayer>();
            Vector2[] outline = layer == null ? corners : Outline(corners, layer);

            Spline spline = layers[s].spline;
            spline.Clear();
            for (int i = 0; i < outline.Length; i++)
            {
                spline.InsertPointAt(i, outline[i]);
                spline.SetTangentMode(i, ShapeTangentMode.Linear);
                spline.SetCorner(i, false);
                spline.SetHeight(i, 1f);
            }
            spline.isOpenEnded = false;
            layers[s].RefreshSpriteShape();
        }
    }

    /// <summary>
    /// The four points one layer traces: the top face is the parallelogram itself,
    /// a side face is the band that falls away under one of the lower edges. Both
    /// are wound counter-clockwise.
    /// </summary>
    static Vector2[] Outline(Vector2[] corners, ParcelLayer layer)
    {
        Vector2 left = corners[0], bottom = corners[1], right = corners[2];
        Vector2 fall = new Vector2(0f, -layer.Depth);

        switch (layer.Face)
        {
            case ParcelFace.LowerLeft:
                return new Vector2[] { left, left + fall, bottom + fall, bottom };
            case ParcelFace.LowerRight:
                return new Vector2[] { bottom, bottom + fall, right + fall, right };
            default:
                return Inset(corners, layer.Inset);
        }
    }

    /// <summary>
    /// Walks a convex outline inwards by the same distance on every edge. Each
    /// corner slides along its bisector, which is the point where both adjoining
    /// edges have moved in by exactly <paramref name="by"/>.
    /// </summary>
    static Vector2[] Inset(Vector2[] outline, float by)
    {
        if (by <= 0f) return outline;

        Vector2[] moved = new Vector2[outline.Length];
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 previous = outline[(i - 1 + outline.Length) % outline.Length];
            Vector2 next = outline[(i + 1) % outline.Length];

            // Left normals, because the corners are wound counter-clockwise.
            Vector2 into = (outline[i] - previous).normalized;
            Vector2 outOf = (next - outline[i]).normalized;
            Vector2 nInto = new Vector2(-into.y, into.x);
            Vector2 nOutOf = new Vector2(-outOf.y, outOf.x);

            float det = nInto.x * nOutOf.y - nInto.y * nOutOf.x;
            if (Mathf.Abs(det) < 1e-5f) { moved[i] = outline[i] + nInto * by; continue; }

            moved[i] = outline[i] + new Vector2(
                by * (nOutOf.y - nInto.y) / det,
                by * (nInto.x - nOutOf.x) / det);
        }
        return moved;
    }

#if UNITY_EDITOR
    // Rebuilding straight out of OnValidate runs inside serialization; the delay
    // puts it back on the editor's own loop.
    void OnValidate() { UnityEditor.EditorApplication.delayCall += DelayedRebuild; }

    void DelayedRebuild() { if (this != null) Rebuild(); }
#endif
}
