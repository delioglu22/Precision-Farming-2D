using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Sends the drone out to the parcel the player is looking at. Mirrors
/// <see cref="SeederLauncher"/> - see its own comments for why a scene load rather than a
/// panel, and why the ticket is filled in before the load.
/// </summary>
[DisallowMultipleComponent]
public class DroneLauncher : MonoBehaviour
{
    [Tooltip("Picks are announced here. Whichever parcel is held is the one that gets fertilized.")]
    [SerializeField] private ParcelSelectionChannel channel;

    [Tooltip("The ticket the mini game reads when it opens.")]
    [SerializeField] private DroneRun run;

    [Tooltip("The mini game's scene. Must be listed in the build settings.")]
    [SerializeField] private string scene = "Fertilizer";

    [Tooltip("Where the drone's last result is shown on the parcel's page. Optional.")]
    [SerializeField] private TMP_Text score;

    Parcel held;

    void OnEnable()
    {
        if (channel != null) channel.Selected += OnSelected;
        if (run != null) run.Finished += OnFinished;
    }

    void OnDisable()
    {
        if (channel != null) channel.Selected -= OnSelected;
        if (run != null) run.Finished -= OnFinished;
    }

    // What the mini game managed comes back along the ticket, because a scene cannot hold a
    // reference into another one.
    void OnFinished(float scored)
    {
        if (score != null) score.text = Mathf.RoundToInt(scored * 100f) + "%";
    }

    void OnSelected(Parcel parcel)
    {
        held = parcel;
    }

    /// <summary>
    /// Opens the mini game over the game. The Optimize button on the drone's row calls this
    /// straight from the Inspector.
    /// </summary>
    public void Open()
    {
        if (held == null || run == null || string.IsNullOrEmpty(scene)) return;

        run.Send(held.Footprint, held.DisplayName);

        // A second copy would stack another canvas on the first.
        Scene already = SceneManager.GetSceneByName(scene);
        if (already.IsValid() && already.isLoaded) return;

        SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
    }
}
