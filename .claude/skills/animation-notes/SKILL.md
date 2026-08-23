---
name: animation-notes
description: Bir Animator, AnimationClip, state ya da transition olusturan/degistiren her iste MUTLAKA bu skill'i kullan. Panelin buyume animasyonunu, klip property eslesmesini, path binding'i ve sahnedeki dinlenme pozunu dogru kurmak icin gereken tuzaklari tasir.
---

# Animation notes

- Every pose of an animator must key **the same properties**. A property that only one clip mentions
  is undefined while blending into it, and the panel jumps instead of growing.
- Growing a bottom sheet to full screen: animate `m_AnchorMax.y` 0 → 1 together with `m_SizeDelta.y`
  760 → 0, never a fixed pixel height. The canvas is 1920 units tall only at exactly 9:16, so a
  hardcoded height leaves a gap at the top on every other aspect.
- `ParcelPanel.controller` is the worked example: states `Closed` → `Open` → `Expanded`, bools `Open`
  and `Expanded`, 0.22s fixed-duration transitions with no exit time. `Expanded → Closed` is listed
  **before** `Expanded → Open`, so a deselection closes the panel outright instead of collapsing first.
- The scene must store the same resting pose as the animator's default state, or the first frame
  shows the panel somewhere the animation never put it.
- `ParcelIdle` / `ParcelSelected` bind by **path**: the scale onto the parcel root, the colour onto
  the child named `Crop`. Rename that child and the pick stops warming, silently and with no error.
  Only the crop is tinted on purpose — see `docs/art.md`.
