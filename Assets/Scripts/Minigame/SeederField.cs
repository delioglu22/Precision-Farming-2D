using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The seeder's playfield: the parcel it was sent to, seen from a pure top-down perspective.
///
/// Unlike the map, the ground here is not a picture placed on the grass - it is a window cut into
/// it. <see cref="Build"/> bakes a torn-edged, shadowed crop of <see cref="dirtSource"/> sized to the
/// parcel's own footprint (<c>footprint * cellPixels</c> pixels, clamped so it can never grow past the
/// canvas), leaving the rest of the texture transparent so the scene's own grass shows through. The
/// grass is a separate, static object behind this one - this script never touches it.
///
/// As the player drags across the soil, a thin bright neon central line is drawn with a glowing
/// phosphor (highlighter) aura around it - untouched while drawing. Only once the line is finished
/// does the machine set out along it (see <see cref="TravelPath"/>): as it passes, it wipes the
/// marker back to plain dirt and leaves a seed mark behind instead.
/// </summary>
[DisallowMultipleComponent]
public class SeederField : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("The run this scene is playing. Everything the field needs is in here.")]
    [SerializeField] private SeederRun run;

    [Header("Ground Display")]
    [Tooltip("RawImage the baked window is drawn into. Its RectTransform is resized to match every time.")]
    [SerializeField] private RawImage ground;

    [Header("UI & Scoring")]
    [Tooltip("Percentage text display (top right).")]
    [SerializeField] private TMP_Text result;

    [Tooltip("Battery gauge image at the bottom.")]
    [SerializeField] private Image battery;

    [Tooltip("Optional field title.")]
    [SerializeField] private TMP_Text title;

    [Header("Machine")]
    [Tooltip("Vehicle that rides along the drawn line while driving.")]
    [SerializeField] private RectTransform machine;

    [Tooltip("Drives the machine's wheel-spin loop through the 'Driving' bool.")]
    [SerializeField] private Animator machineAnimator;

    [Tooltip("The machine's width, in grid cells.")]
    [SerializeField, Min(0.1f)] private float machineCellsWide = 1.5f;

    [Tooltip("The machine sprite's own width-to-height ratio, so it keeps its shape at any size.")]
    [SerializeField, Min(0.1f)] private float machineAspect = 352f / 206f;

    [Tooltip("How fast the machine retraces the finished line, in grid cells per second.")]
    [SerializeField, Min(0.1f)] private float machineSpeed = 6f;

    [Tooltip("How tightly the traced path is rounded off before the machine follows it - a hand-drawn line is never straight.")]
    [SerializeField, Min(0f)] private int pathSmoothingPasses = 2;

    [Tooltip("How fast the machine turns to face its new heading, in degrees per second.")]
    [SerializeField, Min(1f)] private float machineTurnSpeed = 480f;

    [Header("Seeding (left behind by the traveling machine)")]
    [Tooltip("The seed mark stamped behind the machine as it drives the finished line (Read/Write must be enabled).")]
    [SerializeField] private Texture2D seedSprite;

    [Tooltip("Distance between two seed marks, in grid cells.")]
    [SerializeField, Min(0.05f)] private float seedSpacingCells = 0.5f;

    [Tooltip("A seed mark's width, in grid cells.")]
    [SerializeField, Min(0.05f)] private float seedCellsWide = 0.4f;

    [Tooltip("Maximum random tilt on each seed mark, in degrees, so a run doesn't look machine-stamped.")]
    [SerializeField, Range(0f, 90f)] private float seedRotationJitter = 25f;

    [Header("Field Parameters")]
    [Tooltip("The soil photo the window is cropped from (Read/Write must be enabled).")]
    [SerializeField] private Texture2D dirtSource;

    [Tooltip("Pixels per cell, both axes - the same unit the map's canvas already uses.")]
    [SerializeField, Min(1f)] private float cellPixels = 120f;

    [Tooltip("Grass left showing on the window's left and right, in canvas pixels.")]
    [SerializeField, Min(0f)] private float sideMargin = 60f;

    [Tooltip("Grass left showing above and below the window, in canvas pixels.")]
    [SerializeField, Min(0f)] private float topBottomMargin = 120f;

    [Tooltip("How far the torn edge's teeth can reach past the window's clean rectangle.")]
    [SerializeField, Min(0f)] private float tornBleed = 40f;

    [Tooltip("How much line the machine carries, in grid cells.")]
    [SerializeField, Min(1f)] private float batteryCells = 48f;

    [Header("Torn Edge")]
    [SerializeField] private float noiseFreq1 = 0.03f;
    [SerializeField] private float noiseWeight1 = 0.35f;
    [SerializeField] private float noiseFreq2 = 0.13f;
    [SerializeField] private float noiseWeight2 = 1.0f;
    [Tooltip("How far the noise displaces the edge, in pixels.")]
    [SerializeField] private float noiseAmplitude = 15f;
    [Tooltip("Width of the antialiased band at the torn edge, in pixels.")]
    [SerializeField, Min(0.5f)] private float edgeSoftness = 4f;

    [Header("Ambient Occlusion")]
    [Tooltip("Shadow tint the torn edge fades to, cast by the raised grass lip.")]
    [SerializeField] private Color shadowColor = new Color(0.094f, 0.063f, 0.043f, 1f);
    [Tooltip("How far the shadow reaches in from the edge, in pixels.")]
    [SerializeField] private float aoFade = 105f;
    [Tooltip("Shadow strength at the very edge, 0-1.")]
    [SerializeField, Range(0f, 1f)] private float aoMax = 0.38f;

    [Header("Drawing Style (Phosphor & Center Line)")]
    [Tooltip("How wide the phosphor glow aura is, in grid cells.")]
    [SerializeField, Range(0.2f, 2f)] private float bandCells = 0.95f;

    [Tooltip("Thickness of the central matte yellow line in pixels/texels.")]
    [SerializeField, Range(0.5f, 5f)] private float centerLineWidth = 1.4f;

    [Tooltip("Length of each dash of the center line, in pixels/texels.")]
    [SerializeField, Min(1f)] private float dashLength = 30f;

    [Tooltip("Length of each gap between dashes, in pixels/texels.")]
    [SerializeField, Min(1f)] private float dashGapLength = 22f;

    [Tooltip("Thickness of the outline drawn around the phosphor band's own edge, in pixels/texels.")]
    [SerializeField, Min(0f)] private float outlineWidth = 6f;

    [Tooltip("Color of the outline around the phosphor band's edge.")]
    [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 1f);

    [Tooltip("Color of the central matte line.")]
    [SerializeField] private Color centerLineColor = new Color(1f, 1f, 1f, 1f);

    [Tooltip("Color of the lighter transparent phosphor band.")]
    [SerializeField] private Color phosphorGlowColor = new Color(1f, 1f, 1f, 0.44f);

    [Tooltip("Stroke color that spilled past the parcel, onto the torn edge or bare grass.")]
    [SerializeField] private Color spilledGlow = new Color(1f, 1f, 1f, 0.28f);

    const float CanvasWidth = 1080f;
    const float CanvasHeight = 1920f;

    Texture2D field;
    Color32[] pixels;
    Color32[] basePixels;

    bool[] fenced;
    int fencedCount;

    bool[] opaque;
    bool[] seeded;
    bool[] isCoreLine;
    bool[] isOutline;
    bool[] hasSeed;
    float[] minDist;
    int sownCount;

    int width;
    int height;
    float texelsPerCell;

    bool driving;
    bool spent;
    Vector2 lastTexel;
    float lineLeft;
    float lineTraveled;

    readonly List<Vector2> path = new List<Vector2>();
    readonly List<Vector2> smoothPath = new List<Vector2>();
    Coroutine travelRoutine;

    bool machineVertical;
    bool machineFacingLeft;
    float machineAngle;
    float machineTargetAngle;

    /// <summary>The share of the parcel that has seed on it, from 0 to 1.</summary>
    public float Coverage
    {
        get { return fencedCount <= 0 ? 0f : (float)sownCount / fencedCount; }
    }

    /// <summary>Cells of line still in the tank.</summary>
    public float LineLeft { get { return lineLeft; } }

    /// <summary>Whether the run is over, by a lifted finger or by a dry tank.</summary>
    public bool Spent { get { return spent; } }

    void Start()
    {
        Begin();
    }

    void OnDestroy()
    {
        if (run != null && sownCount > 0 && !spent)
        {
            run.Report(Coverage);
        }
        Discard(field);
    }

    /// <summary>Lays the run's parcel out as ground, ready to be driven over.</summary>
    public void Begin()
    {
        Vector2Int footprint = run != null ? run.Footprint : new Vector2Int(5, 4);
        if (title != null && run != null) title.text = run.ParcelName;
        Build(footprint);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (spent || field == null) return;

        Vector2 texel;
        if (!ScreenPointToTexel(eventData.position, out texel)) return;

        driving = true;
        lastTexel = texel;
        path.Clear();
        path.Add(texel);

        Stamp(texel, texel);
        Show();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (spent || field == null) return;
        if (driving) return;

        Vector2 texel;
        if (!ScreenPointToTexel(eventData.position, out texel)) return;

        driving = true;
        lastTexel = texel;
        path.Clear();
        path.Add(texel);

        Stamp(texel, texel);
        Show();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!driving || spent) return;

        Vector2 texel;
        if (!ScreenPointToTexel(eventData.position, out texel)) return;

        Drive(texel);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EndDrive();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        EndDrive();
    }

    void EndDrive()
    {
        if (!driving) return;

        driving = false;
        spent = true;
        Finish();
        StartTravel();
    }

    void Drive(Vector2 to)
    {
        if (lineLeft <= 0f || texelsPerCell <= 0f) return;

        float cells = Vector2.Distance(lastTexel, to) / texelsPerCell;
        if (cells <= 0f) return;

        Vector2 reached = to;
        if (cells > lineLeft)
        {
            reached = Vector2.Lerp(lastTexel, to, lineLeft / cells);
            cells = lineLeft;
        }

        Stamp(lastTexel, reached);
        path.Add(reached);

        lineTraveled += Vector2.Distance(lastTexel, reached);
        lastTexel = reached;
        lineLeft -= cells;

        bool ranOut = lineLeft <= 0f;
        if (ranOut)
        {
            lineLeft = 0f;
            driving = false;
            spent = true;
        }

        Show();
        ShowBattery();
        ShowResult();

        if (ranOut)
        {
            Finish();
            StartTravel();
        }
    }

    /// <summary>Sends the machine along the just-finished line, from start to end, once drawing is over.</summary>
    void StartTravel()
    {
        if (machine == null || path.Count < 2) return;
        if (travelRoutine != null) StopCoroutine(travelRoutine);

        smoothPath.Clear();
        smoothPath.AddRange(SmoothPath(path));
        if (smoothPath.Count < 2) return;

        travelRoutine = StartCoroutine(TravelPath());
    }

    /// <summary>
    /// A hand-drawn line is never straight - the raw path is a wobble of tiny, near-random zigzags,
    /// and a machine that chases every one of them stutters. This first drops points that are too
    /// close together to mean anything, then rounds off what is left with a couple of passes of
    /// Chaikin corner-cutting, which turns a jagged polyline into a smooth, gently rounded one while
    /// keeping the exact start and end point.
    /// </summary>
    List<Vector2> SmoothPath(List<Vector2> raw)
    {
        List<Vector2> thin = new List<Vector2>();
        thin.Add(raw[0]);
        float minSpacing = Mathf.Max(4f, texelsPerCell * 0.25f);
        for (int i = 1; i < raw.Count - 1; i++)
        {
            if (Vector2.Distance(thin[thin.Count - 1], raw[i]) >= minSpacing) thin.Add(raw[i]);
        }
        thin.Add(raw[raw.Count - 1]);
        if (thin.Count < 3) return thin;

        List<Vector2> cur = thin;
        for (int pass = 0; pass < pathSmoothingPasses; pass++)
        {
            List<Vector2> next = new List<Vector2>();
            next.Add(cur[0]);
            for (int i = 0; i < cur.Count - 1; i++)
            {
                next.Add(Vector2.Lerp(cur[i], cur[i + 1], 0.25f));
                next.Add(Vector2.Lerp(cur[i], cur[i + 1], 0.75f));
            }
            next.Add(cur[cur.Count - 1]);
            cur = next;
        }
        return cur;
    }

    IEnumerator TravelPath()
    {
        machine.gameObject.SetActive(true);
        Vector2 prevPos = smoothPath[0];
        PlaceMachine(prevPos);
        ResetOrientation(smoothPath[0], smoothPath[1]);
        SetDriving(true);

        float seedSpacing = Mathf.Max(0.05f, seedSpacingCells) * texelsPerCell;
        float traveledTotal = 0f;
        float nextSeedAt = 0f;

        SowBehind(prevPos, prevPos, traveledTotal, ref nextSeedAt, seedSpacing);
        Show();

        float speed = machineSpeed * texelsPerCell;
        for (int i = 0; i < smoothPath.Count - 1; i++)
        {
            Vector2 from = smoothPath[i];
            Vector2 to = smoothPath[i + 1];
            SetOrientTarget(from, to);

            float segLength = Vector2.Distance(from, to);
            if (segLength <= 0.0001f) continue;

            float traveled = 0f;
            while (traveled < segLength)
            {
                float dt = Time.deltaTime;
                float step = speed * dt;
                traveled += step;
                traveledTotal += step;

                Vector2 pos = Vector2.Lerp(from, to, Mathf.Clamp01(traveled / segLength));
                PlaceMachine(pos);
                ApplyOrientation(dt);
                SowBehind(prevPos, pos, traveledTotal, ref nextSeedAt, seedSpacing);
                prevPos = pos;
                Show();
                yield return null;
            }
        }

        SetDriving(false);
        travelRoutine = null;
    }

    /// <summary>
    /// Wipes the marker line out from under the machine along the stretch it just crossed - back to
    /// the plain baked dirt, swept as a whole segment so a fast frame can't skip over part of it - and,
    /// every <paramref name="seedSpacing"/> texels of travel, leaves a seed mark in its place. The
    /// marker stays untouched everywhere the machine hasn't reached yet.
    /// </summary>
    void SowBehind(Vector2 from, Vector2 to, float traveledTotal, ref float nextSeedAt, float seedSpacing)
    {
        ErasePath(from, to);
        while (traveledTotal >= nextSeedAt)
        {
            StampSeed(to, JitterAngle(to));
            nextSeedAt += seedSpacing;
        }
    }

    /// <summary>Moves the machine to sit over the given texel, in the ground's own local space.</summary>
    void PlaceMachine(Vector2 texel)
    {
        if (machine == null) return;
        machine.anchoredPosition = new Vector2(texel.x - width * 0.5f, texel.y - height * 0.5f);
    }

    /// <summary>Snaps the machine's heading to the very first stretch of the path, with nothing to ease from yet.</summary>
    void ResetOrientation(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        machineVertical = Mathf.Abs(dir.y) > Mathf.Abs(dir.x);
        machineFacingLeft = dir.x < 0f;
        machineAngle = machineTargetAngle = machineVertical
            ? 0f
            : Mathf.Atan2(dir.y, machineFacingLeft ? -dir.x : dir.x) * Mathf.Rad2Deg;
        SetFacing(machineVertical);
    }

    /// <summary>
    /// Picks the heading the machine should ease towards next. Switching between the side-view and
    /// front-view sprite, or between facing left and right, needs a dead zone around the switch point
    /// (a wider margin to leave a mode than to enter it) or a path running close to +/-45 degrees or
    /// straight up/down would flicker between the two every other frame.
    /// </summary>
    void SetOrientTarget(Vector2 from, Vector2 to)
    {
        if (machine == null) return;
        Vector2 dir = to - from;
        if (dir.sqrMagnitude < 0.0001f) return;

        const float hysteresis = 1.3f;
        bool vertical = machineVertical
            ? !(Mathf.Abs(dir.x) > Mathf.Abs(dir.y) * hysteresis)
            : Mathf.Abs(dir.y) > Mathf.Abs(dir.x) * hysteresis;

        if (vertical != machineVertical)
        {
            machineVertical = vertical;
            SetFacing(vertical);
        }

        if (vertical)
        {
            machineTargetAngle = 0f;
            return;
        }

        bool facingLeft = dir.x < 0f;
        float angle = Mathf.Atan2(dir.y, facingLeft ? -dir.x : dir.x) * Mathf.Rad2Deg;
        if (facingLeft != machineFacingLeft)
        {
            // Crossing from moving right to moving left (or back) is a look-away, not a turn -
            // nothing to ease through, since the mirrored art has no continuous path between them.
            machineFacingLeft = facingLeft;
            machineAngle = angle;
        }
        machineTargetAngle = angle;
    }

    /// <summary>Eases the machine's current heading towards its target at a fixed turn rate.</summary>
    void ApplyOrientation(float dt)
    {
        if (machine == null) return;

        if (machineVertical)
        {
            machine.localScale = Vector3.one;
            machine.localEulerAngles = Vector3.zero;
            return;
        }

        machineAngle = Mathf.MoveTowardsAngle(machineAngle, machineTargetAngle, machineTurnSpeed * dt);
        machine.localScale = new Vector3(machineFacingLeft ? -1f : 1f, 1f, 1f);
        machine.localEulerAngles = new Vector3(0f, 0f, machineAngle);
    }

    void SetDriving(bool isDriving)
    {
        if (machineAnimator != null) machineAnimator.SetBool("Driving", isDriving);
    }

    void SetFacing(bool vertical)
    {
        if (machineAnimator != null) machineAnimator.SetBool("Facing", vertical);
    }

    void Finish()
    {
        if (run != null) run.Report(Coverage);
        ShowResult();
    }

    void ShowResult()
    {
        if (result != null) result.text = Mathf.RoundToInt(Coverage * 100f) + "%";
    }

    void ShowBattery()
    {
        if (battery != null) battery.fillAmount = batteryCells <= 0f ? 0f : lineLeft / batteryCells;
    }

    bool ScreenPointToTexel(Vector2 screenPos, out Vector2 texel)
    {
        texel = Vector2.zero;

        RectTransform grt = ground != null ? ground.rectTransform : transform as RectTransform;
        if (grt != null)
        {
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(grt, screenPos, null, out local))
            {
                Rect r = grt.rect;
                if (r.width > 0f && r.height > 0f)
                {
                    float u = Mathf.Clamp01((local.x - r.xMin) / r.width);
                    float v = Mathf.Clamp01((local.y - r.yMin) / r.height);
                    texel = new Vector2(u * width, v * height);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Sows the band swept between two points: draws a thick transparent lighter-colored yellow phosphor
    /// marker band with a thin matte yellow center line. Where the base is the baked dirt (or one of its
    /// torn teeth) the stroke blends into it; where the base is bare, transparent grass it paints directly,
    /// since there is nothing there to blend with.
    /// </summary>
    void Stamp(Vector2 from, Vector2 to)
    {
        float radius = 0.5f * bandCells * texelsPerCell;
        if (radius <= 0f) return;

        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.x, to.x) - radius));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(from.x, to.x) + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.y, to.y) - radius));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(from.y, to.y) + radius));

        Vector2 along = to - from;
        float lengthSq = along.sqrMagnitude;
        float segmentLength = Mathf.Sqrt(lengthSq);
        float radiusSq = radius * radius;
        float coreRadius = centerLineWidth * 0.5f;
        float dashPeriod = dashLength + dashGapLength;
        float outlineInner = radius - outlineWidth;

        Color32 core32 = centerLineColor;
        core32.a = 255;
        Color32 glow32 = phosphorGlowColor;
        glow32.a = 255;
        Color32 spilled32 = spilledGlow;
        spilled32.a = 255;
        Color32 outline32 = outlineColor;
        outline32.a = 255;
        float maxGlowBlend = phosphorGlowColor.a > 0f ? phosphorGlowColor.a : 0.70f;
        float maxSpillBlend = spilledGlow.a > 0f ? spilledGlow.a : 0.45f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int i = y * width + x;

                Vector2 here = new Vector2(x + 0.5f, y + 0.5f);
                float t = lengthSq <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(here - from, along) / lengthSq);
                Vector2 nearest = from + along * t;
                float distSq = (here - nearest).sqrMagnitude;
                if (distSq > radiusSq) continue;

                if (!seeded[i])
                {
                    seeded[i] = true;
                    if (fenced[i]) sownCount++;
                }

                float dist = Mathf.Sqrt(distSq);

                // The best (smallest) distance to the whole swept path this pixel has ever seen -
                // not just this one segment - so the outline traces the path as a whole instead of
                // ringing every short segment's own little capsule.
                if (dist < minDist[i]) minDist[i] = dist;
                float md = minDist[i];

                if (isCoreLine == null || !isCoreLine[i])
                {
                    bool edge = outlineWidth > 0f && md > outlineInner;
                    if (isOutline != null) isOutline[i] = edge;

                    if (edge)
                    {
                        if (opaque[i])
                        {
                            Color32 baseColor = basePixels != null ? basePixels[i] : pixels[i];
                            baseColor.a = 255;
                            Color32 blended = Color32.Lerp(baseColor, outline32, outlineColor.a);
                            blended.a = 255;
                            pixels[i] = blended;
                        }
                        else
                        {
                            Color32 c = outline32;
                            c.a = (byte)Mathf.RoundToInt(outlineColor.a * 255f);
                            pixels[i] = c;
                        }
                    }
                    else
                    {
                        // Phosphor highlighter band with natural soft marker edge on outer 30%
                        float normDist = md / radius; // 0..1
                        float falloff = 1f;
                        if (normDist > 0.70f)
                        {
                            float ft = (normDist - 0.70f) / 0.30f;
                            falloff = 1f - (ft * ft * (3f - 2f * ft));
                        }
                        byte curAlpha = (byte)Mathf.RoundToInt(falloff * 255f);

                        if (opaque[i])
                        {
                            float baseBlend = fenced[i] ? maxGlowBlend : maxSpillBlend;
                            float blend = baseBlend * (curAlpha / 255f);
                            Color32 baseColor = basePixels != null ? basePixels[i] : pixels[i];
                            baseColor.a = 255;
                            Color32 targetGlow = fenced[i] ? glow32 : spilled32;
                            Color32 blended = Color32.Lerp(baseColor, targetGlow, blend);
                            blended.a = 255;
                            pixels[i] = blended;
                        }
                        else
                        {
                            // Nothing baked here but transparent grass - paint the mark directly, own alpha and all.
                            Color32 c = spilled32;
                            c.a = curAlpha;
                            pixels[i] = c;
                        }
                    }
                }

                // Thin matte pen line on top, broken into dashes along the stroke
                float alongDist = lineTraveled + t * segmentLength;
                bool dashOn = Mathf.Repeat(alongDist, dashPeriod) < dashLength;

                if (dashOn)
                {
                    if (dist <= coreRadius)
                    {
                        if (isCoreLine != null) isCoreLine[i] = true;
                        Color32 c = core32;
                        c.a = 255;
                        pixels[i] = c;
                    }
                    else if (dist <= coreRadius + 0.5f)
                    {
                        if (isCoreLine == null || !isCoreLine[i])
                        {
                            float lineEdge = 1f - (dist - coreRadius) / 0.5f;
                            Color32 c = Color32.Lerp(pixels[i], core32, lineEdge * 0.65f);
                            c.a = 255;
                            pixels[i] = c;
                        }
                    }
                }
            }
        }
    }

    void Show()
    {
        field.SetPixels32(pixels);
        field.Apply(false);
    }

    /// <summary>Clears only the thin matte center line back to the plain baked dirt along the segment
    /// just crossed - the phosphor band and its outline are left exactly as drawn, untouched. Swept as
    /// a capsule between two points rather than a circle at one, so a fast frame's big step still
    /// erases the whole stretch instead of leaving untouched dashes between where each frame landed.</summary>
    void ErasePath(Vector2 from, Vector2 to)
    {
        float radius = centerLineWidth * 0.5f + 0.5f;
        if (radius <= 0f) return;

        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.x, to.x) - radius));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(from.x, to.x) + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.y, to.y) - radius));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(from.y, to.y) + radius));
        float radiusSq = radius * radius;

        Vector2 along = to - from;
        float lengthSq = along.sqrMagnitude;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 here = new Vector2(x + 0.5f, y + 0.5f);
                float t = lengthSq <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(here - from, along) / lengthSq);
                Vector2 nearest = from + along * t;
                if ((here - nearest).sqrMagnitude > radiusSq) continue;

                int i = y * width + x;
                if (hasSeed != null && hasSeed[i]) continue;
                pixels[i] = basePixels[i];
                if (isCoreLine != null) isCoreLine[i] = false;
            }
        }
    }

    /// <summary>A cheap, deterministic pseudo-random angle from a position, so the same spot always
    /// jitters the same way rather than picking a new tilt every time it is recomputed.</summary>
    float JitterAngle(Vector2 texel)
    {
        float h = Mathf.Sin(Vector2.Dot(texel, new Vector2(12.9898f, 78.233f))) * 43758.5453f;
        h -= Mathf.Floor(h);
        return (h * 2f - 1f) * seedRotationJitter;
    }

    /// <summary>Blits <see cref="seedSprite"/> onto the baked texture at <paramref name="center"/>,
    /// rotated by <paramref name="angleDeg"/>, alpha-composited over whatever is already there.</summary>
    void StampSeed(Vector2 center, float angleDeg)
    {
        if (seedSprite == null) return;

        float w = seedCellsWide * texelsPerCell;
        float h = w * seedSprite.height / (float)seedSprite.width;
        if (w <= 0f || h <= 0f) return;

        float rad = -angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        float halfDiag = 0.5f * Mathf.Sqrt(w * w + h * h);
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - halfDiag));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(center.x + halfDiag));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - halfDiag));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(center.y + halfDiag));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float lx = x + 0.5f - center.x;
                float ly = y + 0.5f - center.y;
                float rx = lx * cos - ly * sin;
                float ry = lx * sin + ly * cos;

                float u = rx / w + 0.5f;
                float v = ry / h + 0.5f;
                if (u < 0f || u > 1f || v < 0f || v > 1f) continue;

                Color src = seedSprite.GetPixelBilinear(u, v);
                if (src.a <= 0.004f) continue;

                int i = y * width + x;
                if (hasSeed != null) hasSeed[i] = true;
                Color dst = pixels[i];
                float outA = src.a + dst.a * (1f - src.a);
                pixels[i] = outA > 0.0001f
                    ? new Color(
                        (src.r * src.a + dst.r * dst.a * (1f - src.a)) / outA,
                        (src.g * src.a + dst.g * dst.a * (1f - src.a)) / outA,
                        (src.b * src.a + dst.b * dst.a * (1f - src.a)) / outA,
                        outA)
                    : new Color(0f, 0f, 0f, 0f);
            }
        }
    }

    /// <summary>Two octaves of Perlin noise, normalised to roughly [-1, 1], sampled along one edge.</summary>
    float EdgeNoise(float pos, float seed)
    {
        float n1 = (Mathf.PerlinNoise(pos * noiseFreq1, seed) - 0.5f) * 2f * noiseWeight1;
        float n2 = (Mathf.PerlinNoise(pos * noiseFreq2, seed + 50f) - 0.5f) * 2f * noiseWeight2;
        float weightSum = noiseWeight1 + noiseWeight2;
        return weightSum > 0f ? (n1 + n2) / weightSum : 0f;
    }

    /// <summary>
    /// Bakes the parcel's window: a torn-edged, shadowed crop of <see cref="dirtSource"/> sized to
    /// <c>footprint * cellPixels</c> (clamped to fit the canvas), everything past the torn edge left
    /// transparent so the scene's own grass shows through.
    /// </summary>
    public void Build(Vector2Int footprint)
    {
        if (footprint.x <= 0 || footprint.y <= 0 || dirtSource == null || ground == null) return;

        float maxWinW = CanvasWidth - sideMargin * 2f;
        float maxWinH = CanvasHeight - topBottomMargin * 2f;
        float cellPx = cellPixels;
        if (footprint.x * cellPx > maxWinW) cellPx = Mathf.Min(cellPx, maxWinW / footprint.x);
        if (footprint.y * cellPx > maxWinH) cellPx = Mathf.Min(cellPx, maxWinH / footprint.y);
        texelsPerCell = cellPx;

        float winW = footprint.x * cellPx;
        float winH = footprint.y * cellPx;

        width = Mathf.Max(1, Mathf.RoundToInt(winW + tornBleed * 2f));
        height = Mathf.Max(1, Mathf.RoundToInt(winH + tornBleed * 2f));

        // Cover-crop the dirt photo to the window's own aspect, same rule as the grass background.
        float winAspect = winW / winH;
        float srcAspect = (float)dirtSource.width / dirtSource.height;
        float dW = 1f, dH = 1f, dX = 0f, dY = 0f;
        if (srcAspect < winAspect) { dH = srcAspect / winAspect; dY = (1f - dH) * 0.5f; }
        else { dW = winAspect / srcAspect; dX = (1f - dW) * 0.5f; }
        float uPerPx = dW / winW;
        float vPerPx = dH / winH;

        if (field == null || field.width != width || field.height != height)
        {
            Discard(field);
            field = new Texture2D(width, height, TextureFormat.RGBA32, false);
            field.filterMode = FilterMode.Bilinear;
            field.wrapMode = TextureWrapMode.Clamp;
        }

        int n = width * height;
        fenced = new bool[n];
        opaque = new bool[n];
        seeded = new bool[n];
        isCoreLine = new bool[n];
        isOutline = new bool[n];
        hasSeed = new bool[n];
        minDist = new float[n];
        for (int i = 0; i < n; i++) minDist[i] = float.MaxValue;
        pixels = new Color32[n];
        basePixels = new Color32[n];
        fencedCount = 0;
        sownCount = 0;
        driving = false;
        spent = false;
        lineLeft = batteryCells;
        lineTraveled = 0f;
        path.Clear();
        smoothPath.Clear();
        if (travelRoutine != null) { StopCoroutine(travelRoutine); travelRoutine = null; }
        if (result != null) result.text = "0%";

        for (int y = 0; y < height; y++)
        {
            float ry = y - tornBleed;
            float distTop = winH - 1f - ry;
            float distBottom = ry;

            for (int x = 0; x < width; x++)
            {
                float rx = x - tornBleed;
                float distLeft = rx;
                float distRight = winW - 1f - rx;

                int i = y * width + x;

                bool withinNominal = rx >= 0f && rx < winW && ry >= 0f && ry < winH;
                fenced[i] = withinNominal;
                if (withinNominal) fencedCount++;

                float dLeft = distLeft + EdgeNoise(ry, 11.3f) * noiseAmplitude;
                float dRight = distRight + EdgeNoise(ry, 47.9f) * noiseAmplitude;
                float dTop = distTop + EdgeNoise(rx, 91.7f) * noiseAmplitude;
                float dBottom = distBottom + EdgeNoise(rx, 5.2f) * noiseAmplitude;
                float inside = Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dTop, dBottom));

                float contentT = Mathf.Clamp01((inside + edgeSoftness * 0.5f) / edgeSoftness);
                contentT = contentT * contentT * (3f - 2f * contentT);
                opaque[i] = contentT > 0.001f;

                float su = Mathf.Clamp01(dX + rx * uPerPx);
                float sv = Mathf.Clamp01(dY + ry * vPerPx);
                Color baseCol = dirtSource.GetPixelBilinear(su, sv);

                float aoT = inside > 0f ? Mathf.Clamp01(1f - inside / aoFade) : 1f;
                aoT = aoT * aoT * (3f - 2f * aoT);
                float aoAlpha = aoT * aoMax;

                Color outc = Color.Lerp(baseCol, shadowColor, aoAlpha);
                outc.a = contentT;
                pixels[i] = outc;
            }
        }

        // Cache base pixels for smooth glow blending
        Array.Copy(pixels, basePixels, pixels.Length);

        Show();
        ShowBattery();
        ShowResult();

        RectTransform rt = ground.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = Vector2.zero;

        ground.texture = field;

        if (machine != null)
        {
            float machineWidth = machineCellsWide * texelsPerCell;
            machine.sizeDelta = new Vector2(machineWidth, machineWidth / machineAspect);
            machine.anchorMin = new Vector2(0.5f, 0.5f);
            machine.anchorMax = new Vector2(0.5f, 0.5f);
            machine.pivot = new Vector2(0.5f, 0.5f);
            machine.localEulerAngles = Vector3.zero;
            machine.localScale = Vector3.one;
            machine.gameObject.SetActive(false);
        }
        SetDriving(false);
    }

    static void Discard(UnityEngine.Object doomed)
    {
        if (doomed == null) return;
        if (Application.isPlaying) Destroy(doomed);
        else DestroyImmediate(doomed);
    }
}
