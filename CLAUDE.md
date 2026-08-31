# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Precision Farming 2D** — a game about automating farmland and optimizing that automation.
See `docs/design.md` for the core loop and what has actually been decided; keep that document short
and about the game, not about implementation. `docs/art.md` is its sibling for how the game looks —
the grid, the light, the palette, and what a new piece of art has to obey. Neither document carries
implementation; that lives in this file and in the scripts' own comments.

Three scenes ship. `Assets/Scenes/SampleScene.unity` is the one you play from; `Assets/Scenes/UI.unity`
holds the canvas and the EventSystem and is pulled in additively by `SceneBootstrap`;
`Assets/Scenes/Seeder.unity` is the seeder's mini game, laid over the top by `SeederLauncher` when
the parcel page's Optimize button is pressed and taken away again by its own Close button. Those three
are in the build settings. Edit the UI by opening `UI.unity` — alongside the map scene is fine, the
bootstrap will not load it twice.

`Assets/Scenes/Sandbox.unity` is a fourth scene file and is **deliberately not in the build settings**
— it is a workbench, where the woods were arranged before being moved into the map. Nothing loads it
at runtime. Leave it out of the build settings unless it stops being scratch.

**Leave `Seeder.unity` closed while working on the map.** Left open in the editor it is loaded like
any other scene, so pressing Play draws its canvas over everything and the editor logs a second
EventSystem. At runtime that never happens: it is loaded on demand and `LoneEventSystem` stands the
spare one down.

**`World` has to stay a common ancestor of everything clickable.** The EventSystem finds a drag
handler by walking *up the hierarchy* from whatever was pressed, and `MapPan` lives on `World`.
Move `Parcels`, `Map` or `Ground` out from under it and dragging the map silently stops working.

The map is built from **tilemaps**, not from prefabs. `World` holds five children:

| Child | What it is |
| --- | --- |
| `Ground` | One `SpriteRenderer` under everything, with a `BoxCollider2D` and `DeselectOnClick` so a click on bare ground closes the panel |
| `Map` | One shared `Grid` carrying the `Path` and `Ponds` tilemaps |
| `Woods` | `Wood 1`..`Wood 6`, each a cluster of individually placed tree sprites |
| `Parcels` | `Parcel 01`..`Parcel 27`, the clickable fields |
| `Forest Border` | ~900 trees ringing the map so the view never runs off into the clear colour |

**A parcel owns its own geometry.** Each `Parcel` carries a child `Grid` with three tilemaps —
`Field`, `Crops` and `Fence`. `Parcel.cells` is a `RectInt` of tilemap cells; editing it in the
Inspector repaints all three to match, and that is what "designing a parcel" means now. What is
planted is a `TileBase` in `Parcel.crop`, and empty means fallow.

**Adding a parcel**: duplicate an existing one, rename it, set `cells`. `fieldTile` and `fenceTile`
come across with the duplicate, which is the point — `Rebuild` leaves soil and fence untouched while
either is null, so a half-wired parcel cannot clear itself to bare grid.

**The per-parcel `Grid` must sit at the world origin.** `Parcel.CacheLayers` forces it back to
`(0,0,0)` on every rebuild whatever its parent is doing, so its cells line up with the shared `Map`
grid. The parcel's own transform is free to sit at its rect's centre — that is what makes Frame
Selected land somewhere useful.

**`Rebuild` is deferred by one frame on purpose.** `OnValidate` only raises a flag and `Update` does
the work, because `Tilemap.SetTile` notifies the collider through `SendMessage`, which Unity forbids
from inside `OnValidate`. Calling it directly logs a warning *and* still runs the clear before the
guarded refill, wiping `Crops`.

Clicking needs no code of ours beyond raising the pick: a `Physics2DRaycaster` on the camera and a
`TilemapCollider2D` on the `Field` child do the hit test, and the EventSystem walks up to
`IPointerClickHandler` on `Parcel` — the same walk `MapPan` relies on to catch a drag.

See `docs/art.md` for what a parcel looks like and `Assets/Scripts/World/Parcel.cs` for how it is
put together.

The two hats are skills as well, and nothing else here points at them: `.claude/skills/game-designer/`
for talking about the game itself, and `.claude/skills/artist/` for anything touching a colour, a tile
or an import setting.

Note: this file, `.claude/skills/` and `.claude/hooks/` are tracked, so the instructions and the
project skills travel with the repo to another machine. Only per-machine state is ignored:
`.claude/settings.local.json`, the `your-turn` counter and the scheduler lock. `docs/` is tracked
normally.

## Build the scene, don't generate it

The user wants this project built **the way a game developer builds one**: author the scene in Unity
through the MCP tools — create GameObjects, add and configure components, wire references, save the
scene — so the result is real, inspectable, editable content in `SampleScene.unity`.

Do **not** reach for a procedural generator script that builds the world at edit time. An early
component generated the whole parcel map from code and was deleted for exactly this reason: it put
the game's content inside a script instead of in the scene, where the user cannot see or adjust it.

Write scripts for **game behaviour** — automation, economy, parcel state, input. Not for producing
scenery that should have been placed.

## Use the engine before writing code

Unity already does most of what a small game needs. Before writing a MonoBehaviour, **name the Unity
component that would do this job**. If one exists, use it. If none does, say so in one sentence and
then write the script. This is mandatory for any new script over ~50 lines.

Not theoretical: a 138-line component was written to detect taps — pointer tracking, a drag
threshold, `Physics2D.OverlapPointAll`, front-most sorting, and a "did this land on the UI" check.
A `Physics2DRaycaster` on the camera plus `IPointerClickHandler` does all five. It was deleted in
full and nothing was lost.

| Job | Reach for |
| --- | --- |
| Clicks/taps on world objects | `Physics2DRaycaster` on the camera + `IPointerClickHandler` |
| Dragging the world around | `IBeginDragHandler` / `IDragHandler` on a common ancestor — never poll the device |
| A tap that must not fire mid-drag | Already handled — `EventSystem.pixelDragThreshold` |
| A click or drag that must not pass through UI | Already handled — the EventSystem consumes it |
| Many objects that share a setup | A prefab, so one edit reaches all of them |
| Several renderers that must sort as one thing | A `SortingGroup` — one number for the object, 0..n inside it |
| Showing, hiding or sliding UI | `Animator`: a bool and two poses, blended by a transition |
| Values worth tuning | `[SerializeField]` and the Inspector, not constants in code |
| State shared across scenes | A `ScriptableObject` asset both scenes reference |
| Arranging UI | Anchors and layout groups, not code that sets positions |

Wiring a `Button` to that Animator is where the table gets sharp. A `UnityEvent` persistent call
carries **at most one static argument**, so it cannot reach `Animator.SetBool(name, value)`.
`SetTrigger(name)` fits the signature but an unconsumed trigger latches and fires on the next open —
tap the sheet's action button twice and the next panel expands on its own.
`ParcelPanel.SetExpanded(bool)` exists for exactly this: one public method taking one bool, which a
Button can call straight from the Inspector.

The rule is "name the alternative", **not** "never write scripts". `MapPan` exists because Unity has
no built-in map panning, and that is a fine reason.

The failure mode to watch for is not ignorance of the API — it is preferring self-contained code
because it is easier to verify programmatically. Resist that and verify the engine's behaviour instead.

`.claude/skills/engine-first/SKILL.md` carries the same rule and fires whenever new
behaviour is being added.

## Working with the editor

There is no CLI build/lint/test loop. Everything runs through the Unity Editor over the
**Unity MCP** server (`com.coplaydev.unity-mcp`, tools are `mcp__UnityMCP__*`). No test assemblies or
`.asmdef` files exist, so there is nothing to run with the Test Framework — verification means
*compile cleanly, then inspect the result in the editor*.

**If Unity MCP is disconnected or failing, stop and tell the user. In every case, without exception.**
Do not improvise a substitute route: not compiling with Unity's Roslyn from the shell, not guessing
that it probably compiles, and **not driving the MCP server yourself over its HTTP endpoint**
(`http://127.0.0.1:8080/mcp`) when the session's own `mcp__UnityMCP__*` tools are missing. "It is the
same server, so the verification is identical" is not an exception — it was tried, it worked, and the
user still ruled it out. Say the tools are missing and let the user reconnect with `/mcp` or restart
the session. This is a standing instruction from the user.

Normal loop after changing anything:

1. `refresh_unity` with `compile: "request"`, `wait_for_ready: true`
2. `read_console` with `types: ["error"]` — must return 0 entries before you claim it works.
   A `MCP-FOR-UNITY: [WebSocket] Unexpected receive error` warning is domain-reload noise, not a code error.
3. `manage_gameobject` / `manage_components` to author, `manage_camera` action `screenshot` to look at
   the result, `manage_scene` action `save` when scene state changed.

Scene edits are only persisted by an explicit save, and with both scenes open that call does not
always reach the one you mean — see `.claude/skills/unity-mcp-quirks/` before saving `UI.unity`.

The editor's tool quirks — the ones that each cost a round trip — now live in
`.claude/skills/unity-mcp-quirks/`, so they load only when the work touches the editor:
CodeDom's C# 6 limit, importing a new script, scene edits lost in play mode, the Overlay
canvas that screenshots cannot capture, and how to instantiate a prefab.

## Animation traps

The hard-won ones live in `.claude/skills/animation-notes/` (clip properties, growing the panel,
path bindings), so they load only when the work needs them.

**SpriteShape is gone.** Parcels, ponds and woods were rebuilt on tilemaps and nothing in the project
uses `SpriteShapeController` any more. If a note anywhere still describes tracing a spline from a
footprint, it is describing a system that was deleted.

## Rendering notes

- The project is **Linear** color space. `SpriteRenderer.color` converts automatically;
  `Mesh.SetColors` does not — write mesh vertex colors as `color.linear`.
- A picked parcel is tinted **per cell**: `Parcel.Warm` multiplies each tile's existing colour by
  `highlight` and `Parcel.Restore` puts the old colours back, so a tile that already carried a tint
  keeps it. `SetTileFlags(cell, TileFlags.None)` has to come first or the colour is ignored.
- A picked parcel also rises by `Parcel.lift` (0.10). Keep it under 0.24 — one cell's step up the
  screen — or a lifted row sorts past the row in front of it.
- Editor-generated objects should carry `HideFlags.DontSaveInEditor | DontSaveInBuild` if they are
  transient, or they bloat the scene file. Authored content is the opposite: it belongs in the scene.
- **Zoom is player-controlled, not fixed.** `MapPan` starts the camera at `maxZoomSize` (9, a wide
  view of the farm) every time the map loads, and the mouse wheel or a two-finger pinch moves it
  between `minZoomSize` (3) and `maxZoomSize`, both tunable in the Inspector. Picking a parcel still
  overrides this and closes to the fixed `pickedSize` (5); releasing the pick eases back to
  wherever the wheel or a pinch last left it, not back to a fixed resting size. The camera is
  **portrait** (aspect ≈ 0.56), so the horizontal half-width is always noticeably less than the
  orthographic size itself.
- **The wheel rides the same EventSystem bubbling as the drag.** `MapPan` implements `IScrollHandler`
  alongside the drag interfaces, so a scroll over any collider under `World` reaches it exactly the
  way a drag does — no separate wiring. **Pinch has no such handler.** UGUI has no multi-touch
  gesture interface, so it is read every frame from the new Input System's `EnhancedTouch.Touch.
  activeTouches` instead — two active touches, the distance between them compared frame to frame.
  `EnhancedTouchSupport.Enable()`/`Disable()` in `OnEnable`/`OnDisable` is required for that API to
  report anything.
- **The old zoom ceiling is void.** It used to be the `water_sheet` sprite's 26 x 14 edge; that sprite
  is gone and water is tiles now. What keeps the camera's clear colour off screen at the widest zoom
  is the `Forest Border`, which reaches ±36 x ±20 while `MapPan.mapSize` clamps panning to
  41.5 x 20.75 — so the border always overhangs the pannable area. Raising `maxZoomSize` much further
  needs the border to grow with it.

## Hand part of the work back

The user is learning this engine, not outsourcing it, and said so: doing everything for them costs
them the skill. Every couple of commits a `UserPromptSubmit` hook drops a `[YOUR TURN]` ticket into
the conversation. When one arrives, pick one piece of the current task that the user can do by hand
in Unity, hand it over, wait, then check the result.

`.claude/hooks/your-turn.js` only keeps the count — a shell script cannot tell a good handover task
from a terrible one. The judgement is in `.claude/skills/hand-it-over/SKILL.md`: what makes
a piece worth handing over, how to describe it without writing a click-by-click recipe, and how to
verify it afterwards. An ignored ticket nags three times and then lapses, so an unanswered ticket is
not a way out — either hand something over or say in one sentence why there is nothing suitable, then
close it with `node .claude/hooks/your-turn.js --close`.

The clock is commits rather than messages, because `incremental-commits` already splits work into
pieces that each land as one commit. The user tunes it with `--every <n>`, `--off`/`--on`, and can
force or skip a ticket by putting `#my-turn` or `#you-do-it` in a prompt.

## Commits

`.claude/skills/incremental-commits/SKILL.md` governs this. The parts that
bite most often:

- **Never commit without the user's explicit approval.** Summarize the change in one line, show the
  proposed message, then stop and wait.
- **Never commit code whose compile state you could not verify** in the Unity console.
- Split work into 2–6 pieces that each compile and run on their own; commit before a risky refactor.
- Messages: English, imperative, one line, ≤72 chars, `<type>: <what>` with `feat` / `fix` /
  `refactor` / `chore`. **No** `Co-Authored-By` line, no "Generated with Claude Code", no tool signature.
- Commit `.meta` files alongside their assets, and the `.unity` scene when it changed.

## Before pushing

`.claude/skills/pre-push-review/` runs a quality pass before commits leave for the branch — one
question, does every file in the repo earn its place. It reports and never deletes; what goes is
the user's call. The checks are in `checks.sh` and `unity-checks.md`, and the known false positives
are in the skill so they are not re-argued every push.

## Dictation

The user speaks their prompts through voice dictation, in English. Everything is English — replies,
code, comments, commit messages, and the skills under `.claude/skills/`.

Dictation is reliable for ordinary prose and unreliable for everything else. Words arrive merged,
dropped or replaced by a homophone, so read the meaning from context and carry on without remarking
on the spelling.

**The exception is a file name, a path or a literal command: never guess at one of those, ask.**
Those are exactly what dictation mangles — "cloud dot m d" is `CLAUDE.md` and "dot cloud folder" is
`.claude/`, and a name that arrives one syllable wrong points at a file that exists but is not the
one meant. Guessing there edits the wrong thing silently.
