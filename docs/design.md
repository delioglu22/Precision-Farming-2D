# Precision Farming 2D — design

A game about **automating farmland and then optimizing that automation**. You do not farm by hand;
you set up systems that farm for you, and the interesting decisions are about how well those systems
are tuned.

This document stays short on purpose. It records what has actually been decided, and grows as the
game does.

## Core loop

1. **Buy a parcel.** Land is the thing you spend money on.
2. **Click it to automate it.** A parcel with no automation produces nothing.
3. **Optimize the automation.** This is where the game is. A crudely automated parcel is profitable;
   a well tuned one is much more profitable.
4. **Take the profit and buy the next parcel.** Repeat, with more land under management each cycle.

The pressure comes from the loop compounding: profit buys land, land needs automation, and better
optimization on existing land funds expansion faster than sprawling into new land does.

## Playing it

A phone game held upright. You drag the map to move around it, and tapping a parcel raises a panel
from the bottom of the screen — that panel is where the automation is set up and tuned.

## Not decided yet

What automation and optimization concretely consist of — the inputs a parcel takes, the knobs the
player turns, and how profit is calculated. These get written here as they are settled, not before.

## The map

Parcels are the click target for everything above, so the map exists to make them readable and
distinct at a glance.

The world is drawn in 2D isometric. Parcels should look like surveyed farmland: straight boundaries
running along the isometric axes, sizes and elongations varying noticeably from one parcel to the
next, and mostly rectangular outlines with the occasional corner traded between neighbours so the
grid does not read as uniform. They must never look like jigsaw pieces — no thin teeth, no long
narrow arms, no interlocking by a single cell.

The farmland sits on an island in open water, and carries a few small woods and ponds among the
fields. Those are scenery — they are not parcels, cannot be clicked, and take no part in the loop.

The map is authored in the scene, not generated at runtime.
