# Precision Farming 2D

A 2D isometric mobile game about **automating farmland and optimizing that automation**. You don't
farm by hand — you buy parcels, automate each one with a seeder, a drone and an irrigation system,
and the game is in tuning that automation well.

Built in Unity (URP, Linear color space) through an AI-pair-programming workflow. See
[`docs/project-case-study.md`](docs/project-case-study.md) for the engineering write-up — architecture
decisions, the built-in-engine-first discipline, and the commit/review process behind the 55+ commits
in this repo's history.

## Core loop

1. Buy a parcel.
2. Click it to automate it — an unautomated parcel produces nothing.
3. Optimize the automation. A crudely automated parcel is profitable; a well-tuned one is much more so.
4. Take the profit, buy the next parcel, repeat with more land under management each cycle.

Full design details live in [`docs/design.md`](docs/design.md).

## What's built so far

- The map: 27 hand-placed parcels, woods, ponds and a forest border, all on tilemaps.
- Parcel selection with a per-tile warm tint and a lift off the map.
- A bottom-sheet panel that grows into a fullscreen page for reading and acting on a parcel.
- Camera pan and zoom (mouse wheel or two-finger pinch).
- A working seeder mini game: drive one unbroken line across the field, lay a band of seed, battery
  measured in line length, coverage reported back as a percentage.

The drone and irrigation mini games, and the scoring formula that turns a parcel's soil/water
readings into machine weights, are not decided yet — see the "Not decided yet" section of
`docs/design.md`.

## Project layout

| Path | What it is |
| --- | --- |
| `Assets/Scenes/SampleScene.unity` | The map — the scene you play from |
| `Assets/Scenes/UI.unity` | The canvas and EventSystem, loaded additively at runtime |
| `Assets/Scenes/Seeder.unity` | The seeder mini game, opened over the map from a parcel's Optimize button |
| `Assets/Scripts/` | Game behaviour — parcel state, map panning/zoom, the seeder |
| `docs/design.md` | The game design doc — what's decided about the game itself |
| `docs/art.md` | The visual language — grid, light, palette, tile rules |
| `docs/project-case-study.md` | The engineering write-up of how this was built |

## Running it

Open the project in Unity (see `ProjectSettings/ProjectVersion.txt` for the exact editor version),
open `SampleScene.unity`, and press Play. There is no separate build/lint/test step — this project
has no test assemblies, so verification is: compile cleanly, then check the result in the editor.
