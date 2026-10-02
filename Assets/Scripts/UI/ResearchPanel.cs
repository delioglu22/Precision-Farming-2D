using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The research tree: four branches of authored node cards, each showing its cost, effect and
/// whether it is owned, available or still locked (and why). Buying needs a second tap, like
/// every other purchase, and goes through <see cref="Farm.BuyResearch"/>, which checks the
/// prerequisites and the spendable money again.
///
/// The cards and their connecting lines are laid out in the Inspector; this script only fills
/// them in. The open button appears after the first cabbage harvest.
/// </summary>
[DisallowMultipleComponent]
public class ResearchPanel : MonoBehaviour
{
    [System.Serializable]
    public class NodeCard
    {
        [Tooltip("Must match a research id in the farm rules.")]
        public string id;
        public Button button;
        public TMP_Text title;
        public TMP_Text detail;
    }

    [Header("References")]
    [SerializeField] private FarmChannel farmChannel;
    [SerializeField] private GameObject panel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text money;

    [Header("Cards")]
    [SerializeField] private NodeCard[] cards = new NodeCard[0];

    [Header("Looks")]
    [SerializeField] private Sprite ownedSprite;
    [SerializeField] private Sprite availableSprite;
    [SerializeField] private Sprite lockedSprite;

    [Header("Confirmation")]
    [SerializeField] private float confirmSeconds = 4f;
    [SerializeField] private float cooldownSeconds = 1f;

    private string armed;
    private float armedUntil, quietUntil;

    private Farm CurrentFarm { get { return farmChannel != null ? farmChannel.Current : null; } }

    private void OnEnable()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        foreach (NodeCard c in cards)
        {
            NodeCard card = c;
            if (card.button != null) card.button.onClick.AddListener(() => OnCard(card.id));
        }
    }

    private void OnDisable()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Open);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        foreach (NodeCard c in cards) if (c.button != null) c.button.onClick.RemoveAllListeners();
    }

    public void Open() { if (panel != null) panel.SetActive(true); }
    public void Close() { if (panel != null) panel.SetActive(false); armed = null; }

    private void OnCard(string id)
    {
        Farm farm = CurrentFarm;
        if (farm == null || Time.unscaledTime < quietUntil) return;
        if (farm.WhyCannotResearch(id) != null) return;
        if (armed != id) { armed = id; armedUntil = Time.unscaledTime + confirmSeconds; return; }
        armed = null;
        if (farm.BuyResearch(id)) quietUntil = Time.unscaledTime + cooldownSeconds;
    }

    private void Update()
    {
        Farm farm = CurrentFarm;
        bool unlocked = farm != null && farm.LoadProblem == null && farm.CashCompletedTotal > 0;
        if (openButton != null && openButton.gameObject.activeSelf != unlocked) openButton.gameObject.SetActive(unlocked);
        if (!unlocked && panel != null && panel.activeSelf) panel.SetActive(false);
        if (farm == null || panel == null || !panel.activeSelf) return;
        if (armed != null && Time.unscaledTime > armedUntil) armed = null;

        if (money != null)
            money.text = "Coins " + farm.Coins + " · kept for running fields " + Mathf.Min(farm.Coins, farm.ProtectedTotal)
                + " · <b>free to spend " + farm.Spendable + "</b>";

        foreach (NodeCard card in cards) ShowCard(farm, card);
    }

    private void ShowCard(Farm farm, NodeCard card)
    {
        FarmRules.ResearchNode node = farm.Rules.FindResearch(card.id);
        if (node == null) return;
        string requirement;
        Farm.ResearchState state = farm.StateOf(node, out requirement);
        string why = farm.WhyCannotResearch(card.id);

        if (card.title != null) card.title.text = node.title;
        string line;
        if (state == Farm.ResearchState.Owned) line = "<b>Owned</b>";
        else if (state == Farm.ResearchState.Locked) line = "<color=#8A3D3B>" + requirement + "</color>";
        else if (armed == card.id) line = "<b>Tap again to buy for " + node.price + "</b>";
        else if (why != null) line = node.price + " coins · <color=#8A3D3B>" + why + "</color>";
        else line = "<b>" + node.price + " coins</b> · tap to buy";
        if (card.detail != null) card.detail.text = node.effect + "\n" + line;

        if (card.button != null)
        {
            card.button.interactable = state == Farm.ResearchState.Available;
            Image image = card.button.image;
            if (image != null)
                image.sprite = state == Farm.ResearchState.Owned ? ownedSprite : state == Farm.ResearchState.Available ? availableSprite : lockedSprite;
        }
    }
}
