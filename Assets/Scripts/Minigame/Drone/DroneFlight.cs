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
/// This script only drives the spray - it does not build it. <see cref="sprayCamera"/>,
/// <see cref="spray"/> and <see cref="sprayTexture"/> are real objects placed in this scene and
/// wired here in the Inspector, tunable there without touching code. A real ParticleSystem
/// cannot draw above a Screen Space - Overlay canvas (Overlay composites after every camera in
/// the scene), so the spray camera renders to <see cref="sprayTexture"/> instead, and
/// <see cref="sprayDisplay"/> - an ordinary RawImage inside the canvas - shows that texture,
/// composited like any other piece of UI.
/// </summary>
[DisallowMultipleComponent]
public class DroneFlight : MonoBehaviour
{
    [Tooltip("The drone's own RectTransform, moved to hover over whichever tile it is visiting.")]
    [SerializeField] private RectTransform drone;

    [Tooltip("Where the drone sits between visits.")]
    [SerializeField] private Vector2 restPosition = Vector2.zero;

    [Header("Spray (placed in the scene, not built here)")]
    [Tooltip("Renders the spray particles to sprayTexture. Its own orthographic size and culling mask live on it, not here.")]
    [SerializeField] private Camera sprayCamera;

    [Tooltip("The spray effect itself - tune its colour, size, burst count and shape on this object.")]
    [SerializeField] private ParticleSystem spray;

    [Tooltip("The RenderTexture asset sprayCamera renders into and sprayDisplay shows.")]
    [SerializeField] private RenderTexture sprayTexture;

    [Tooltip("The RawImage inside this canvas that displays sprayTexture. Resized to frame the grid, same window the ground and tiles use.")]
    [SerializeField] private RawImage sprayDisplay;

    [SerializeField, Min(0.05f)] private float flySeconds = 0.35f;
    [SerializeField, Min(0.05f)] private float spraySeconds = 0.3f;

    [Tooltip("RenderTexture pixels per grid cell. Purely a resolution/quality knob.")]
    [SerializeField, Min(8)] private int pixelsPerCellInTexture = 64;

    Vector2 gridSize = new Vector2(600f, 840f);

    readonly Queue<RectTransform> pending = new Queue<RectTransform>();
    Coroutine routine;

    void ResizeTexture()
    {
        if (sprayTexture == null || sprayCamera == null) return;

        int width = Mathf.Max(8, Mathf.RoundToInt(pixelsPerCellInTexture * (gridSize.x / 120f)));
        int height = Mathf.Max(8, Mathf.RoundToInt(pixelsPerCellInTexture * (gridSize.y / 120f)));
        // Only the texture itself is expensive to redo - skip that when a previous run already
        // left it at the right size. The display rect below is cheap and has to run every time
        // regardless, or a texture that happened to already be the right size would leave
        // sprayDisplay at whatever it was last (in the scene's authored default, or a smaller
        // previous run's grid) - every spray would then render into that stale, wrong-sized
        // window instead of the current run's grid.
        if (sprayTexture.width != width || sprayTexture.height != height)
        {
            sprayTexture.Release();
            sprayTexture.width = width;
            sprayTexture.height = height;
            sprayTexture.Create();
            sprayCamera.aspect = gridSize.x / gridSize.y;
        }

        if (sprayDisplay != null)
        {
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
        ResizeTexture();
    }

    /// <summary>Queues a visit to this tile. Called by DroneField on a tap, right or wrong.</summary>
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
    /// camera's own world space, so the particle lands at the matching spot inside the
    /// RenderTexture regardless of how large the actual parcel's grid is in pixels.
    /// </summary>
    void SprayAt(Vector2 tileAnchoredPosition)
    {
        if (sprayCamera == null || spray == null) return;

        float nx = gridSize.x > 0f ? tileAnchoredPosition.x / gridSize.x : 0f;
        float ny = gridSize.y > 0f ? tileAnchoredPosition.y / gridSize.y : 0f;

        float worldHalfHeight = sprayCamera.orthographicSize;
        float worldHalfWidth = worldHalfHeight * sprayCamera.aspect;

        Vector3 worldPos = new Vector3(nx * worldHalfWidth * 2f, ny * worldHalfHeight * 2f, 0f);
        spray.transform.position = worldPos;
        // Play() on a system still mid-burst from the last tile is a no-op - the drone's own
        // visit cycle is shorter than the spray's lifetime, so without this every tap after the
        // first would just move a silent emitter instead of triggering a new burst.
        spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        spray.Play();
    }
}
