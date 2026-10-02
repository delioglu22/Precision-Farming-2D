# Precision Farming 2D — design

A portrait mobile game about **building a farm that works well because you set it up well**.
The desired reaction is: “I love watching and growing this little farm.” Read the land, choose a
production routine, watch it work, then improve or expand it.

This document records the agreed direction, not proof of implemented behaviour. A first playable
version of soil recovery, explicit start/repeat controls and research-backed automation exists in
the prototype; its numbers are provisional fixtures awaiting playtest. Minigames are legacy content.
Implementation steps and provisional balance fixtures belong in the separate implementation plan.

## Core loop

1. Read a parcel's moisture and current soil fertility.
2. Choose a crop and set planting density, fertilizer dose and watering dose.
3. Preview the input costs, expected income, net result and soil change before pressing **Start**.
4. Watch sowing, care, growth, harvest and sale. Enable **Repeat** to keep the confirmed plan running.
5. Manage declining fertility, first by switching crops manually, then by installing automation.
6. Invest in research, installed equipment, depot capacity or more land.

A good routine should keep working without repeated gestures. The progression is to understand a
problem, solve it, then delegate that solution to the farm. Once automated, the same problem should
stay solved within the routine's stated limits. New investments create the next useful choice;
constant emergencies on established fields are not the goal. Parcels are permanent parts of a
farm, not arcade levels.

## Farming plans and soil

| Control | Tradeoff |
| --- | --- |
| Planting density | More potential harvest, seed cost, care demand and soil depletion |
| Fertilizer dose | Supports production on depleted soil at an operating cost |
| Watering dose | Compensates for dry land at an operating cost |

The next control presentation is a 0–100 scale, initially in 5-point steps. These are machine
settings, not soil readings. Controls should interact: dense planting needs more care, so moving
every slider to its maximum is not a universal solution. Exact curves remain prototype tuning.

Each parcel remains one uniform soil region. Natural moisture is stable in this slice. Fertility
becomes a recoverable nutrient reserve: commercial crops consume it; a restoration legume builds
it back. This is a readable farming abstraction, not a simulation of individual minerals. Include
one commercial crop and one restoration crop first. No weather or per-cell variation is needed.

Restoration earns its value through soil recovery, rather than a profitable sale. Its operating
cost and the opportunity cost of foregoing a commercial crop must be visible. Spending more on
fertilizer can sustain a commercial plan for a while, but should not remove the reason to restore
soil. Recovery must remain possible when a field becomes uneconomic; avoid a money trap in which
the player cannot recover any productive land.

## Preview, repeat and safe stopping

The parcel page shows the selected crop, three settings, cash needed to start, itemized seed,
fertilizer and water costs, expected sales and net income, and expected fertility after harvest.
The player confirms the plan with **Start**; **Repeat** is a separate, explicit setting. One-shot
operation finishes one crop and waits. Edits during a running crop apply to the next crop; turning
Repeat off lets the current crop finish. The harvest result remains available as history.

Before spending on each new crop, re-evaluate the plan. An uneconomic commercial plan pauses before
sowing, and an unaffordable crop also pauses. A red field marker explains the actual reason, such
as fertilizer costs exceeding income or insufficient funds. Never erase an in-flight crop to stop
the next one. Restoration is exempt from the commercial profit guard, but must still be affordable.
Healthy restoration is a normal working state, not a red error.

## Research and conditional automation

A persistent research tree is required. It includes sowing, fertilizing and watering efficiency,
and automation capabilities. Research spends the same farm currency as expansion: improving the
existing system competes with growing the farm. Efficiency reduces waste and operating costs; it
must not eliminate soil planning or produce free, limitless resources.

Initially the player selects a restoration crop manually and later returns to the commercial plan.
A research unlock introduces a **soil sensor and control module**. The unlock includes the first
installation kit so its benefit can be tried immediately; subsequent parcels need their own paid
installation. Basic soil readings remain visible without the module so manual decisions are fair.

An installed module maintains separate commercial and restoration plans. Provisional thresholds
are to enter restoration below 30% fertility and remain there until fertility exceeds 70%, then
return to the saved commercial plan. Crossing 30% while restoring does not switch it back early.
Crop changes happen between cycles. These thresholds are tunable defaults, not final balance.

If commercial production would become uneconomic before the lower threshold, the controller may
restore earlier. Recovery must have a viable commercial outcome; otherwise pause with a reason
rather than run a fruitless recovery loop. Preview and status must explain the controller's next
choice, including the return condition. Money availability is checked separately from profitability.

## Land, equipment and buildings

An unused parcel offers **Plant** or **Build** according to its fixed land suitability. Suitability
and current fertility are separate: depleted farmland never loses its Plant option or becomes
building-only. Building on arable land gives up possible crop income. Preserve the existing depot
and land-use decisions while testing the new farming loop; no new resale/refund system is needed.

Equipment belongs to the farm. Buying capacity lets the player automate more parcels; research
improves the efficiency of equipment already owned. A future larger machine could serve more
parcels than a small one, rather than duplicate a research bonus. The current equipment-set and
depot capacity model stays for this slice; a full device catalogue and shared-machine scheduling
wait until the soil/automation loop is proven. Do not also sell a “better seeder” whose sole benefit
is the same seed-cost reduction already sold through research.

The equipment depot supports fleet growth after the first farming demonstration. An organic market
is a later candidate, only when it adds a useful selling decision. Ordinary sales need no market.
Engineers, staff statistics and training buildings remain outside scope.

## Opening and prototype boundary

These are learning beats to test, not fixed five-minute timers:

1. Present a prepared commercial plan on a fertile, dry parcel. Preview a watering adjustment,
   then let the player start production and recognize the machines' jobs.
2. Show the result and enable Repeat so the improvement persists. Introduce a moist second parcel
   that benefits from a different care plan; avoid asking for every control at once.
3. Make depletion and its economic consequence understandable. Let the player restore a parcel
   manually and return to commercial production without a long repetition grind.
4. Unlock and install the first controller. Show it repeat the same recovery decision automatically.
5. Offer investment in efficiency, another installation or expansion through the depot.

Keep one currency, two initial contrasting parcels, one commercial crop, one restoration crop and
three recognizable machine jobs. Retain saving and the existing expansion path. No offline income,
separate resource inventories, extra soil properties, weather or fleet scheduling belongs in this
slice. More commercial crops and machine models wait for this loop to earn the complexity.

The implementer must label provisional costs, crop timing, soil response, thresholds and research
node values as balance fixtures. The user judges whether starting, learning and delegating feels
satisfying; technical correctness alone cannot establish that. Research and capacity unlock pacing
must be tested against the farm's actual income, not assumed from the existence of a purchase button.

## Presentation

The player pans a portrait isometric map. Tapping a parcel opens its summary sheet; expanding it
opens the working page with identity and soil readings anchored. Close returns from page to sheet,
then to map. Crop plans belong on Plant; buildings and their contents belong on Build. Explain
states such as “Restoring soil — return to the commercial crop above 70%” directly on the parcel.

Automation must be visible: recognizable autonomous machines, readable growth, harvest and restart.
The seeder should read as a farming robot at phone scale; its compact solar-powered identity is a
useful direction for later art work, not a requirement to replace assets during this systems slice.
Straight isometric parcel boundaries contrast with irregular woods, ponds and forest scenery.
See `art.md` for the visual constraints.
