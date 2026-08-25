using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The sheet that rises from the bottom when a parcel is picked, and grows to fill
/// the screen when there is more to show. Both moves are Animator state changes
/// authored in the Animation window - this only flips the parameters and fills in
/// the title.
/// </summary>
[DisallowMultipleComponent]
public class ParcelPanel : MonoBehaviour
{
    [Tooltip("Picks are announced here. The same asset the map scene raises on.")]
    [SerializeField] ParcelSelectionChannel channel;
    [SerializeField] Animator sheet;
    [SerializeField] TMP_Text title;
    [SerializeField] Button closeButton;

    static readonly int Open = Animator.StringToHash("Open");
    static readonly int Expanded = Animator.StringToHash("Expanded");

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
        if (sheet == null) return;

        // A fresh pick always arrives as the small sheet.
        sheet.SetBool(Expanded, false);
        sheet.SetBool(Open, parcel != null);
    }

    /// <summary>
    /// Grows the sheet to full screen and back. A Button calls this straight from the
    /// Inspector, which is why it takes the bool: a UnityEvent cannot reach
    /// Animator.SetBool, and a trigger would latch and re-expand the next panel.
    /// </summary>
    public void SetExpanded(bool expanded)
    {
        if (sheet != null) sheet.SetBool(Expanded, expanded);
    }

    /// <summary>
    /// One step back: full screen shrinks to the sheet, the sheet deselects. Closing by
    /// hand is a deselection, so that half goes back through the channel.
    /// </summary>
    public void Dismiss()
    {
        if (sheet != null && sheet.GetBool(Expanded))
        {
            sheet.SetBool(Expanded, false);
            return;
        }

        if (channel != null) channel.Raise(null);
        else if (sheet != null) sheet.SetBool(Open, false);
    }
}
