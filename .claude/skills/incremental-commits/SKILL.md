---
name: incremental-commits
description: Work in small pieces in this Unity project and commit after each working piece. MUST use this skill whenever the user asks for a feature, a screen, a system or a fix — even when the word "commit" never comes up. Any job that touches more than one file, has more than one step, or takes longer than a few minutes is in scope. Only a one-line fix can skip it.
---

# Commit in small pieces

There is no single giant commit in this project. Work is split into small, working
pieces and each piece is committed on its own.

The reason: when something breaks, the user needs a point to go back to. One big
commit is worth nothing in a "it worked, then it stopped working" situation.

## How it goes

1. **Plan first.** Split the work into 2–6 pieces. Each piece must leave a state that
   compiles and runs on its own. Show the plan to the user briefly, then start.
2. **Finish one piece.** Only the changes belonging to that piece.
3. **Verify the compile.** Read the console through Unity MCP. If there is a compile
   error or a console error, do NOT commit — fix it first.
   **If you cannot read the console, do NOT commit.** Tell the user and wait. Never
   guess that "it probably compiles".
4. **Ask for approval; never commit on your own.** Summarize what you did in one
   sentence, write the commit message, show it to the user. Then stop.
   Wait until they have tried it in Unity and approved. Commit once approval comes.
   If they report a problem, fix it first, then ask again.
5. **Move to the next piece.** Repeat until all pieces are done.
6. **Summarize at the end.** Tell the user which commits landed, as a one-line list.

Checking the compile is your job; checking how it plays is the user's. You cannot
play the game — never assume "it works fine".

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

Before a large refactor, an architectural change or anything touching many files,
commit the current working state. If the change goes badly the user can go back.

You do not need approval for that commit — nothing new is being added, only the
already-working state being saved.

## Never commit

- Code that does not compile
- Code that leaves errors in the Unity console
- Code whose compile state you could not verify
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

Do **not** add a `Co-Authored-By` line, a "Generated with Claude Code" line, or any
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
