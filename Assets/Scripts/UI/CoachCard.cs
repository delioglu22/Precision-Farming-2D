using UnityEngine;
using TMPro;

/// <summary>
/// Shows the introduction's current hint in a small card. It sits at the top of the screen
/// while the small sheet is up, and at the bottom otherwise, so it never covers what the hint
/// is talking about. Anchors do the placing; this only picks which set to use.
/// </summary>
[DisallowMultipleComponent]
public class CoachCard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FarmChannel farmChannel;
    [SerializeField] private ParcelPanel panel;
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text message;

    [Header("Placement")]
    [Tooltip("Distance from the screen edge, in canvas units.")]
    [SerializeField] private float topMargin = 170f;
    [SerializeField] private float bottomMargin = 40f;

    private void Update()
    {
        Farm farm = farmChannel != null ? farmChannel.Current : null;
        // The recovery prompt speaks alone; a farming hint under it would only confuse.
        string text = farm != null && farm.Intro != null && farm.LoadProblem == null ? farm.Intro.Message : string.Empty;

        bool show = !string.IsNullOrEmpty(text);
        if (card.gameObject.activeSelf != show) card.gameObject.SetActive(show);
        if (!show) return;
        if (message.text != text) message.text = text;

        bool sheetOnly = panel != null && panel.IsOpen && !panel.IsExpanded;
        float y = sheetOnly ? 1f : 0f;
        card.anchorMin = new Vector2(0f, y);
        card.anchorMax = new Vector2(1f, y);
        card.pivot = new Vector2(0.5f, y);
        card.anchoredPosition = new Vector2(0f, sheetOnly ? -topMargin : bottomMargin);
    }
}
