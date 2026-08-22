# Precision Farming 2D — art

How the game looks, and what a new piece has to obey to sit next to what is already there.
`design.md` says what the game is; this says what it looks like.

## The world

Everything stands on a **2:1 isometric grid**. One cell is `1.0 x 0.5` world units, drawn at 100
pixels per unit, so a cell is 100 x 50 px. Nothing is drawn off this grid — an edge that does not run
along one of the two axes reads as a mistake, not as variety.

The **light comes from the upper right and never moves.** On a slab: the top face is lit, the side
falling away to the lower left is the darkest surface in the scene, and the side to the lower right
sits between them. Any new solid object obeys this or it fights the fields around it.

Colour is **flat within a facet**. The shading lives in the step between one facet and the next, not
in a gradient inside one. Pixels are not filtered — Point, no compression, no mipmaps. A soft edge
looks like an error here.

The farmland is an island in open water, with a few small woods and ponds among the fields.

## Parcels

A parcel is a slab of earth with a field on top: a dark line around the outline, an earth border
inside it, the crop in the middle, and the two sides falling away under the lower edges.

**A parcel is not a picture.** It is five flat shapes stacked, each traced from the parcel's size in
cells by Unity's SpriteShape. So making parcel art never means drawing a parcel — it means picking a
colour for one of the four earth surfaces, or drawing the tile the field is filled with. A parcel's
size is a number, so fields of any dimension cost nothing to add and none of them needs new art.

## Fields

Six crops, one tile each. The furrows run along the **same isometric axis on every parcel**, never
the other one, with a dark line every 16 px. Draw a field tile at **64 x 64** — the pattern only
meets itself cleanly at that size, and a wrong size shows as a seam across every field at once.

```
wheat        A58945 -> D2B35E      plowed      4A3B28 -> 9E7A51
grass        567C3A -> 7CA955      straw       896D47 -> DCC17E
grass dark   42632F -> 628C47      sage        886D47 -> AFA676
```

## Earth

The four surfaces of the slab, darkest to lightest:

```
outline     35291B      the line around the whole parcel
left face   402E1F      the side in shadow
right face  5E462D      the side catching the light
border      8A6E46      the earth ring between the outline and the crop
```

## Making a new piece

**Measure before inventing.** The art already here is regular to the pixel. Guessing produces
something that almost matches, which reads worse than something openly different.

**Flat colour is a swatch, not a file.** A single colour belongs in the layer's colour field in the
Inspector. Only a pattern earns a texture of its own.

**Do not ask an image generator for exact geometry.** It cannot hit a grid to the pixel, and a tile
that is three pixels off seams across the whole map. Ask it for surface texture and for scenery —
trees, rocks, water — where nothing has to line up.
