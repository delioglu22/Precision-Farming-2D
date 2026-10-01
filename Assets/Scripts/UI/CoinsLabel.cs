using UnityEngine;
using TMPro;

/// <summary>Shows the farm's coins. Reads only; money changes in <see cref="Farm"/>.</summary>
[DisallowMultipleComponent]
public class CoinsLabel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FarmChannel farmChannel;
    [SerializeField] private TMP_Text label;

    private int shown = int.MinValue;

    private void Update()
    {
        Farm farm = farmChannel != null ? farmChannel.Current : null;
        if (farm == null || label == null || farm.Coins == shown) return;
        shown = farm.Coins;
        label.text = shown + " coins";
    }
}
