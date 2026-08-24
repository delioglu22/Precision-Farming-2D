using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The surface a machine is tuned on. It rises over the parcel page, covers it, and
/// walks back to it when closed - one more step in the same chain the page and the
/// sheet already make. Both moves are Animator state changes; this only flips the
/// parameter and names the machine.
///
/// One surface serves all three machines, which is why Open takes the machine's name:
/// a UnityEvent persistent call carries exactly one static argument, so a Button in the
/// Inspector can pass a string but cannot reach Animator.SetBool(name, value).
/// </summary>
[DisallowMultipleComponent]
public class MinigamePanel : MonoBehaviour
{
    [Tooltip("Picks are announced here. Losing the parcel takes this surface with it.")]
    [SerializeField] ParcelSelectionChannel channel;
    [SerializeField] Animator surface;
    [SerializeField] Text title;
    [SerializeField] Button closeButton;

    static readonly int OpenId = Animator.StringToHash("Open");

    void OnEnable()
    {
        if (channel != null) channel.Selected += OnSelected;
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    void OnDisable()
    {
        if (channel != null) channel.Selected -= OnSelected;
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    /// <summary>
    /// Raises the surface for one machine. A Button calls this straight from the
    /// Inspector with the machine's name as its static argument.
    /// </summary>
    public void Open(string machine)
    {
        if (title != null) title.text = machine;
        if (surface != null) surface.SetBool(OpenId, true);
    }

    /// <summary>One step back: the surface drops and the parcel's page is underneath it.</summary>
    public void Close()
    {
        if (surface != null) surface.SetBool(OpenId, false);
    }

    // The surface only ever covers a picked parcel, so a change of pick - a new one or a
    // deselection - leaves nothing for it to sit on.
    void OnSelected(Parcel parcel)
    {
        Close();
    }
}
