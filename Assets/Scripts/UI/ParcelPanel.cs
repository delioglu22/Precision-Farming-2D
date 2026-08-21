using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The sheet that rises from the bottom when a parcel is picked. The sliding is
/// an Animator state change authored in the Animation window - this only flips
/// the parameter and fills in the title.
/// </summary>
[DisallowMultipleComponent]
public class ParcelPanel : MonoBehaviour
{
    [Tooltip("Picks are announced here. The same asset the map scene raises on.")]
    [SerializeField] ParcelSelectionChannel channel;
    [SerializeField] Animator sheet;
    [SerializeField] Text title;
    [SerializeField] Button closeButton;

    static readonly int Open = Animator.StringToHash("Open");

    void OnEnable()
    {
        if (channel != null) channel.Selected += OnSelected;
        if (closeButton != null) closeButton.onClick.AddListener(Dismiss);
    }

    void OnDisable()
    {
        if (channel != null) channel.Selected -= OnSelected;
        if (closeButton != null) closeButton.onClick.RemoveListener(Dismiss);
    }

    void OnSelected(Parcel parcel)
    {
        if (title != null) title.text = parcel != null ? parcel.DisplayName : string.Empty;
        if (sheet != null) sheet.SetBool(Open, parcel != null);
    }

    /// <summary>Closing by hand is a deselection, so it goes back through the channel.</summary>
    public void Dismiss()
    {
        if (channel != null) channel.Raise(null);
        else if (sheet != null) sheet.SetBool(Open, false);
    }
}
