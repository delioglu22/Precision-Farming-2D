---
name: animation-notes
description: Create or change Unity Animator controllers, AnimationClips and animation transitions in this project. Covers the parcel panel's growth, matching clip properties, path bindings and the saved resting pose. Does not apply to unrelated gameplay state machines.
---

# Animation notes

- The panel's poses must key **the same controlled properties**. Relying on an unkeyed property's
  default or previous value while blending can make the panel jump instead of growing.
- Growing a bottom sheet to full screen: animate `m_AnchorMax.y` 0 → 1 together with `m_SizeDelta.y`
  760 → 0, never a fixed pixel height. The canvas is 1920 units tall only at exactly 9:16, so a
  hardcoded height leaves a gap at the top on every other aspect.
- `ParcelPanel.controller` is the worked example: states `Closed` → `Open` → `Expanded`, bools `Open`
  and `Expanded`, 0.22s fixed-duration transitions with no exit time. `Expanded → Closed` is listed
  **before** `Expanded → Open`, so a deselection closes the panel outright instead of collapsing first.
- The scene must store the same resting pose as the animator's default state, or the first frame
  shows the panel somewhere the animation never put it.
- Clip bindings use hierarchy **paths** relative to the Animator. Inspect the current bindings before
  renaming or moving animated children, then check every pose after the change.
- Parcel selection currently uses `Assets/Scripts/World/Parcel.cs`, not parcel animation clips:
  it lifts the `Grid` and warms the occupied cells in `Field`, `Crops` and `Fence`, then restores
  their previous colours. Keep that separate from the panel's Animator when diagnosing selection.
- The page's body is shown by **animating a `CanvasGroup`**, not by toggling the GameObject: path
  `Content/Body`, with `m_Alpha`, `m_Interactable` and `m_BlocksRaycasts` keyed in all three clips.
  Skip the raycast key and the invisible machine rows still swallow taps while the sheet is small,
  which reads as a dead panel rather than as an animation bug.
