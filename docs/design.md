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
from the bottom of the screen — that panel is where a parcel is read and acted on. The view closes
in on the parcel as the panel rises and opens back out when it is dismissed, so the map is read at
arm's length and a parcel up close.

The sheet reports, it does not edit. A parcel is one thing at a time: empty land asks a single
question — **Build** or **Plant** — and answering it decides what that land is for, since ground
given to a store is ground that grows nothing. The answer can be undone, but only from the bottom of
the parcel's own page, and undoing it throws away everything tuned there. That is the price of
changing your mind: not the coins, but the attention already spent.

Once answered the sheet stops asking. Its header says what the parcel is, and its body reports the
land itself — how fertile the soil is and how much water it holds. Those two readings are what makes
one parcel different from the next, and they are what the player weighs before deciding what the land
is for.

The machines stay off the sheet. **Plant** opens the page that lists the seeder, the drone and the
irrigation system with their properties; **Build** opens the page where a building is chosen and what
it holds is shown. The sheet is for reading a parcel at a glance, the page for working on it.

Acting on a parcel needs more room than a sheet leaves, so the sheet grows into a page that fills
the screen. It is the same surface, and what makes it read that way is what does not move: the
parcel's name and its two readings hold their place while the body opens in the space below them.
The page is not somewhere else, it is the same parcel closer up. Closing walks back the way you
came — page to sheet, sheet to map.

Three machines work a farm: a seeder, a drone that fertilises and an irrigation system. Each is tuned
in a mini game of its own, so optimizing is per machine rather than one dial for the whole parcel, and
every machine gets to ask the player for something different.

## Not decided yet

What automation and optimization concretely consist of — the inputs a parcel takes, the knobs the
player turns, and how profit is calculated. These get written here as they are settled, not before.

Whether a machine's percentage is worth the same on every parcel. If it is, every farm asks for the
same three mini games in the same order and optimizing becomes a chore list. The intent is that the
bottleneck differs from parcel to parcel, so that leaving a machine at 60% is often the right call —
but what decides the bottleneck is not settled.

Whether a crop is more than a look. Six of them exist, and right now choosing one is decoration. For
a crop to become state it has to decide something: what it costs to plant, what it yields, how long
it takes, and whether the automation is tuned per crop or per parcel. Until one of those has an
answer worth playing, it stays a look.

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
