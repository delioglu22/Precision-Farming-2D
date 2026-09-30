---
name: pre-push-review
description: Review the outgoing changes, unused content and documentation drift before a requested push or repository cleanup audit. Use for "push to remote", "anything unnecessary left" and explicit project quality reviews; ordinary documentation edits do not require a full project audit.
---

# Before pushing: everything earns its place

Two questions: **does every file in the repo deserve to be there?** and **is what we
wrote still true?** Things that do not break the compile, do not show in the console
and are not noticeable while playing are never found unless they are looked for.

## How to run it

1. Identify the review scope and outgoing changes. For changes to Unity code, scenes or
   assets, refresh/compile through Unity MCP, confirm compilation has finished and the
   editor is ready, then read the error console: **must be 0**. A successful compile
   request alone does not prove completion on the installed Unity 6 integration.
   If Unity verification is unavailable, stop that part and report it as unverified.
   Documentation, skills and development-hook changes alone do not require the editor.
2. `bash .claude/skills/pre-push-review/checks.sh` — everything visible from the shell.
   It names what it is looking at in its own section headers, and deletes nothing.
3. For a full Unity audit or relevant Unity changes, run the checks in `unity-checks.md`
   while the editor is stopped. The reference scan covers all enabled build scenes,
   including Seeder and Fertilizer, and closes any preview scenes it opened.
4. Do the documentation read below — that is the half `checks.sh` cannot reach.
5. **Report findings before expanding the task.** A review request is not permission
   to delete assets or rewrite unrelated behavior. Fix issues already covered by the
   user's authorization; otherwise propose the concrete changes for approval. An
   unused-GUID report is a lead to inspect, never proof that an asset should be deleted.

## Did the docs go stale this session

`checks.sh` only checks "is the file it names still there". The drift that matters is
semantic: the file is present, the sentence about it is wrong. Only whoever did the
session can see that.

Resolve the actual target branch and comparison base from the branch/PR context; do
not assume `origin/main`. Inspect its diff against the work being reviewed, including
uncommitted changes when those are in scope. For a push, inspect the commits absent
from the destination branch as well. Then ask:

| File | When it goes stale |
| --- | --- |
| `docs/design.md` | The game itself — the loop, how the panel behaves, what a parcel is. **If a behaviour changed, look here.** |
| `docs/art.md` | The look — grid, light, palette. If a new colour, layer or sprite arrived. |
| `AGENTS.md` | Canonical project instructions: scene structure, engine rules and any quoted tuning values. If behavior, authoring conventions or those values changed. |
| `CLAUDE.md` | The Claude entrypoint should still import the canonical instructions rather than duplicate them. |
| `.claude/skills/` and `.claude/hooks/` | Shared workflows, commands and hook behavior. Check paths and assumptions when the workflow changes; `.agents/skills` exposes the same skills to Codex. |

The kind that slips through most often: the panel's behaviour changes and
`docs/design.md` still describes the old behaviour. File present, name correct,
sentence false — no mechanical check reaches that.

## Known false positives

Do not re-argue these on every push:

- **`Parcel.crop` is empty on fallow parcels.** Confirm the parcel is meant to be
  fallow before treating this as a missing assignment. Do not infer correctness from
  a fixed total count of empty references; the content changes over time.
- **`SeederField.title` is optional.** Its tooltip marks it optional and the display
  update checks for null; an unassigned title alone is not a broken seeder.
- **`fieldTile` and `fenceTile` null is a real state, not a break.** `Parcel.Rebuild`
  deliberately leaves soil and fence alone while either is unassigned, so a freshly
  duplicated parcel cannot clear itself to bare grid. Inspect whether an empty one
  belongs to an intentionally incomplete object or prevents a finished parcel working.
- **`CustomButton` can include deliberately empty inherited references.** It extends
  `Button` and lives in `Assembly-CSharp`, so unlike a plain `Button` the scan does not
  skip it and walks its inherited `Selectable` fields too. `m_SelectOnUp/Down/Left/Right`
  are explicit-navigation targets and navigation is Automatic; `m_ObjectArgument` is a
  `UnityEvent` argument the calls may not use. Check the current navigation mode and
  event signature before classifying each empty field.
- **The jetty prefabs and sprites are unreferenced on purpose.** `Jetty Low`,
  `Jetty Posts` and their two PNGs came out of the ponds when a jetty was judged to
  promise boats and fishing the game will not have. They are kept for later, deliberately.
  Do not offer to delete them again.
- **Nothing references a folder's GUID** — the check only looks at files anyway.
- **Scene files look unreferenced**; they are reached through the build settings.
- `Map.unity` showing as modified in `git status` with an empty `git diff` is
  not a finding: Unity rewrote the file with identical content.
