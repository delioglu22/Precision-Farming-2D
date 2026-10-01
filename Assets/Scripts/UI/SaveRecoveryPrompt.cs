using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shown only when a save file exists but cannot be loaded. It explains that the old file
/// is kept, and offers the one way forward: start a new farm.
/// </summary>
[DisallowMultipleComponent]
public class SaveRecoveryPrompt : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FarmChannel farmChannel;
    [SerializeField] private GameObject prompt;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private Button newFarmButton;

    private void OnEnable() { if (newFarmButton != null) newFarmButton.onClick.AddListener(StartNewFarm); }
    private void OnDisable() { if (newFarmButton != null) newFarmButton.onClick.RemoveListener(StartNewFarm); }

    private void Update()
    {
        Farm farm = farmChannel != null ? farmChannel.Current : null;
        bool show = farm != null && farm.LoadProblem != null;
        if (prompt.activeSelf != show) prompt.SetActive(show);
        if (show && detail != null) detail.text = farm.LoadProblem + " It has been kept as it is.";
    }

    private void StartNewFarm()
    {
        Farm farm = farmChannel != null ? farmChannel.Current : null;
        if (farm != null) farm.StartNewFarm();
    }
}
