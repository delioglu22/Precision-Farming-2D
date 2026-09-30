# Precision Farming 2D — art

How the farm looks and feels, and what a new piece must obey to belong beside the existing art.
`design.md` owns the game rules and prototype scope. This document separates the established visual
language and asset inventory from the next prototype's visual target; describing a target does not
mean its art already exists.

## The visual promise

The farm should be pleasant to watch because the player's plan is visibly working. A machine does
a recognizable job, the crop changes, a harvest leaves the field, and the next cycle begins without
another command. The reward is an increasingly productive little farm whose activity can be read at
a glance. The map carries that experience; it is more than a menu for separate minigames.

Give attention to clear silhouettes, purposeful motion and visible consequences before decorative
effects. Extra particles or a large score cannot make an unreadable machine understandable.

## The world

The land uses a **2:1 isometric grid**. One cell is `1.0 x 0.5` world units at **100 pixels per unit**,
so a cell is 100 x 50 px. Parcel boundaries, furrows and tile joins follow its two axes precisely.
This is a rule for ground geometry, not for every silhouette: wheels, rotors, foliage and other
props can be rounded or irregular while still sitting convincingly in the same world.

The **light comes from the upper right and never moves**. On a solid object the top face is lit,
the face falling to the lower left is darkest, and the lower-right face sits between them. Colour
is flat within a facet, with shading between facets rather than a gradient inside one.

Ground tiles have crisp edges: Point filtering, no compression and no mipmaps. Standalone props
retain their vendor-style outlines and baked soft shadows, described under "Vendor props" below.

The farm is open grassland enclosed by forest, with small woods and ponds among the fields. There
is no sea. Keep this setting and its established palette while making farming activity readable.

## Parcels and crops

An agricultural parcel is surveyed land: a rectangular area of worked soil, a fence and aligned
crop rows. The current soil, crop and fence tilemaps let these surfaces repeat across different
parcel sizes; a new parcel is not a separate painted picture. A selected parcel warms and lifts
slightly, preserving the underlying colour relationships and remaining attached to the ground. The lift
must stay below the next row's vertical step so selection does not reverse the scene's depth.

The existing inventory contains sixteen crop tiles: bellpepper, broccoli, cabbage, carrot, celery,
corn, eggplant, greenbean, lettuce, onion, pepper, potato, radish, spinach, tomato and wheat. That is
an art inventory, not sixteen playable crops or sixteen finished growth sequences. **The first
prototype uses one crop**; its identity remains to be chosen. Land without a crop is visibly fallow.

For that one crop, sowing, growth, readiness for harvest and the cleared field must read as different
moments. The harvested field returns to a recognizable starting state before the routine repeats.
Changes in plant size, fullness and visible soil should carry the progression, rather than colour
alone or a floating timer. Furrows run along the same isometric axis on every parcel and throughout
the cycle. Each repeating stage must meet itself cleanly at tile boundaries and remain legible from
the farm's normal view.

The following established ground and crop tones remain the reference when adding stages:

```
wheat        A58945 -> D2B35E      plowed      4A3B28 -> 9E7A51
grass        567C3A -> 7CA955      straw       896D47 -> DCC17E
grass dark   42632F -> 628C47      sage        886D47 -> AFA676
```

## Machines and repeating work

The three machines need distinct silhouettes and distinct effects at phone size. Their action
should visibly connect to the land they are treating; idle, working and finishing should be easy
to tell apart. A routine should visibly repeat without suggesting that the player must tap each pass.

- **Seeder:** a grounded farm machine with a clear front, recognizable wheels or tracks, a seed
  carrying body and a working edge. Its movement and the newly planted rows should explain sowing.
  The old seeder was mistaken for an insect; a body with ambiguous limb-like appendages does not
  pass, even if its motion is smooth. Establish the vehicle silhouette before adding small detail.
- **Fertilizer drone:** an airborne machine, with lift and a ground shadow that separate it from
  the seeder. Its fertilizer application must read differently from watering. Keep its payload and
  application area clear; rotor detail should not dominate the small silhouette.
- **Irrigation:** a visible connection between a water source, its delivery and the crop. A clear
  spray or stream is a useful visual cue; the exact equipment form is still open. It must not look
  like the drone's fertilizer effect recoloured, or turn the field into a pond.

Movement should have a readable start, working phase and finish. Harvest needs its own visible
change in the crop and collected produce, but a fourth machine is not required for this first
slice. Avoid adding vehicles solely to explain a transition the existing scene can communicate.

## Reading the land and the result

Moisture and fertility are different readings, so give each a clear label and a distinct visual cue.
Keep them separate from the player's planting, fertilizer and watering settings: a land condition
and an instruction should not look like the same value. The prototype treats a parcel as one soil
region; the visuals should not promise a detailed per-cell soil simulation.

Show care taking effect through the field's actual state, supported by concise feedback. A change
of tint alone should not be the only way to tell which condition or action is being shown. The
selected parcel's warm tint is selection feedback, not a signal of fertility or profitability.

The result distinguishes **harvest income, operating costs and net return**. A larger harvest can
cost more to produce; a lush crop alone must not imply that its plan is the most profitable. Keep
that comparison readable on the parcel page. Celebratory motion may accompany a harvest, but
arbitrary coverage percentages, memory-game scores or oversized score effects are not the target
visual language for successful farming.

## Buildings

Buildings belong to the growing farm and must make their use readable. The **equipment depot is
the proposed first building**, after the farming loop is established. If that proposal is taken
forward, its silhouette and contents should suggest equipment storage and available space, rather
than crop stalls or an unrelated decorative house. Its final form and footprint remain open.

An organic market is a later candidate, not a requirement for showing the first harvest or sale.
Staff portraits, training facilities and a collection of speculative building variants are outside
the initial art slice.

## Woods and ponds

Both sit directly on the grass. A **pond** is a painted surface; a **wood** is a collection of
individually placed trees. Crop and water surfaces repeat, while tree spacing and size vary enough
that each wood has its own arrangement. There is no canopy tile and no regular tree lattice.

Pond water stays fresh and green rather than oceanic; deeper water is darker than the ground around
it so it reads as a depression. Banks follow the world axes but have varied cut-back corners and
bites in their outline, rather than forming a clean field-shaped parallelogram. Bank props do not
trace an evenly spaced outline. Only objects built over water, such as a jetty, cross the waterline;
ordinary props stay on the bank.

The forest border uses the same trees and remains wider than the area the player can pan across.
Height projects up the screen: tall props near a far boundary can appear to belong to the land
behind them. Keep their ownership visually clear, using near-edge placement where needed.

```
shallow water  74B0A4        bank sand    C9AE7A
deep water     3F8080
```

## Earth palette

The old raised parcel slab is retired. Its palette remains a reference for soil, fences and other
earth-coloured surfaces; it is not a request to restore side faces or an earth ring:

```
outline     35291B      darkest ground outline
left face   402E1F      earth in shadow
right face  5E462D      earth catching light
border      8A6E46      worked earth beside a boundary
```

## Vendor props

The buildings, tools and standalone objects in `Assets/Art/Vendor/Gr8FarmPack`,
`Assets/Art/Vendor/Gr8OutdoorsPack_ODDBLOT` and `Assets/Art/Vendor/Gr8Pond_ExpansionPack_ODDBLOT`
establish the prop style: flat cartoon forms, outlined silhouettes and modest internal linework.
New machines and buildings should belong beside these props, while their contact with the ground
respects the tile grid. They do not need to imitate the ground's pixel geometry.

The existing measured outline reference is **`#33363F`**, dark navy-charcoal rather than pure black.
The packs vary by a few hex steps; the recorded boat sample is `#353540`. Keep the shared fill
palette as the starting point for new props:

```
outline        33363F      dark navy-charcoal
cream          EBE5D9      roofs, trim, highlights
warm grey      BFB8B2      secondary light surface, shadow-on-cream
barn red dark  A74C49      shared dark red
barn red light C95854      shared light red
metal grey     626166 .. 928C8C   metal ramp
sage green     557D57 / 8B9151   plant stem and leaf
```

Faces remain flat-coloured, with closer shading steps than the ground and restrained seams,
rivets or hatching. A soft shadow at the base is part of the prop style, not a reason to blur the
whole sprite. Point filtering preserves those authored edges and shadows.

Large structures use the world's approximate three-quarter isometric view; small hand-props may
read closer to a front-facing icon. Match framing to scale and role. The vendor packs provide no
vehicle reference, so the new seeder's recognition must be judged on its own silhouette, with the
outline and palette providing continuity with the existing buildings.

## The first art slice and its checks

Start with the existing map and two contrasting parcels, one crop's readable cycle, the three
machine identities and their working effects, and the parcel information/result display. Reuse the
established ground, fence, woods and ponds. The depot is the next proposed building treatment; more
crops, machine variants, markets and scenery expansion wait until this small slice reads well.

Judge the result in the portrait farm view and in the parcel view at the intended phone scale:

- Can someone identify the seeder as a vehicle and distinguish sowing, fertilizing and watering
  without an explanation of the animation?
- Can they see growth, harvest and the start of another cycle without giving another command?
- Can they distinguish the two land readings, the chosen treatment and the income/cost result?
- Do machines remain recognizable against the crops, and does the farm remain pleasant to watch
  with both fields active rather than becoming a mass of overlapping effects?

## Making a new piece

Measure existing references before inventing new colours or geometry. A flat colour belongs in a
swatch, not its own texture file. Repeating soil, crop and water surfaces remain tiles; individually
meaningful machines, buildings and irregular tree arrangements are standalone objects.

Exact tile boundaries must be constructed and checked against the grid. Do not rely on image
generation to produce matching tile geometry. Generation may provide standalone props or texture,
using the established outline and palette, but the result still has to meet the phone-scale checks
above. Sprite pivots are deliberate; the established import baseline is 100 PPU, Point filtering,
no compression and no mipmaps.
