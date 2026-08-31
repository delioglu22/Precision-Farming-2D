---
name: pre-push-review
description: Run a quality pass over the project before pushing commits to the branch - every file in the repo must earn its place, and the docs must not have gone stale during the session. MUST use this skill whenever the user says "let's push", "send it up", "push to remote". It also applies to "let's clean up", "is there anything unnecessary left", "any garbage code or assets", "should we update the docs".
---

# Before pushing: everything earns its place

Two questions: **does every file in the repo deserve to be there?** and **is what we
wrote still true?** Things that do not break the compile, do not show in the console
and are not noticeable while playing are never found unless they are looked for.

## How to run it

1. `read_console` `types: ["error"]` → **must be 0.** If not, no push. Not up for debate.
2. `bash .claude/skills/pre-push-review/checks.sh` — everything visible from the shell.
   It names what it is looking at in its own section headers, and deletes nothing.
3. Run the two blocks in `unity-checks.md` — the ones that need the editor. They do not
   work in play mode; press Stop first, then reopen `UI.unity` additively.
4. Do the documentation read below — that is the half `checks.sh` cannot reach.
5. **Report what you find. Do not delete it and do not fix it yourself.** What goes,
   and what gets rewritten, is the user's call — especially with Unity's own scaffold
   files, since a package may be looking for one.

## Did the docs go stale this session

`checks.sh` only checks "is the file it names still there". The drift that matters is
semantic: the file is present, the sentence about it is wrong. Only whoever did the
session can see that.

Use `git diff --stat origin/main..HEAD` to see what is going out in this push, then ask:

| File | When it goes stale |
| --- | --- |
| `docs/design.md` | The game itself — the loop, how the panel behaves, what a parcel is. **If a behaviour changed, look here.** |
| `docs/art.md` | The look — grid, light, palette. If a new colour, layer or sprite arrived. |
| `CLAUDE.md` | How the project is built: scene structure, the parcel tilemaps, rules, and **the numbers it quotes** (camera 4.5 / 3.2, `MapPan.mapSize` 41.5 x 20.75, the forest border's reach). If a tunable changed. |

The kind that slips through most often: the panel's behaviour changes and
`docs/design.md` still describes the old behaviour. File present, name correct,
sentence false — no mechanical check reaches that.

## Known false positives

Do not re-argue these on every push:

- **`Parcel.crop` is empty on fallow parcels.** A parcel with nothing planted is
  correct, not a bug — that is what "fallow, ploughed field" means. **6 of the 27
  parcels are fallow as of this writing** — 04, 08, 14, 18, 24 and 31. **A number that
  does not match the actual fallow count is worth a look** — it means a working parcel
  lost its crop. Mind that the names run past the count: numbers were skipped when the
  map was rebuilt, so the highest is `Parcel 31` while there are 27 of them.
- **`fieldTile` and `fenceTile` null is a real state, not a break.** `Parcel.Rebuild`
  deliberately leaves soil and fence alone while either is unassigned, so a freshly
  duplicated parcel cannot clear itself to bare grid. All 27 are wired as of this
  writing, so a null one now means a parcel that was added and not finished.
- **`CustomButton` adds 22 more empty references, all of them nothing.** It extends
  `Button` and lives in `Assembly-CSharp`, so unlike a plain `Button` the scan does not
  skip it and walks its inherited `Selectable` fields too. `m_SelectOnUp/Down/Left/Right`
  are explicit-navigation targets and navigation is Automatic; `m_ObjectArgument` is a
  `UnityEvent` argument the calls do not use. **The whole-project baseline is 28** — 6
  fallow parcels plus these 22, and as of this writing nothing else shows up. Earlier
  versions of this note said 42 (assuming 8 fallow parcels and 12 further false
  positives on the pre-tilemap `ParcelFootprint`/`ParcelLayer` scripts); both of those
  are gone along with the scripts. A count under 28 is missing something real; a count
  over 28 is a new empty reference worth a look.
- **The jetty prefabs and sprites are unreferenced on purpose.** `Jetty Low`,
  `Jetty Posts` and their two PNGs came out of the ponds when a jetty was judged to
  promise boats and fishing the game will not have. They are kept for later, deliberately.
  Do not offer to delete them again.
- **`Art/UI/Bars/slider_background.png` and `slider_fill.png` are unreferenced on
  purpose** — they are for the seeder's battery bar, which is not wired yet.
- **Nothing references a folder's GUID** — the check only looks at files anyway.
- **Scene files look unreferenced**; they are reached through the build settings.
- `SampleScene.unity` showing as modified in `git status` with an empty `git diff` is
  not a finding: Unity rewrote the file with identical content.
