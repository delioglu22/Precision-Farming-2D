using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A field the player can own and, later, automate.
///
/// The click arrives from Unity's EventSystem: a Physics2DRaycaster on the
/// camera does the hit test, picks the parcel drawn in front, respects the drag
/// threshold so panning does not count as a tap, and swallows anything that
/// landed on the UI. None of that needs code of ours.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class Parcel : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Where the pick is announced. The UI listens on the other side.")]
    [SerializeField] ParcelSelectionChannel channel;
    [Tooltip("Shown in the panel. Falls back to the object name when left empty.")]
    [SerializeField] string displayName;

    public string DisplayName
    {
        get { return string.IsNullOrEmpty(displayName) ? name : displayName; }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (channel != null) channel.Raise(this);
    }
}
