using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Drags the camera across the farm the way a map on a phone moves, and keeps it
/// over the land.
///
/// The drag comes from Unity's EventSystem, so it never fights the UI: a finger
/// that starts on the panel belongs to the panel, not to the map. This lives on
/// the root the whole map hangs off, because the EventSystem walks up the
/// hierarchy from whatever was pressed to find a drag handler. The scroll wheel
/// rides the same bubbling for the same reason - IScrollHandler is one more
/// interface on the same ancestor, nothing new to wire.
///
/// Pinch has no such handler to lean on - UGUI has no multi-touch gesture
/// interface, only single-pointer ones - so it is read directly from the new
/// Input System's EnhancedTouch API in Update, alongside the existing glide.
///
/// What has to stay over the land is not the screen but the *band* of it the
/// panel leaves uncovered. Measuring against the whole screen is what used to
/// pin the view to a third of a unit of travel and park the bottom row of
/// parcels permanently under the sheet.
/// </summary>
[DisallowMultipleComponent]
public class MapPan : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
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

    [Header("How close the view sits")]
    [Tooltip("Orthographic size while a parcel is open, close enough to read one field.")]
    [SerializeField, Min(0.1f)] float pickedSize = 5f;

    [Header("Player-controlled zoom, with nothing picked")]
    [Tooltip("How close the wheel or a pinch may bring the view.")]
    [SerializeField, Min(0.1f)] float minZoomSize = 3f;
    [Tooltip("How far the wheel or a pinch may push the view out. The camera starts here.")]
    [SerializeField, Min(0.1f)] float maxZoomSize = 9f;
    [Tooltip("World units of orthographic size per wheel notch.")]
    [SerializeField, Min(0f)] float scrollSensitivity = 0.6f;

    [Header("Feel")]
    [Tooltip("How quickly the view closes on a pick and opens back out. Higher arrives sooner.")]
    [SerializeField, Range(0.5f, 20f)] float zoomDamping = 8f;
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

    /// <summary>Where the wheel or a pinch has left the view. Starts at maxZoomSize - far - every time the map loads.</summary>
    float manualSize;
    float? pinchDistance;

    void Reset() { view = Camera.main; }

    void OnEnable()
    {
        if (view == null) view = Camera.main;
        glide = Vector2.zero;
        dragging = false;
        focusing = false;
        sheetOpen = false;
        manualSize = maxZoomSize;
        pinchDistance = null;
        EnhancedTouchSupport.Enable();
        if (channel != null) channel.Selected += OnSelected;
        if (view != null)
        {
            view.orthographicSize = manualSize;
            view.transform.position = Clamped(view.transform.position);
        }
    }

    void OnDisable()
    {
        if (channel != null) channel.Selected -= OnSelected;
        EnhancedTouchSupport.Disable();
    }

    /// <summary>Mouse wheel / trackpad. Bubbles up from whatever the pointer is over, same as a drag.</summary>
    public void OnScroll(PointerEventData eventData)
    {
        manualSize = Mathf.Clamp(manualSize - eventData.scrollDelta.y * scrollSensitivity, minZoomSize, maxZoomSize);
    }

    /// <summary>
    /// Two fingers moving apart or together. Unlike a drag this is not offered by
    /// the EventSystem - there is no multi-touch gesture interface in UGUI - so it
    /// is read straight from EnhancedTouch every frame instead of from an event.
    /// </summary>
    void Pinch()
    {
        if (Touch.activeTouches.Count != 2) { pinchDistance = null; return; }

        float distance = Vector2.Distance(Touch.activeTouches[0].screenPosition, Touch.activeTouches[1].screenPosition);
        if (pinchDistance.HasValue && pinchDistance.Value > 0.01f)
        {
            // Fingers spreading (distance grows) shrinks the ratio, which zooms in -
            // the same sense as scrolling up.
            float ratio = pinchDistance.Value / distance;
            manualSize = Mathf.Clamp(manualSize * ratio, minZoomSize, maxZoomSize);
        }
        pinchDistance = distance;
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
            // Aim with the distance the view is heading for rather than the one it
            // is leaving, or the pick sits off centre for as long as the zoom runs.
            Vector3 field = parcel.transform.position;
            focusTarget = new Vector3(field.x, field.y - pickedSize * sheetCover, here.z);
        }
        else
        {
            // The band just grew back to the whole screen, so the view may now be
            // sitting off the bottom of the island. Ease it back over the land.
            focusTarget = here;
        }
        // Left unclamped on purpose: how far the view may travel depends on how
        // much of the map it can see, and that is still changing.
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
        if (view == null) return;

        // A hand on the map does not stop the view from finishing its approach.
        Pinch();
        Zoom();
        if (dragging) return;

        if (focusing)
        {
            Vector3 goal = Clamped(focusTarget);
            Vector3 from = view.transform.position;
            Vector3 now = Vector3.Lerp(from, goal, 1f - Mathf.Exp(-focusDamping * Time.unscaledDeltaTime));
            if ((goal - now).sqrMagnitude < 1e-6f) { now = goal; focusing = false; }
            view.transform.position = now;
            return;
        }

        if (glide.sqrMagnitude <= glideCutoff * glideCutoff) { glide = Vector2.zero; return; }
        Vector3 was = view.transform.position;
        view.transform.position = Clamped(was + new Vector3(glide.x, glide.y, 0f) * Time.unscaledDeltaTime);
        if ((view.transform.position - was).sqrMagnitude < 1e-8f) glide = Vector2.zero;
        else glide *= Mathf.Exp(-glideDamping * Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Eases toward the closer distance a pick asks for, or back toward wherever
    /// the wheel or a pinch last left the view. The clamp is redone as it goes,
    /// since how far the view may travel depends on how much of the map fits on
    /// screen.
    /// </summary>
    void Zoom()
    {
        float wanted = sheetOpen ? pickedSize : manualSize;
        if (view.orthographicSize == wanted) return;

        float size = Mathf.Lerp(view.orthographicSize, wanted, 1f - Mathf.Exp(-zoomDamping * Time.unscaledDeltaTime));
        if (Mathf.Abs(size - wanted) < 0.001f) size = wanted;
        view.orthographicSize = size;
        view.transform.position = Clamped(view.transform.position);
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
