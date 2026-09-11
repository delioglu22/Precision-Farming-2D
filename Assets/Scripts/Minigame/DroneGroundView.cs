using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the parcel's ground for the drone's field - the same bake as the seeder's own ground,
/// sized to the run's footprint. Picture only: no tiles, no taps, no score. The tap-to-fertilize
/// grid described in docs/design.md ("The drone") is a separate piece, built once its own
/// numbers are ready.
/// </summary>
[DisallowMultipleComponent]
public class DroneGroundView : MonoBehaviour
{
    [Tooltip("The run this scene is playing. Only its footprint is read here.")]
    [SerializeField] private DroneRun run;

    [Tooltip("RawImage the baked window is drawn into.")]
    [SerializeField] private RawImage ground;

    [Tooltip("The soil photo the window is cropped from. The seeder's own Seeder_Dirt.jpg, so both machines work the same dirt.")]
    [SerializeField] private Texture2D dirtSource;

    Texture2D baked;

    void Start()
    {
        Vector2Int footprint = run != null ? run.Footprint : new Vector2Int(5, 4);
        ParcelGroundBaker.Settings settings = ParcelGroundBaker.Settings.Default(dirtSource);
        ParcelGroundBaker.Result result = ParcelGroundBaker.Bake(footprint, settings, baked);
        baked = result.texture;
        if (ground == null || baked == null) return;

        RectTransform rt = ground.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(result.width, result.height);
        rt.anchoredPosition = Vector2.zero;
        ground.texture = baked;
    }

    void OnDestroy()
    {
        if (baked != null) Destroy(baked);
    }
}
