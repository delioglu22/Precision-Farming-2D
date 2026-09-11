using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One cell of the drone's field. Shows three things and nothing else: whether it needs
/// fertilizing right now, whether it has been tapped, and - once the run ends - whether that
/// tap was worth it. See docs/design.md, "The drone", for what each state means to the player.
///
/// The colours are flat swatches on the Image, not a texture - there is nothing repeating or
/// hand-drawn here yet, so a file would be one more asset for no visual gain.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image), typeof(Button))]
public class DroneTile : MonoBehaviour
{
    [Tooltip("Fully transparent until lit or tapped - the ground shows through untouched.")]
    [SerializeField] private Color emptyColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color litColor = new Color(0.86f, 0.16f, 0.16f, 0.55f);
    [SerializeField] private Color wetColor = new Color(0.05f, 0.03f, 0.02f, 0.55f);
    [SerializeField] private Color correctColor = new Color(0.25f, 0.75f, 0.25f, 0.55f);
    [SerializeField] private Color wrongColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    Image image;
    Button button;
    DroneField owner;
    int index;

    public bool Wet { get; private set; }
    public bool Lit { get; private set; }
    public bool IsNeeded { get; private set; }

    void Awake()
    {
        image = GetComponent<Image>();
        button = GetComponent<Button>();
        // The field drives every colour this tile shows - the button's own tint transition
        // would fight it on every hover and press.
        button.transition = Selectable.Transition.None;
        image.color = emptyColor;
        button.onClick.AddListener(OnClick);
    }

    public void Init(DroneField field, int cellIndex)
    {
        owner = field;
        index = cellIndex;
        Wet = false;
        Lit = false;
        IsNeeded = false;
        image.color = emptyColor;
    }

    /// <summary>Whether this tile is one of the parcel's needed cells, lit or not yet.</summary>
    public void SetNeeded(bool needed)
    {
        IsNeeded = needed;
    }

    public void SetLit(bool lit)
    {
        Lit = lit;
        if (!Wet) image.color = lit ? litColor : emptyColor;
    }

    /// <summary>Not prevented on a second tap - the mark is already down, so it costs nothing to allow.</summary>
    public void MarkWet()
    {
        if (Wet) return;
        Wet = true;
        image.color = wetColor;
    }

    /// <summary>Recolours a tapped tile once the run ends. Untapped tiles stay as they are.</summary>
    public void Reveal(bool correct)
    {
        if (!Wet) return;
        image.color = correct ? correctColor : wrongColor;
    }

    void OnClick()
    {
        if (owner != null) owner.OnTileTapped(index);
    }
}
