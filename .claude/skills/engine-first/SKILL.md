---
name: engine-first
description: When adding new behaviour in Unity, look for the engine's built-in component before writing a script. MUST use this skill for any work that would add a MonoBehaviour, handle input or clicks, play an animation, show or hide UI, or carry data — even when nobody says "write a script". Any new script over ~50 lines is in scope.
---

# Use the engine first

Unity already does most of what a small game needs. Finding that ready-made answer
before writing code is your job.

## The rule

Before writing a new MonoBehaviour, **name the Unity component that would do this
job**. If one exists, use it. If none does, say so in one sentence, then write the
script.

This is mandatory for any new script over 50 lines. Say it out loud when you hand
the work over too: "I used this" or "Unity has no equivalent because ...".

## This is not a theoretical rule

A 138-line `ParcelSelector` was written in this project to detect taps: pointer
tracking, a drag threshold, `Physics2D.OverlapPointAll`, front-most sorting, and a
"did this land on the UI" check. A `Physics2DRaycaster` on the camera plus
`IPointerClickHandler` does all five. The script was deleted in full and nothing was
lost. 440 lines became 286.

## Where to look

| Job | Reach for |
| --- | --- |
| Clicks on world objects | `Physics2DRaycaster` on the camera + `IPointerClickHandler` |
| A tap that must not fire mid-drag | Already handled — `EventSystem.pixelDragThreshold` |
| A click that must not pass through UI | Already handled — the EventSystem consumes it |
| Showing, hiding or sliding UI | `Animator`: one bool, two poses, a transition between them |
| Values worth tuning | `[SerializeField]` and the Inspector, not constants |
| State shared across scenes | A `ScriptableObject` both scenes point at |
| Arranging UI | Anchors and layout groups, not code that computes positions |
| Timing, sequenced events | `Animator` or Timeline |
| Several renderers that must sort as one thing | A `SortingGroup` — one number outside, 0..n inside |

The `SortingGroup` row is not theory either. Back when a parcel was a five-layer slab,
the plan was to spread every parcel's order across five numbers per layer.
`SortingGroup` holds the layers together and leaves the parcel one number, so that plan
collapsed entirely and `Parcel.cs` stopped needing to know how many layers exist. The
slab is gone — parcels are tilemaps now — but that is exactly why the lesson kept:
the engine's answer outlived the thing it was answering for.

## Swinging too far the other way

The rule is **not** "never write scripts" — it is "name the alternative". Some things
have no Unity equivalent. `MapPan` exists for exactly that reason: there is no
built-in map panning, and writing it was correct. Avoiding necessary code is as bad
as writing unnecessary code.

## The real danger

The problem is not ignorance of the API. It is that verifying your own code is easier
than verifying the engine's, so you drift toward writing it. Resist that and verify
the engine instead — raycast through the EventSystem, step the Animator by hand,
measure the result.
