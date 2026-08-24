---
name: spriteshape-notes
description: MUST use this skill for any work that touches a parcel's, pond's or forest's layers, fill texture, spline or collider bake. It carries the SpriteShape traps that have each cost a round trip: the fill texture's import type, boundsScale, bake order, edge sprite alignment, fillOffset and stale screenshots.
---

# SpriteShape notes

Every one of these cost a round trip. A parcel is five stacked fills — outline, two side faces, earth
border, crop — each traced from its footprint by `ParcelFootprint` and `ParcelLayer`; read those two
scripts' comments before changing the stack.

Every one of these cost a round trip. A parcel is five stacked fills — outline, two side faces, earth
border, crop — each traced from its footprint by `ParcelFootprint` and `ParcelLayer`; read those two
scripts' comments before changing the stack.

- A fill texture must be imported as **Default with Repeat wrap**. Hand it a Sprite-type texture and
  the shape renders **nothing at all** — no error, no empty mesh, and the bounds still look right.
- `SpriteShapeController.boundsScale` defaults to **2**, so `Renderer.bounds` reports twice the
  geometry. Never measure a shape with it; read the spline or the baked collider instead.
- In edit mode the mesh does not bake until the editor ticks, so within a single `execute_code`
  `RefreshSpriteShape()` alone changes nothing. Call `UpdateSpriteShapeParameters()`, then
  `BakeMesh()` and `Complete()` the handle, and only then `BakeCollider()`.
- An edge sprite is **centred on the spline** and its custom pivot is ignored; row 0 of the strip
  faces outwards. Author edge strips symmetric about the middle row or they land half outside.
- The fill draws **over** any edge geometry inside the spline. `fillOffset` is the lever, but a
  negative offset rounds the corners off, which is why parcels inset the outline instead.
- A spline point left as `SetCorner(true)` with no corner sprite in the profile leaves a wedge of
  bare geometry at every vertex. With no corner sprites, set it `false` and let the edges mitre.
- Baking a lot of shapes at once does not repaint the Game view, so `manage_camera screenshot` hands
  back a **stale frame**. Converting the island looked catastrophically broken for three screenshots
  while the meshes were in fact correct. Change something else, or repaint, before believing a
  picture taken right after a bake.
