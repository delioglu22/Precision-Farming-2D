using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Sends the seeder out to the parcel the player is looking at.
///
/// The mini game is its own scene, so opening it is a scene load rather than a panel sliding
/// up. Unity does the load - SceneManager - but has nothing a Button can call from the
/// Inspector with a scene name, which is the same gap SceneBootstrap fills for the UI scene.
///
/// The ticket is filled in before the load and not after, because the mini game reads its
/// footprint the moment it starts: by the time Seeder.unity is up, what it needs has to
/// already be sitting in the asset.
/// </summary>
[DisallowMultipleComponent]
public class SeederLauncher : MonoBehaviour
{
    [Tooltip("Picks are announced here. Whichever parcel is held is the one that gets seeded.")]
    [SerializeField] ParcelSelectionChannel channel;

    [Tooltip("The ticket the mini game reads when it opens.")]
    [SerializeField] SeederRun run;

    [Tooltip("The mini game's scene. Must be listed in the build settings.")]
    [SerializeField] string scene = "Seeder";

    [Tooltip("Where the seeder's last result is shown on the parcel's page. Optional.")]
    [SerializeField] Text coverage;

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
    void OnFinished(float sown)
    {
        if (coverage != null) coverage.text = Mathf.RoundToInt(sown * 100f) + "%";
    }

    void OnSelected(Parcel parcel)
    {
        held = parcel;
    }

    /// <summary>
    /// Opens the mini game over the game. The Optimize button on the seeder's row calls this
    /// straight from the Inspector.
    /// </summary>
    public void Open()
    {
        if (held == null || run == null || string.IsNullOrEmpty(scene)) return;

        ParcelFootprint shape = held.GetComponent<ParcelFootprint>();
        if (shape == null) return;

        // A second copy would stack another camera and another canvas on the first.
        Scene already = SceneManager.GetSceneByName(scene);
        if (already.IsValid() && already.isLoaded) return;

        run.Send(shape.Footprint, held.DisplayName);
        SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
    }
}
