---
name: hand-it-over
description: Hand a piece of the work in this Unity project back to the user so they do it themselves in Unity, then wait and check the result. MUST use this skill whenever a "[YOUR TURN]" ticket arrives. It also applies when the user says "let me do this one", "leave it to me", or asks how something is done.
---

# Hand a piece of the work back

The user is making this game; you are helping. Doing all of it is fast today and
means they forget Unity tomorrow. They said so themselves. So every so often a piece
of the work goes to them.

A handover has to be a real piece. Not "you try it too" — you will not be doing that
piece, the user will, and the work will come out of their hands.

## Which piece to hand over

A good handover piece holds all four of these at once:

1. **It is done in Unity's interface.** Inspector, Animation window, Hierarchy,
   Sprite Editor, Scene view. That is the hand they are afraid of losing.
2. **There is a decision inside it.** Not just clicking — something that makes them
   think: which value, which order, which anchor.
3. **It takes 5–15 minutes.** Shorter is not a lesson; longer is drudgery.
4. **It is reversible.** A wrong move undoes with one Ctrl+Z or one field.

What has worked in this project:

| Handover | The decision inside it |
| --- | --- |
| Settling Inspector values (`focusDamping`, `edgeMargin`, `sheetCover`, the selected tint) | Which value feels right — only someone playing it knows |
| Placing a new parcel | Working out the cell rect so it sits square against its neighbours |
| A new pose clip in the Animation window | Which property to drive, which one overrides the instance |
| Adding a component to a prefab and wiring the reference | Does it belong on the prefab or on the instance |
| Settling a parcel's cell rect and crop | Which size and crop sit right next to its neighbours |
| Fitting a layer's colour into the palette | Does it follow the light, where does it land among the existing tones |
| Placing a UI element with anchors and a layout group | Solving it by layout rather than by code |
| Creating a `ScriptableObject` asset and wiring it into both scenes | Why a cross-scene reference has to be an asset |

Do not hand over:

- The middle of a half-finished refactor
- Something whose setup takes six MCP calls
- Pure typing (rewriting a file from scratch teaches nothing)
- Something you do not know how to do either
- Work the user has just said they are in a hurry on

## How to hand it over

Four lines. Do **not** give a click-by-click recipe — following a recipe teaches
nothing. Say where it lives and what "right" looks like, and let them find the order.

```
Your turn on this one:

  What    Add Parcel 32 to the north-east of the map.
  Where   SampleScene > World > Parcels, duplicated from a parcel already there.
  Why     The cells rect has to be worked out by hand so the new field sits square
          against its neighbours without overlapping any of them.
  Check   If clicking selects the new parcel, its Field collider repainted too.

Tell me when it's done and I'll check the console and the scene.
```

If they get stuck, help — but let them try once first.

## What you do while a handover is open

**Do not do that piece.** Do not idle either: finish the parts of the same job that
do not depend on it, get the script side ready, but leave the handed-over piece alone.

If the user says "you do it", do not argue. Do it and close the ticket.

## When it comes back

1. **Check it.** `refresh_unity` + `read_console` `types: ["error"]` → 0 entries. If
   the scene changed, read the state with `execute_code`; take a screenshot if needed.
2. **One line of feedback.** If it is right, say it is right. If it is wrong, say what
   is wrong and why — and let the user fix it.
3. **Close the ticket:** `node .claude/hooks/your-turn.js --close`
4. Carry on with the normal work.

## The ticket mechanism

`.claude/hooks/your-turn.js` is a `UserPromptSubmit` hook. It drops a "[YOUR TURN]"
ticket every N commits (2 by default) — that is, every N pieces of finished work. The
judgement is in this skill, the counter is in the hook; a script cannot tell which
work is worth handing over, so the choice is yours.

If a ticket is left open the hook nags three times, then drops it. So ignoring a
ticket is not an option: either hand something over, or **say in one sentence why you
did not** and close it.

The user's controls:

| | |
| --- | --- |
| `#my-turn` | in a prompt, forces a ticket immediately |
| `#you-do-it` | skip this one |
| `node .claude/hooks/your-turn.js --status` | status |
| `--every <n>` / `--off` / `--on` | frequency, and switching it off |

## Swinging too far the other way

This skill is not "dump the work on the user". If they asked for something, you are
doing it; the handover is **one piece** of the job, not the whole thing. Do not hand
over two hours of work because a ticket arrived, and do not leave the job half done
because of a handover — everything outside the handed-over piece is still yours.

If there is no suitable piece, do not invent one. Say "there is no meaningful piece to
hand over here, because ...", close the ticket, and carry on.
