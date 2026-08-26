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

**A picked parcel warms its crop, not its earth.** The field brightens and the slab grows a couple of
percent; the outline, the border and the two sides hold their colour. Warming the whole slab reads as
strongly but softens the boundary against the neighbours, and earth does not get warmer when you
touch it. Keeping the earth fixed also keeps its colour in one place, since only the crop layer is
untinted to begin with.

## Fields

Six crops, one tile each. The furrows run along the **same isometric axis on every parcel**, never
the other one, with a dark line every 16 px. Draw a field tile at **64 x 64** — the pattern only
meets itself cleanly at that size, and a wrong size shows as a seam across every field at once.

```
wheat        A58945 -> D2B35E      plowed      4A3B28 -> 9E7A51
grass        567C3A -> 7CA955      straw       896D47 -> DCC17E
grass dark   42632F -> 628C47      sage        886D47 -> AFA676
```

## Woods and ponds

They stand on the same slab as a parcel, with the same outline, border and sides. What sits on that
slab is now the difference between them: a pond is a surface, a wood is a collection of things.

A **pond** is three rings inside the border: a **sand bank**, then shallow water, then deep. Its
water is deliberately **not** the sea's colour — fresh water sits greener and brighter than open
water, and without that separation a pond reads as a hole in the island with the sea showing
through. Deep water is also the one place a real dark belongs: it has to fall well below the earth
around it, or the pond sits on the land instead of in it.

The bank and the border are traced from the plot like any other slab, but the **water is drawn by
hand**. A parcel is surveyed and should look surveyed; water is not, and water with a plot's edges
reads as a field somebody filled with blue. So the two water layers are authored per pond, along
the same two axes as everything else but never as a clean parallelogram — corners cut back by
different amounts, and a bite taken out somewhere so the outline is not convex. They therefore do
not grow with the plot, and no two ponds are the same shape.

Leaving the bank as the plot's own diamond is what makes its width vary for free: wide where the
water pulls back from a corner, pinched to almost nothing where it reaches out. An even bank is the
giveaway of a machine-made shape, so never trace the bank from the water. The bank is what the
slab used to be missing — without it the water met the track directly, so anything standing near it
read as floating, and a jetty most of all. Props stand **on the sand**. Only a thing built out over
water — a jetty, a mooring — crosses the waterline, and nothing at all sits on open water.

A **wood** is no longer a surface at all. The canopy tile is gone. The slab takes a flat colour
where that tile used to be, and the trees are individual sprites stood on top of it. A wood
therefore does **not** grow with its plot for free the way a pond does — a bigger wood means more
trees, placed. That is the price of no two woods looking alike.

Trees are not laid on a lattice. They vary a little in size and sit at no fixed spacing, but every
one of them stands **inside the floor**. The earth border around a slab is the track around the
plot — the same ring a parcel cannot be worked, planted or built on — so nothing stands on the
border and nothing hangs past the slab's edge. A wood reads as a wood by what fills its middle.

Height projects **up the screen**. A tall thing placed at the far edge of a slab leans back across
the border and onto whatever is behind it, which is how a jetty ends up looking like it belongs to
the next field. Put the tall things toward the near edge and let the low ones sit further back.

```
shallow water  74B0A4        wood floor   486A3C
deep water     3F8080        bank sand    C9AE7A
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

**A surface that repeats is a fill, not a heap of objects.** Crops and water cover a plot with the
same thing over and over, and a tiling fill grows with the plot for free. Reach for real objects
only when they are individually meaningful — something the player will click, count or move.

**A wood is the exception, and the only one.** Its trees are placed one at a time, because a wood is
meant to differ from the next wood, and a lattice cannot do that. A pond is not the exception: its
water is the same at every point, so the water stays a fill, and the only things placed on it are
built out over it.

**Do not ask an image generator for a tile or for anything on the grid.** It cannot hit a grid to
the pixel, and a tile that is three pixels off seams across the whole map. Ask it for a one-off that
stands alone and lines up with nothing — a building, a boat, a signpost.
