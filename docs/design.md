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
given to a store is ground that grows nothing. Below fifty percent fertility, Plant is never on
offer — the sheet asks Build alone, because land that poor isn't farmland to weigh, only ground to
build on. The answer can be undone, but only from the bottom of
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

Each of the three machines reports a percentage, and what a parcel earns is its full worth scaled by
the **average** of those three rather than their product. Eighty percent on all three machines is a
parcel running at eighty percent, because covering four fifths of a field is not a failure. That
average is weighted, and the weights come from the two readings the parcel already shows: poor soil
makes the drone matter more, dry land makes irrigation matter more, and the seeder always carries
weight because nothing grows where no seed landed. This is what stops every farm from asking the same
three questions in the same order — on good land a half-tuned drone costs almost nothing and walking
away from it is correct, while on poor land it is the whole parcel.

## The seeder

The seeder is tuned by driving it. The map draws a parcel in isometric, but the mini game lays the
same field out straight down, as the rectangle of cells it is — seen at an angle it would spend half
the screen on dead corners. Its mini game hands you that rectangle and one unbroken line: you put a
finger down, the machine follows where you lead it, and you do not lift until the run is over. The
line lays a band of seed around itself, and the job is to cover the parcel with that band before the
battery runs out. The battery is a length of line rather than a stretch of time, and it empties along
the foot of the screen as the machine drives, so what is being watched is how much line is left
against how much ground is still bare. Ground seeded twice is
battery spent for nothing, ground missed stays bare, and the run ends on a percentage — the share of
the parcel that got seed. The run cannot be failed; an empty battery just stops the machine where it
stands, and whatever was covered still pays. Seed laid outside the parcel is wasted, which makes the
boundary the hard part and the middle of a field the easy part. A parcel's percentage stands until it
is run again.

Seeders come in sizes and a farm runs a fleet of them. A bigger seeder lays a wider band and carries
more battery, and that is what lets it finish a large parcel at all — a smaller one runs dry with
ground left over and cannot be made to reach. Every seeder also has a limit on how many parcels it can
be responsible for, and that limit is counted in parcels rather than in ground: a slot spent on a
scrap of land costs exactly what a slot spent on a large field costs. So the machine goes on the
biggest parcel it can still finish, and putting an expensive seeder on land a cheap one could have
covered wastes the thing that is actually scarce. Seeders are dear early on, the fleet is short, and
some parcel goes unseeded until it is not.

The top seeder is sized against the largest a parcel can be, not against a margin above it: driven
well it clears that field with little battery to spare, driven carelessly it runs dry short of the
last corner. That keeps the biggest parcel a real test even once the fleet is maxed out, rather than
something money alone settles.

## The drone

The drone fertilizes by memory rather than by hand. Its mini game draws the same grid the seeder
uses, and every tile a parcel needs fertilized lights up red at once — which tiles matters, the order
they're tapped in does not. How many light up comes from the parcel's own fertility: the worse the
soil, the larger the share of the grid that needs it, up to half the grid at the worst fertility a
parcel can still be planted on — land poorer than that never gets a Plant option in the first place.

A drone has a single stat: how many lit tiles it can hold on screen at once. A parcel needing more
than that splits into groups instead — a group closes the moment its own tiles are all found and the
next lights in its place, so a wrong tap can waste a click but never stalls the run. Upgrading a drone
lowers that number, never removes it, so the hardest parcel stays a real test even to the best drone
money can buy.

A tapped tile leaves the same wet mark whether it was right or wrong, so the run gives no feedback
while it's happening — only once it ends does the player see what was found and what was wasted.
Tapping an already-marked tile again is not stopped, since the mark is already there to see. The
score is correct taps over total taps across the whole run, not per group, so four tiles found in
four taps score full marks and the same four found in six do not; there is no cap on how many tiles a
player may hit, the formula is the only penalty for it. At least three tiles light no matter how good
the soil, so one miss is never the whole run, and the worst a run can score is the parcel's own share
of lit tiles — tapping the full lit set once each and nothing else.

The drone itself never scans in: it drops onto a single tile from above, sprays, and leaves the way
it came.

## Not decided yet

What automation and optimization concretely consist of — the inputs a parcel takes, the knobs the
player turns, and how profit is calculated. These get written here as they are settled, not before.

How a parcel's two readings turn into the three weights. That poor soil leans on the drone and dry
land on irrigation is settled; the arithmetic that takes a fertility and a water figure and returns
three numbers is not.

How long a lit tile stays lit before it's decided, and how much room that leaves to reach it, isn't
settled. It likely can't be one flat number — a group scattered across a large board needs more reach
time than one bunched in a corner — so it stays a knob to find by playing rather than by arithmetic.

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

The farmland is open grassland closed in by forest, and carries a few small woods and ponds among the
fields. Those are scenery — they are not parcels, cannot be clicked, and take no part in the loop.

The map is authored in the scene, not generated at runtime.
