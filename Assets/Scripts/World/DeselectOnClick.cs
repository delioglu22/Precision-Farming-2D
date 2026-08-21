using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Sits on the sea. Clicking past the land is how you put a parcel down again -
/// the EventSystem only gets here when nothing nearer wanted the click.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class DeselectOnClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] ParcelSelectionChannel channel;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (channel != null) channel.Raise(null);
    }
}
