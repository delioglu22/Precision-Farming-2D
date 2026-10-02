using UnityEngine;
using TMPro;

/// <summary>
/// Shows the farm's coins and how many of them are free to spend on land, buildings, research
/// and controllers (the rest is kept for running the fields). Reads only.
/// </summary>
[DisallowMultipleComponent]
public class CoinsLabel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FarmChannel farmChannel;
    [SerializeField] private TMP_Text label;

    private int shownCoins = int.MinValue, shownSpendable = int.MinValue;

    private void Update()
    {
        Farm farm = farmChannel != null ? farmChannel.Current : null;
        if (farm == null || label == null) return;
        if (farm.Coins == shownCoins && farm.Spendable == shownSpendable) return;
        shownCoins = farm.Coins;
        shownSpendable = farm.Spendable;
        label.text = shownCoins + " coins\n<size=60%>" + shownSpendable + " free to spend</size>";
    }
}
