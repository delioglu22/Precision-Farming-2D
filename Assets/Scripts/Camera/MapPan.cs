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

    [Header("Feel")]
    [Tooltip("How quickly the glide after a swipe runs out. Higher stops sooner.")]
    [SerializeField, Range(0.5f, 20f)] float glideDamping = 6f;
    [Tooltip("Below this speed the glide is dropped instead of crawling to a stop.")]
    [SerializeField, Min(0f)] float glideCutoff = 0.05f;

    Vector2 glide;
    bool dragging;

    void Reset()
    {
        view = Camera.main;
    }

    void OnEnable()
    {
        if (view == null) view = Camera.main;
        glide = Vector2.zero;
        dragging = false;
        if (view != null) view.transform.position = Clamped(view.transform.position);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = true;
        glide = Vector2.zero;
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

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
    }

    void Update()
    {
        if (dragging || view == null) return;
        if (glide.sqrMagnitude <= glideCutoff * glideCutoff) { glide = Vector2.zero; return; }

        Vector3 was = view.transform.position;
        view.transform.position = Clamped(was + new Vector3(glide.x, glide.y, 0f) * Time.unscaledDeltaTime);

        if ((view.transform.position - was).sqrMagnitude < 1e-8f) glide = Vector2.zero;
        else glide *= Mathf.Exp(-glideDamping * Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Keeps the view over the land. On an axis where the farm is smaller than
    /// the screen there is nothing to scroll, so the view is centred instead.
    /// </summary>
    Vector3 Clamped(Vector3 position)
    {
        float halfHeight = view.orthographicSize;
        float halfWidth = halfHeight * view.aspect;

        float reachX = mapSize.x * 0.5f + edgeMargin - halfWidth;
        float reachY = mapSize.y * 0.5f + edgeMargin - halfHeight;

        position.x = reachX <= 0f ? mapCentre.x : Mathf.Clamp(position.x, mapCentre.x - reachX, mapCentre.x + reachX);
        position.y = reachY <= 0f ? mapCentre.y : Mathf.Clamp(position.y, mapCentre.y - reachY, mapCentre.y + reachY);
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
