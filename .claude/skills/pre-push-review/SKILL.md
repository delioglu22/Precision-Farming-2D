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
| `CLAUDE.md` | How the project is built: scene structure, prefabs, rules, and **the numbers it quotes** (camera 6.4 / 4.8, `water_sheet` 26x14). If a tunable changed. |

The kind that slips through most often: the panel's behaviour changes and
`docs/design.md` still describes the old behaviour. File present, name correct,
sentence false — no mechanical check reaches that.

## Known false positives

Do not re-argue these on every push:

- **`ParcelFootprint.crop` and `cropLayer` are empty on Pond and Forest slabs.** Twenty
  of them turn up and all twenty are correct: a pond and a wood have no crop, they have
  water and canopy fill instead. Only the `Parcel` ones should be filled in. **A number
  other than 20 is worth a look.**
- **Nothing references a folder's GUID** — the check only looks at files anyway.
- **Scene files look unreferenced**; they are reached through the build settings.
- `SampleScene.unity` showing as modified in `git status` with an empty `git diff` is
  not a finding: Unity rewrote the file with identical content.
