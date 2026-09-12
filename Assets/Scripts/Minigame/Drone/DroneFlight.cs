using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flies the drone to a tile, sprays it, and leaves the way it came - the visual reward for a
/// correct tap. See docs/design.md, "The drone": landing and takeoff are movement, not baked
/// into the sprite, and the spray is Unity's own particle system, not embedded in the art. The
/// hover/sway loop keeps playing on the Animator throughout; this only moves the RectTransform.
/// Requests queue instead of interrupting each other, so two quick correct taps don't teleport
/// the drone mid-flight.
///
/// A real ParticleSystem cannot draw above a Screen Space - Overlay canvas: Overlay composites
/// after every camera in the scene, so nothing a camera renders - the particle system included -
/// can ever appear in front of it. Switching the canvas to Screen Space - Camera was tried (see
/// git history) so the particle could share the same camera pass as the UI, but that broke the
/// field outright once tested for real: the canvas's own CanvasScaler disagreed with the actual
/// screen size in a way this session never fully pinned down.
///
/// This keeps Overlay - the working, proven mode, matching Seeder.unity - and gives the particle
/// system its own small, dedicated camera on its own layer ("Spray VFX"), rendering to a
/// RenderTexture that an ordinary RawImage inside the Overlay canvas displays. That RawImage is
/// just another piece of UI, so it composites correctly like everything else; the real
/// ParticleSystem never has to touch the canvas's render mode at all.
/// </summary>
[DisallowMultipleComponent]
public class DroneFlight : MonoBehaviour
{
    [Tooltip("The drone's own RectTransform, moved to hover over whichever tile it is visiting.")]
    [SerializeField] private RectTransform drone;

    [Tooltip("Where the drone sits between visits.")]
    [SerializeField] private Vector2 restPosition = Vector2.zero;

    [Tooltip("The RawImage the spray's RenderTexture is drawn into. Resized to frame the grid, same window the ground and tiles use.")]
    [SerializeField] private RawImage sprayDisplay;

    [SerializeField, Min(0.05f)] private float flySeconds = 0.35f;
    [SerializeField, Min(0.05f)] private float spraySeconds = 0.3f;

    [Tooltip("Half-height of the spray camera's view, in world units - purely an internal scale for the particle system, unrelated to canvas pixels.")]
    [SerializeField, Min(0.5f)] private float cameraHalfHeight = 5f;

    [Tooltip("RenderTexture pixels per grid cell. Purely a resolution/quality knob.")]
    [SerializeField, Min(8)] private int pixelsPerCellInTexture = 64;

    Camera sprayCamera;
    RenderTexture sprayTexture;
    ParticleSystem spray;
    Vector2 gridSize = new Vector2(600f, 840f);

    readonly Queue<RectTransform> pending = new Queue<RectTransform>();
    Coroutine routine;

    void Awake()
    {
        int sprayLayer = LayerMask.NameToLayer("Spray VFX");

        GameObject camGo = new GameObject("Spray Camera");
        sprayCamera = camGo.AddComponent<Camera>();
        sprayCamera.orthographic = true;
        sprayCamera.orthographicSize = cameraHalfHeight;
        sprayCamera.cullingMask = 1 << sprayLayer;
        sprayCamera.clearFlags = CameraClearFlags.SolidColor;
        sprayCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        sprayCamera.nearClipPlane = 0.1f;
        sprayCamera.farClipPlane = 20f;
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        GameObject psGo = new GameObject("Spray VFX");
        psGo.layer = sprayLayer;
        spray = psGo.AddComponent<ParticleSystem>();
        spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = spray.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.startLifetime = 0.4f;
        main.startSpeed = 1.5f;
        main.startSize = 0.35f;
        main.startColor = new Color(0.65f, 0.85f, 1f, 0.95f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = spray.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });

        ParticleSystem.ShapeModule shape = spray.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.15f;

        ParticleSystemRenderer renderer = spray.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 10;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null) renderer.material = new Material(shader);

        BuildTexture();
    }

    void BuildTexture()
    {
        int width = Mathf.Max(8, Mathf.RoundToInt(pixelsPerCellInTexture * (gridSize.x / 120f)));
        int height = Mathf.Max(8, Mathf.RoundToInt(pixelsPerCellInTexture * (gridSize.y / 120f)));

        if (sprayTexture != null) { sprayTexture.Release(); Destroy(sprayTexture); }
        sprayTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
        sprayTexture.Create();
        sprayCamera.targetTexture = sprayTexture;
        sprayCamera.aspect = gridSize.x / gridSize.y;

        if (sprayDisplay != null)
        {
            sprayDisplay.texture = sprayTexture;
            RectTransform rt = sprayDisplay.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = gridSize;
            rt.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>Called by DroneField once it knows the run's grid size, in canvas pixels.</summary>
    public void Configure(Vector2 gridSizeInCanvasPixels)
    {
        gridSize = gridSizeInCanvasPixels;
        if (sprayCamera != null) BuildTexture();
    }

    /// <summary>Queues a visit to this tile. Called by DroneField on a correct, new tap.</summary>
    public void Visit(RectTransform tile)
    {
        pending.Enqueue(tile);
        if (routine == null) routine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        while (pending.Count > 0)
        {
            RectTransform tile = pending.Dequeue();
            if (tile == null) continue;

            Vector2 target = tile.anchoredPosition;
            yield return Move(drone.anchoredPosition, target, flySeconds);

            SprayAt(target);
            yield return new WaitForSeconds(spraySeconds);

            yield return Move(drone.anchoredPosition, restPosition, flySeconds);
        }
        routine = null;
    }

    IEnumerator Move(Vector2 from, Vector2 to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            drone.anchoredPosition = Vector2.Lerp(from, to, Mathf.Clamp01(t / seconds));
            yield return null;
        }
        drone.anchoredPosition = to;
    }

    /// <summary>
    /// Maps a tile's canvas position (relative to the grid's own centre) onto the spray
    /// camera's small, fixed world space, so the particle lands at the matching spot inside
    /// the RenderTexture regardless of how large the actual parcel's grid is in pixels.
    /// </summary>
    void SprayAt(Vector2 tileAnchoredPosition)
    {
        float nx = gridSize.x > 0f ? tileAnchoredPosition.x / gridSize.x : 0f;
        float ny = gridSize.y > 0f ? tileAnchoredPosition.y / gridSize.y : 0f;

        float worldHalfHeight = sprayCamera.orthographicSize;
        float worldHalfWidth = worldHalfHeight * sprayCamera.aspect;

        Vector3 worldPos = new Vector3(nx * worldHalfWidth * 2f, ny * worldHalfHeight * 2f, 0f);
        spray.transform.position = worldPos;
        spray.Play();
    }

    void OnDestroy()
    {
        if (sprayTexture != null) { sprayTexture.Release(); Destroy(sprayTexture); }
    }
}
