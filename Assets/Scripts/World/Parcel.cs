using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A field the player can own and, later, automate.
///
/// The click arrives from Unity's EventSystem: a Physics2DRaycaster on the
/// camera does the hit test, picks the parcel drawn in front, respects the drag
/// threshold so panning does not count as a tap, and swallows anything that
/// landed on the UI. None of that needs code of ours.
///
/// A parcel also listens to the channel it raises on, because it cannot drop its
/// own highlight without knowing that somebody else was picked.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class Parcel : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Where the pick is announced. The UI listens on the other side.")]
    [SerializeField] ParcelSelectionChannel channel;
    [Tooltip("Shown in the panel. Falls back to the object name when left empty.")]
    [SerializeField] string displayName;
    [Tooltip("Two poses blended by a bool: the picked parcel warms up and grows a few percent.")]
    [SerializeField] Animator highlight;
    [Tooltip("How far the pick jumps up the draw order so a neighbour cannot clip the growth.")]
    [SerializeField] int selectedSortingBoost = 1000;

    static readonly int Selected = Animator.StringToHash("Selected");

    SpriteRenderer body;
    int baseSortingOrder;

    public string DisplayName
    {
        get { return string.IsNullOrEmpty(displayName) ? name : displayName; }
    }

    void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        baseSortingOrder = body.sortingOrder;
    }

    void OnEnable()
    {
        if (channel != null) channel.Selected += OnSelectionChanged;
    }

    void OnDisable()
    {
        if (channel != null) channel.Selected -= OnSelectionChanged;
    }

    void OnSelectionChanged(Parcel picked)
    {
        bool mine = picked == this;
        if (highlight != null) highlight.SetBool(Selected, mine);

        // sortingOrder is a baked topological depth order, so the parcel that
        // grows would be clipped by whichever neighbour draws after it. The pick
        // goes in front of the whole island for as long as it is held.
        body.sortingOrder = mine ? baseSortingOrder + selectedSortingBoost : baseSortingOrder;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (channel != null) channel.Raise(this);
    }
}
