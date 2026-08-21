using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drags the camera across the farm the way a map on a phone moves, and keeps it
/// over the land.
///
/// The drag comes from Unity's EventSystem, so it never fights the UI: a finger
/// that starts on the panel belongs to the panel, not to the map. This lives on
/// the root the whole map hangs off, because the EventSystem walks up the
/// hierarchy from whatever was pressed to find a drag handler.
///
/// What has to stay over the land is not the screen but the *band* of it the
/// panel leaves uncovered. Measuring against the whole screen is what used to
/// pin the view to a third of a unit of travel and park the bottom row of
/// parcels permanently under the sheet.
/// </summary>
[DisallowMultipleComponent]
public class MapPan : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] Camera view;

    [Header("Extent of the farm, in world units")]
    [SerializeField] Vector2 mapCentre = Vector2.zero;
    [SerializeField] Vector2 mapSize = new Vector2(24f, 12f);
    [Tooltip("How far past the edge of the land the view may travel.")]
    [SerializeField, Min(0f)] float edgeMargin = 0.3f;

    [Header("The sheet that covers the bottom of the screen")]
    [Tooltip("Picks are announced here. The map centres on them, and the panel is open for as long as one is held.")]
    [SerializeField] ParcelSelectionChannel channel;
    [Tooltip("How much of the screen height the open panel hides. Keep this in step with the panel's own height.")]
    [SerializeField, Range(0f, 0.8f)] float sheetCover = 0.396f;

    [Header("Feel")]
    [Tooltip("How quickly the glide after a swipe runs out. Higher stops sooner.")]
    [SerializeField, Range(0.5f, 20f)] float glideDamping = 6f;
    [Tooltip("Below this speed the glide is dropped instead of crawling to a stop.")]
    [SerializeField, Min(0f)] float glideCutoff = 0.05f;
    [Tooltip("How quickly the view settles on the parcel it was sent to. Higher arrives sooner.")]
    [SerializeField, Range(0.5f, 20f)] float focusDamping = 8f;

    Vector2 glide;
    bool dragging;
    bool sheetOpen;
    bool focusing;
    Vector3 focusTarget;

    void Reset() { view = Camera.main; }

    void OnEnable()
    {
        if (view == null) view = Camera.main;
        glide = Vector2.zero;
        dragging = false;
        focusing = false;
        sheetOpen = false;
        if (channel != null) channel.Selected += OnSelected;
        if (view != null) view.transform.position = Clamped(view.transform.position);
    }

    void OnDisable()
    {
        if (channel != null) channel.Selected -= OnSelected;
    }

    /// <summary>
    /// Puts the pick in the middle of what the player can actually see, which is
    /// the band above the panel and not the middle of the screen.
    /// </summary>
    void OnSelected(Parcel parcel)
    {
        if (view == null) return;
        glide = Vector2.zero;
        sheetOpen = parcel != null;

        Vector3 here = view.transform.position;
        if (parcel != null)
        {
            Vector3 field = parcel.transform.position;
            focusTarget = new Vector3(field.x, field.y - BandOffset(), here.z);
        }
        else
        {
            // The band just grew back to the whole screen, so the view may now be
            // sitting off the bottom of the island. Ease it back over the land.
            focusTarget = here;
        }
        focusTarget = Clamped(focusTarget);
        focusing = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // A hand on the map outranks wherever the map was taking itself.
        dragging = true;
        glide = Vector2.zero;
        focusing = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (view == null) return;
        Vector2 shift = eventData.delta * (view.orthographicSize * 2f / Screen.height);
        Vector3 was = view.transform.position;
        view.transform.position = Clamped(was - new Vector3(shift.x, shift.y, 0f));
        // Measure what actually happened, so running into the edge does not
        // build up speed the view cannot use.
        if (Time.unscaledDeltaTime > 0f)
        {
            Vector3 moved = view.transform.position - was;
            glide = new Vector2(moved.x, moved.y) / Time.unscaledDeltaTime;
        }
    }

    public void OnEndDrag(PointerEventData eventData) { dragging = false; }

    void Update()
    {
        if (dragging || view == null) return;

        if (focusing)
        {
            Vector3 from = view.transform.position;
            Vector3 now = Vector3.Lerp(from, focusTarget, 1f - Mathf.Exp(-focusDamping * Time.unscaledDeltaTime));
            if ((focusTarget - now).sqrMagnitude < 1e-6f) { now = focusTarget; focusing = false; }
            view.transform.position = now;
            return;
        }

        if (glide.sqrMagnitude <= glideCutoff * glideCutoff) { glide = Vector2.zero; return; }
        Vector3 was = view.transform.position;
        view.transform.position = Clamped(was + new Vector3(glide.x, glide.y, 0f) * Time.unscaledDeltaTime);
        if ((view.transform.position - was).sqrMagnitude < 1e-8f) glide = Vector2.zero;
        else glide *= Mathf.Exp(-glideDamping * Time.unscaledDeltaTime);
    }

    /// <summary>How much of the view the sheet is hiding right now.</summary>
    float Cover() { return sheetOpen ? sheetCover : 0f; }

    /// <summary>How far above the camera the centre of the uncovered band sits.</summary>
    float BandOffset() { return view.orthographicSize * Cover(); }

    /// <summary>
    /// Keeps the uncovered band over the land. On an axis where the farm is
    /// smaller than that band there is nothing to scroll, so it is centred instead.
    /// </summary>
    Vector3 Clamped(Vector3 position)
    {
        float halfHeight = view.orthographicSize;
        float halfWidth = halfHeight * view.aspect;
        float bandHalf = halfHeight * (1f - Cover());
        float bandOffset = BandOffset();

        float reachX = mapSize.x * 0.5f + edgeMargin - halfWidth;
        float reachY = mapSize.y * 0.5f + edgeMargin - bandHalf;

        position.x = reachX <= 0f ? mapCentre.x : Mathf.Clamp(position.x, mapCentre.x - reachX, mapCentre.x + reachX);

        float band = position.y + bandOffset;
        band = reachY <= 0f ? mapCentre.y : Mathf.Clamp(band, mapCentre.y - reachY, mapCentre.y + reachY);
        position.y = band - bandOffset;
        return position;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(mapCentre, new Vector3(mapSize.x, mapSize.y, 0f));
    }
#endif
}
