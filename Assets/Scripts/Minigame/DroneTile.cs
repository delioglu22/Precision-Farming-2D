using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One cell of the drone's field. All it knows right now is whether it has been tapped - the
/// same wet mark either way, since docs/design.md says a tap looks identical whether it was
/// right or wrong until the run ends. Which tiles need fertilizing, grouping and score are a
/// separate piece, waiting on numbers docs/design.md still leaves open.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image), typeof(Button))]
public class DroneTile : MonoBehaviour
{
    [Tooltip("Fully transparent until tapped - the ground shows through untouched.")]
    [SerializeField] private Color emptyColor = new Color(0f, 0f, 0f, 0f);

    [SerializeField] private Color wetColor = new Color(0.05f, 0.03f, 0.02f, 0.55f);

    Image image;
    Button button;

    public bool Wet { get; private set; }

    void Awake()
    {
        image = GetComponent<Image>();
        button = GetComponent<Button>();
        // The tile drives its own colour - the button's built-in tint would fight it.
        button.transition = Selectable.Transition.None;
        image.color = emptyColor;
        button.onClick.AddListener(MarkWet);
    }

    /// <summary>Not prevented on a second tap - the mark is already down, so it costs nothing to allow.</summary>
    void MarkWet()
    {
        if (Wet) return;
        Wet = true;
        image.color = wetColor;
    }
}
