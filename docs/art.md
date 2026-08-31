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

The farmland is open grassland, closed in by forest on every side, with a few small woods and ponds
among the fields. There is no sea — the trees are what the map ends in.

## Parcels

A parcel is a rectangle of worked soil with a fence around it and a crop planted across it.

**A parcel is not a picture.** It is three tilemaps — the soil, the crop and the fence — painted
across the block of cells the parcel owns. So making parcel art never means drawing a parcel; it
means drawing a tile. A parcel's size is a rectangle of cells, so fields of any dimension cost
nothing to add and none of them needs new art.

**A picked parcel warms and lifts.** Every tile it owns is multiplied by a warm tint, and the whole
parcel rises a fraction off the map. The lift has a ceiling: raise it past one cell's step up the
screen and the parcel sorts in front of the row it should be sitting behind.

## Fields

Sixteen crops, one tile each — bellpepper, broccoli, cabbage, carrot, celery, corn, eggplant,
greenbean, lettuce, onion, pepper, potato, radish, spinach, tomato, wheat. A parcel with none of
them assigned is **fallow**, and that is a real state of the game, not a gap to be filled.

The furrows run along the **same isometric axis on every parcel**, never the other one. A crop tile
has to meet itself cleanly on the grid: get the size wrong and the seam shows across every field at
once, not only the one being looked at.

The earlier hand-drawn fills are kept below, because a new crop tile still has to sit among these
tones rather than beside them:

```
wheat        A58945 -> D2B35E      plowed      4A3B28 -> 9E7A51
grass        567C3A -> 7CA955      straw       896D47 -> DCC17E
grass dark   42632F -> 628C47      sage        886D47 -> AFA676
```

## Woods and ponds

Neither stands on a slab any more; both sit directly on the grass. The difference between them is
what they are made of: a pond is a surface, a wood is a collection of things.

A **pond** is water painted onto the shared map, cell by cell. Its colour is deliberately fresh
rather than oceanic — greener and brighter than open water — and deep water is the one place a real
dark belongs: it has to fall well below the ground around it, or the pond sits on the land instead
of in it.

The **water is drawn by hand**. A parcel is surveyed and should look surveyed; water is not, and
water with a plot's edges reads as a field somebody filled with blue. So each pond is authored along
the same two axes as everything else but never as a clean parallelogram — corners cut back by
different amounts, and a bite taken out somewhere so the outline is not convex. A pond therefore
grows with nothing for free, and no two ponds are the same shape.

An even bank is the giveaway of a machine-made shape, so never trace a bank from the water. Props
stand **on the bank**; only a thing built out over water — a jetty, a mooring — crosses the
waterline, and nothing at all sits on open water.

A **wood** is not a surface at all. There is no canopy tile: the trees are individual sprites stood
on the grass. A wood therefore does **not** grow for free the way a painted surface does — a bigger
wood means more trees, placed. That is the price of no two woods looking alike.

Trees are not laid on a lattice. They vary a little in size and sit at no fixed spacing. A wood
reads as a wood by what fills its middle, not by any edge drawn around it.

The same trees, in far greater number, make the **forest border** that closes the map in on every
side. That border is scenery with a job: it is what the view runs into instead of the empty
background, so it has to stay wider than anywhere the camera can be panned.

Height projects **up the screen**. A tall thing placed at the far edge of a slab leans back across
the border and onto whatever is behind it, which is how a jetty ends up looking like it belongs to
the next field. Put the tall things toward the near edge and let the low ones sit further back.

```
shallow water  74B0A4        bank sand    C9AE7A
deep water     3F8080
```

## Earth

The slab is retired — a parcel no longer has an outline, two side faces and an earth ring. These were
its four surfaces, darkest to lightest, kept because they are still the tones the ground, the fence
and anything else made of earth have to agree with:

```
outline     35291B      the darkest line in the scene
left face   402E1F      the side in shadow
right face  5E462D      the side catching the light
border      8A6E46      worked earth between a boundary and a crop
```

## Making a new piece

**Measure before inventing.** The art already here is regular to the pixel. Guessing produces
something that almost matches, which reads worse than something openly different.

**Flat colour is a swatch, not a file.** A single colour belongs in a renderer's or a tilemap's
colour field in the Inspector. Only a pattern earns a texture of its own.

**A surface that repeats is a tilemap, not a heap of objects.** Crops and water cover ground with the
same thing over and over, and painted cells grow with the area for free. Reach for real objects only
when they are individually meaningful — something the player will click, count or move.

**A wood is the exception, and the only one.** Its trees are placed one at a time, because a wood is
meant to differ from the next wood, and a lattice cannot do that. A pond is not the exception: its
water is the same at every point, so the water stays painted, and the only things placed on it are
built out over it.

**Do not ask an image generator for a tile or for anything on the grid.** It cannot hit a grid to
the pixel, and a tile that is three pixels off seams across the whole map. Ask it for a one-off that
stands alone and lines up with nothing — a building, a boat, a signpost.
