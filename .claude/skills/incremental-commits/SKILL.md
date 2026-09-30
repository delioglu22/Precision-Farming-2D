---
name: incremental-commits
description: Keep implementation changes in independently verifiable pieces and obtain explicit approval before every commit in this project. Use for features, fixes, refactors and requested commits; documentation-only work needs document checks, not Unity verification.
---

# Commit in small pieces

Split substantial implementation work into small, working pieces. A focused change
can be one piece; do not split it just to reach a prescribed number of commits.

The reason: when something breaks, the user needs a known working point to return to.

## How it goes

1. **Plan substantial work first.** Choose pieces that can each be verified on their
   own. Show the plan briefly, then start. A small change does not need a separate plan.
2. **Finish one piece.** Only the changes belonging to that piece.
3. **Verify the changed behavior.** For Unity code, scenes or assets, request a refresh
   and compile through Unity MCP. Confirm compilation has finished and the editor is
   ready before reading the error console; the Unity 6 tool can return before its
   requested compile completes. Resolve errors and inspect the
   changed behavior in the editor. If Unity MCP is unavailable, stop the Unity work and
   report that verification is blocked; never guess that it compiles. For a change
   containing only documentation, skills or development hooks, check the text, links,
   configuration or hook behavior as appropriate; Unity compilation is not required.
4. **Ask for approval; never commit on your own.** Summarize what you did in one
   sentence, write the commit message, show it to the user. Then stop.
   Wait for explicit approval of that commit. For gameplay changes, give the user a
   concrete interaction to try in Unity before asking for acceptance.
   If they report a problem, fix it first, then ask again.
5. **Move to the next piece.** Repeat until all pieces are done.
6. **Summarize at the end.** Tell the user which commits landed, as a one-line list.

Compilation and technical behavior checks are your job. The user decides whether the
result feels right and is accepted. Report what you actually checked and any remaining
limits; a clean console alone does not prove the interaction works.

## How big is a piece

The right size: a working change that can be described in a single sentence.

Good pieces:
- "The data class that holds parcel state, and its defaults"
- "Horizontal map panning"
- "Parcel selection and the selected look"
- "The bottom panel opening and closing"

Bad pieces:
- "Build the main screen" (too big — splits into the four above)
- "Rename a variable" (too small — it rides along with the next piece)

## Commit before a risky change

Before a risky refactor, identify a known working commit to return to. If uncommitted
work needs a checkpoint, verify it, show exactly what belongs in the checkpoint and
ask for explicit approval before committing. Snapshot commits have no approval exemption.
Do not fold unrelated user changes into that checkpoint.

## Never commit

- Code that does not compile
- Code that leaves errors in the Unity console
- Unity code whose compile state you could not verify through Unity MCP
- A piece the user has not approved
- `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/` — these belong in
  `.gitignore`. If they are not there, fix `.gitignore` first, then carry on
- Unrelated changes in the same commit

## The commit message

English, short, imperative. This project will sit in the user's portfolio, so the
messages should read well.

Format: `<type>: <what was done>`

Types: `feat` (new feature), `fix` (bug fix), `refactor` (restructuring with no
change in behaviour), `chore` (configuration, gitignore, packages)

```
feat: add horizontal map panning with clamped bounds
feat: show parcel info panel on selection
fix: charge bar not updating during drag
chore: add Unity gitignore
```

No body; one line is enough. Keep the message under 72 characters.

Do **not** add a `Co-Authored-By` line, a generated-by line, or any
other tool signature. Just the single-line message.

The message describes the final state of the code, not the trial and error along the
way. "I tried this first, it didn't work, then I did that" never goes in a message.

## Unity-specific

- Commit `.meta` files too — Unity loses references without them
- If the scene file (`.unity`) changed, include it in the relevant commit
- Prefab (`.prefab`) changes travel with their `.meta` file
- ScriptableObject assets are committed with both the `.asset` and the `.meta` file

## What to tell the user

Two lines after each piece: what was done, and the proposed commit message. Then wait
for approval. Do not write a long explanation; the user wants to see progress, not a
report.

```
Parcel selection added — tapping a parcel highlights its outline.
Proposed commit: feat: show parcel info panel on selection
Could you try it in Unity and approve?
```

When the work is finished, give a short list:

```
3 commits landed:
  feat: add parcel data model
  feat: add horizontal map panning
  feat: show parcel info panel on selection
```
