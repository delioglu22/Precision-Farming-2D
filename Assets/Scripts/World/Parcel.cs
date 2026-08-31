using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A field the player can own and, later, automate.
///
/// A parcel owns its geometry: a Grid child holding its own Field, Crops and Fence
/// tilemaps, sized to its own rectangle of cells. Editing that rectangle in the
/// Inspector - or the crop planted on it - repaints those three tilemaps to match,
/// the way editing the old ParcelFootprint's size used to redraw its SpriteShape.
/// A tilemap does not do that on its own; painting it is an imperative act, not a
/// live function of a size field, so this is the one place that turns "the field is
/// now this rectangle" back into "the tiles say so".
///
/// The click needs no code of ours beyond raising the pick: a Physics2DRaycaster on
/// the camera and a TilemapCollider2D on the Field child do the hit test, and the
/// EventSystem walks up from the collider to find IPointerClickHandler here - the
/// same walk MapPan relies on to catch a drag from anywhere under World.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class Parcel : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Shown in the panel. Falls back to the object name when left empty.")]
    [SerializeField] string displayName;

    [Tooltip("The block of cells this parcel owns, in tilemap coordinates. Editing this repaints the parcel.")]
    [SerializeField] RectInt cells = new RectInt(0, 0, 7, 5);

    [Tooltip("What is planted, or none for a fallow, ploughed field.")]
    [SerializeField] UnityEngine.Tilemaps.TileBase crop;

    [Tooltip("The soil and boundary tiles a parcel is built from. Assigned once - a new parcel gets these by duplicating an existing one.")]
    [SerializeField] UnityEngine.Tilemaps.TileBase fieldTile;
    [SerializeField] UnityEngine.Tilemaps.TileBase fenceTile;

    [Tooltip("Where the pick is announced. The UI listens on the other side.")]
    [SerializeField] ParcelSelectionChannel channel;

    [Tooltip("How much the picked parcel warms up. Over 1 brightens where the renderer allows it and warms it either way, so it reads even if the value is clamped.")]
    [SerializeField] Color highlight = new Color(1.15f, 1.08f, 0.92f, 1f);

    [Tooltip("How far the picked parcel rises off the map, in world units. Keep it under a quarter - one cell's step up the screen - or a lifted row can sort past the row in front of it.")]
    [SerializeField, Range(0f, 0.24f)] float lift = 0.10f;

    Transform grid;
    UnityEngine.Tilemaps.Tilemap[] layers;
    UnityEngine.Tilemaps.Tilemap field, cropLayer, fence;
    Color[][] before;
    bool selected;

    public string DisplayName
    {
        get { return string.IsNullOrEmpty(displayName) ? name : displayName; }
    }

    public RectInt Cells { get { return cells; } }

    /// <summary>
    /// Size in cells along the two isometric axes, in the order the seeder expects.
    /// The tilemap's X axis is the one the old footprint called Y, so the pair is
    /// swapped here rather than at every call site.
    /// </summary>
    public Vector2Int Footprint
    {
        get { return new Vector2Int(cells.height, cells.width); }
    }

    void Awake()
    {
        CacheLayers();
    }

    void CacheLayers()
    {
        grid = transform.Find("Grid");
        if (grid == null) { layers = new UnityEngine.Tilemaps.Tilemap[0]; return; }
        // A per-parcel Grid must sit at the world origin so its cells line up with
        // the shared Path/Ponds grid - the parcel's own transform is free to sit at
        // its rect's centre (Frame Selected jumps there), but the Grid always
        // overrides back to (0,0,0) regardless of what its parent is doing.
        grid.position = Vector3.zero;
        field = grid.Find("Field").GetComponent<UnityEngine.Tilemaps.Tilemap>();
        cropLayer = grid.Find("Crops").GetComponent<UnityEngine.Tilemaps.Tilemap>();
        fence = grid.Find("Fence").GetComponent<UnityEngine.Tilemaps.Tilemap>();
        layers = new UnityEngine.Tilemaps.Tilemap[] { field, cropLayer, fence };
    }

    bool pendingRebuild;

    // Fires whenever a serialized field changes in the Inspector - including cells,
    // so resizing a parcel there is what "designing a parcel" means now. It cannot
    // call Rebuild directly: Tilemap.ClearAllTiles/SetTile notify the collider via
    // SendMessage, and Unity blocks SendMessage while still inside OnValidate - it
    // logs a warning and, worse, still runs the clear before the guarded refill,
    // wiping Crops the moment this field exists with nothing assigned to it yet.
    // Flagging the work and doing it on the next Update, outside that restricted
    // window, is what the Rebuild is idempotent for.
    void OnValidate()
    {
        pendingRebuild = true;
    }

    void Update()
    {
        if (!pendingRebuild) return;
        pendingRebuild = false;
        if (grid == null) CacheLayers();
        Rebuild();
    }

    // Field and Fence are only ever touched when their tile asset is actually
    // assigned. That is what keeps a fresh, not-yet-wired parcel harmless: with
    // fieldTile/fenceTile still null - which is the state every parcel is in for one
    // frame the moment these fields are introduced, before a one-time script pass
    // wires them - Rebuild leaves the soil and boundary exactly as they were instead
    // of clearing 24 parcels to bare grid. Crops has no such hazard: a null crop is
    // an ordinary, everyday state (a fallow field), so it always clears properly.
    public void Rebuild()
    {
        if (field == null || cropLayer == null || fence == null) return;

        if (fieldTile != null)
        {
            field.ClearAllTiles();
            for (int x = cells.xMin; x < cells.xMax; x++)
                for (int y = cells.yMin; y < cells.yMax; y++)
                    field.SetTile(new Vector3Int(x, y, 0), fieldTile);
            field.CompressBounds();
        }

        if (fenceTile != null)
        {
            fence.ClearAllTiles();
            for (int x = cells.xMin; x < cells.xMax; x++)
                for (int y = cells.yMin; y < cells.yMax; y++)
                    fence.SetTile(new Vector3Int(x, y, 0), fenceTile);
            fence.CompressBounds();
        }

        cropLayer.ClearAllTiles();
        if (crop != null)
            for (int x = cells.xMin; x < cells.xMax; x++)
                for (int y = cells.yMin; y < cells.yMax; y++)
                    cropLayer.SetTile(new Vector3Int(x, y, 0), crop);
        cropLayer.CompressBounds();
    }

    void OnEnable()  { if (channel != null) channel.Selected += OnSelectionChanged; }
    void OnDisable() { if (channel != null) channel.Selected -= OnSelectionChanged; }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (channel != null) channel.Raise(this);
    }

    void OnSelectionChanged(Parcel picked)
    {
        bool mine = picked == this;
        if (mine == selected) return;
        selected = mine;

        if (grid == null) return;
        grid.position = mine ? new Vector3(0f, lift, 0f) : Vector3.zero;

        if (mine) Warm(); else Restore();
    }

    // The cells may already carry a tint of their own, so the old colours are put
    // back rather than reset to white.
    void Warm()
    {
        before = new Color[layers.Length][];
        for (int L = 0; L < layers.Length; L++)
        {
            UnityEngine.Tilemaps.Tilemap t = layers[L];
            BoundsInt b = t.cellBounds;
            before[L] = new Color[b.size.x * b.size.y];
            int i = 0;
            for (int x = b.xMin; x < b.xMax; x++)
                for (int y = b.yMin; y < b.yMax; y++, i++)
                {
                    Vector3Int c = new Vector3Int(x, y, 0);
                    if (t.GetTile(c) == null) { before[L][i] = Color.white; continue; }
                    before[L][i] = t.GetColor(c);
                    t.SetTileFlags(c, UnityEngine.Tilemaps.TileFlags.None);
                    t.SetColor(c, before[L][i] * highlight);
                }
        }
    }

    void Restore()
    {
        if (before == null) return;
        for (int L = 0; L < layers.Length && L < before.Length; L++)
        {
            UnityEngine.Tilemaps.Tilemap t = layers[L];
            if (before[L] == null) continue;
            BoundsInt b = t.cellBounds;
            int i = 0;
            for (int x = b.xMin; x < b.xMax; x++)
                for (int y = b.yMin; y < b.yMax; y++, i++)
                {
                    Vector3Int c = new Vector3Int(x, y, 0);
                    if (t.GetTile(c) == null) continue;
                    t.SetTileFlags(c, UnityEngine.Tilemaps.TileFlags.None);
                    t.SetColor(c, before[L][i]);
                }
        }
        before = null;
    }

    public bool Contains(Vector3Int cell)
    {
        return cell.x >= cells.xMin && cell.x < cells.xMax
            && cell.y >= cells.yMin && cell.y < cells.yMax;
    }
}
