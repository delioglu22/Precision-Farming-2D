# Precision Farming 2D — design

A portrait mobile game about **building a farm that works well because you set it up well**.
The desired reaction is: “I love watching and growing this little farm.” Precision farming gives
that pleasure a cause: read the land, choose how to treat it, watch the result, then improve or expand.

This is the agreed direction and the working scope for the next playable prototype. It replaces
the earlier minigame-led design; existing minigames are legacy content awaiting an implementation
plan, not the target loop. Open decisions and provisional defaults are called out below.

## Core loop

1. Read a parcel's **moisture and fertility**.
2. Set a farming plan using the seeder, fertilizer drone and irrigation system.
3. Watch the machines plant and care for the crop; growth, harvest and sale complete the cycle.
4. Compare the harvest income with operating costs and adjust the plan when useful.
5. Keep a good plan running and invest the profit in equipment, buildings or another parcel.

Plans persist across cycles. The player does not need to repeat a gesture or win a minigame to
keep an already configured field productive. Parcels are lasting parts of the farm, not disposable
arcade levels. Expansion should reveal a new land condition or capacity decision, rather than make
the player re-enter the same instructions on a larger map.

## What the player optimizes

The initial model uses two land properties and three machine controls:

| Machine | Control | Why it matters |
| --- | --- | --- |
| Seeder | Planting density | More potential crop also means more seed and greater care needs |
| Fertilizer drone | Fertilizer intensity | Less fertile soil needs more support, which costs money |
| Irrigation | Watering level | Dry land needs more water; extra watering must justify its cost |

The objective is a worthwhile harvest after seed, fertilizer and water costs. Raising every control
to its maximum should not be the best plan for every parcel. A fertile, dry field should lead to a
different plan from a moist, less fertile field. These relationships must be understandable through
visible changes and a simple result breakdown, before adding more soil properties or machine types.

The first prototype treats each parcel as one uniform soil region. Its natural moisture and
fertility are stable conditions while a plan is being compared; weather, soil depletion and
per-cell variation are deferred. These are simplifying prototype defaults, not promises about the
finished game's depth. Exact response curves, control ranges and costs remain to be tuned.

Automation should be visible on the farm: recognizable machines with a clear job, readable crop
stages, a visible harvest and an unmistakable restart of the next cycle. The seeder must read as a
farm machine at phone scale. Watching a good routine work is part of the reward, alongside choosing
that routine. Machine scheduling is not the initial core challenge.

## Land, equipment and buildings

An unused parcel offers **Plant** or **Build**. Below 50% fertility it offers Build only. A building
on plantable land gives up crop income; poor land still asks which building is worth its space and
purchase price. Changing land use discards that parcel's farming plan; refunds and resale rules
remain open.

Equipment belongs to the farm. Capacity should eventually make expanding the fleet compete with
improving existing equipment. The old minigame battery and coverage scores do not define the new
machine upgrades. Service limits and upgrade effects need a separate balance decision.

The proposed first building is an **equipment depot**, supporting equipment ownership and future
fleet growth. As a prototype default, start with enough capacity for the introductory equipment;
the building layer should not block the first demonstration of farming. Its exact footprint,
capacity and price are open.

An **organic market** is a possible later building, once crop sales have a useful choice to add.
Ordinary selling must already work without it. Engineers, staff statistics and training buildings
are outside the current scope.

## The first five minutes

The opening should demonstrate one complete, understandable automation loop. These are pacing
beats to test, not fixed timers:

1. Show one working parcel, one crop and the three machines with a usable starter plan. Let the
   player recognize sowing, growth and harvest without a setup checklist.
2. On this fertile but dry parcel, show the moisture reading and let the player improve watering.
   Keep planting density preset so the first decision has one clear cause and effect.
3. Show the harvest result and running cost, then let the next cycle repeat with that same plan.
   The player should see that their improvement lasts without another command.
4. Introduce a moist parcel with lower, but still plantable, fertility. Fertilizer is the next
   useful adjustment; copying the first parcel's water-heavy plan should be visibly wasteful.
5. Pull back to two fields following different plans. Show the next investment to work toward;
   introduce the depot's expansion role after the farming loop makes sense.

Planting density becomes an editable decision after water and fertility are understood. The first
playtest should establish whether players can explain why the two fields need different plans and
whether they enjoy watching their plans continue to work.

## Prototype boundary and open decisions

The first playable slice needs two contrasting farm parcels, one crop, three machine types,
persistent plans, automatic harvest/sale and a readable income/cost result. Use one currency;
separate seed, fertilizer and water inventories are outside this slice. Build/Plant and the depot
remain part of the design, with the depot proposed as the first expansion step after this loop
is proven.

Decide before implementing the relevant slice: growth and cycle timing; the yield/cost model;
control presentation; starting money and unlock costs; machine capacity and upgrades; depot costs
and capacity; saving and offline progression. Do not silently turn these into features or promise
an idle income system. More crops, soil properties, machine types and a market wait for the small
loop to earn that additional complexity.

## Presentation

The player pans a portrait isometric map. Tapping a parcel opens a summary sheet with its identity,
moisture and fertility. Expanding it opens that parcel's working page, keeping the header and land
readings anchored. Close returns from page to sheet, then to map. Farming plans belong on the Plant
page; building choices and contents belong on the Build page.

Parcels look like surveyed farmland, with straight boundaries along the isometric axes and varied
sizes and proportions. Woods, ponds and the forest border give the farm a setting. They are scenery,
not production parcels. See `art.md` for visual constraints.
