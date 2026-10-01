# Precision Farming 2D — shared agent guidance

This is the canonical project guide for Codex and Claude Code. `CLAUDE.md` imports it.
Edit shared instructions here, not in a second copy.

## Sources of truth and collaboration

- `docs/design.md` records the agreed game direction, prototype assumptions and open decisions.
  Keep it short and about the game. A target described there is not proof it has been implemented.
- `docs/art.md` records the visual language: grid, light, palette and asset constraints.
- This file and scripts' comments carry implementation knowledge. An implementation plan belongs
  in a separate, task-specific document, not in the design or art document.
- `.claude/skills/` contains the canonical project skills. Each folder under `.agents/skills/`
  links to the matching Claude skill, so both tools read the same rules. Edit the canonical files.
  Preserve these relative symlinks when cloning; if a checkout cannot use symlinks, fix discovery
  before using the skills rather than creating independent copies.
- `.claude/settings.json` configures the Claude hooks. Codex does not gain those hooks merely by
  discovering the shared skills. Per-machine MCP configuration and hook state stay untracked.

Use the `game-designer` skill for game decisions, `artist` for visual work and `animation-notes`
for animation assets. The implementation and review skills are referenced in the sections below.

The user's current workflow is **Codex for design, plans and review; Claude Code for implementation
through Unity MCP**. Only one agent may write to the Unity project/editor at a time. Parallel
read-only review or work on explicitly separate documentation files is fine.

An implementation handoff states the player-visible goal, scope and exclusions, relevant current
scenes/scripts, required behaviour and acceptance checks. Identify open design decisions instead of
letting the implementer invent answers. Implement a small playable slice, then report the changed
files, saved scenes, compilation status, checks performed and remaining limits. The reviewer checks
actual diffs and relevant editor behaviour; the implementer's summary alone is not evidence.
The agent verifies technical behaviour; the user judges whether the game feels good.

## Current implementation

The build settings currently enable four scenes: `Assets/Scenes/Map.unity`, `UI.unity`,
`Seeder.unity` and `Fertilizer.unity`. `Map` is the entry scene. `SceneBootstrap` loads `UI`
additively; it owns the canvas and EventSystem. Edit UI in `UI.unity`, alongside Map if useful.
The bootstrap will not load it twice.

Seeder and Fertilizer are **legacy minigames** still present in the project, opened by
`SeederLauncher` and `DroneLauncher`. Their existence does not override the new design direction.
No button in the parcel page calls those launchers any more; their objects remain in `UI.unity`.
Keep both closed during ordinary map work: loading them in the editor also loads their overlays
when Play starts. Open them only for intentional inspection or testing, then restore the scene setup.
`LoneEventSystem` handles the spare EventSystem at runtime.

The old seeder window fits 8 x 14 cells at its normal cell size and shrinks larger fields to fit.
That is a maintenance detail of the legacy minigame, not a parcel-size constraint for the new game.

### The farming loop

`Assets/Scripts/Farm/` holds the automation prototype. `Farm` (root object in Map) owns the
wallet, the numbered equipment sets, land decisions, the per-frame tick and saving.
`FarmField` sits beside `Parcel` on participating parcels: land readings, ownership, land use
(Empty/Farm/Depot), confirmed plan and the running crop. Money moves only in `FarmField.Tick`
(crop cost at sowing, income at sale) and in `Farm`'s purchase methods, which re-check their
conditions, so UI taps and animations cannot pay twice. `FarmRules` (asset in `Assets/Settings`)
holds every tuneable number and the harvest formula; it is read, never written at runtime.
`FieldView` only presents: crop stages through `Parcel.SetCropTile`, the `Field Machines`
prefab under the parcel's Grid, the depot building and the intro's hint arrow. `FarmIntro`
advances the first minutes from game state. UI reaches the farm through the `FarmChannel`
asset; `FarmPage` binds readings and the plan draft, `LandPage` owns the sheet's action
buttons and the land/stop decisions. The save is `farm.json` in `Application.persistentDataPath`
(versioned; an unreadable file is kept and the farm waits for "Start a new farm"). Use the
`Farm` component's context menu "Delete Save File" in Edit Mode to start fresh.

## Authoring the world

Build content in Unity through MCP: create objects, configure components, wire references and save
scenes. The user must be able to inspect and adjust the authored result. Do not introduce an
edit-time generator that hides scenery inside a script. Scripts are for behaviour, such as
observation, automation, economy, parcel state and input.

**`World` must remain a common ancestor of everything clickable.** `MapPan` lives there; the
EventSystem walks up from the pressed object to find its drag handler. Moving `Parcels`, `Map` or
`Ground` outside it silently breaks map dragging.

The map uses tilemaps. `World` contains:

| Child | Purpose |
| --- | --- |
| `Ground` | Background SpriteRenderer, BoxCollider2D and DeselectOnClick |
| `Map` | Shared Grid with Path and Ponds tilemaps |
| `Woods` | Individually placed tree clusters |
| `Parcels` | Clickable fields |
| `Forest Border` | Trees extending beyond the pannable area |

Each `Parcel` owns a child Grid with `Field`, `Crops` and `Fence` tilemaps. `Parcel.cells` is its
`RectInt` geometry; editing it in the Inspector repaints those layers. `Parcel.crop` is the crop
TileBase; an empty value means fallow. Add a parcel by duplicating an existing one, renaming it and
setting `cells`, preserving its `fieldTile` and `fenceTile` references. Rebuild intentionally leaves
soil or fence untouched while the respective reference is null.

The per-parcel Grid must align with the shared Map grid at world origin. `Parcel.CacheLayers`
resets it when caching the layers; a rebuild with an already-cached Grid does not repair an
arbitrary move. The parcel's own transform may sit at its rect's centre for Frame Selected.
Selection then adds the small visual lift described below.

`OnValidate` only flags a rebuild; `Update` performs it later. Do not move tile writes into
`OnValidate`: collider notifications use `SendMessage`, which Unity forbids there, and a clear can
run before the guarded refill, wiping Crops.

`Physics2DRaycaster` on the camera and `TilemapCollider2D` on Field provide the hit test. The
EventSystem walks to `IPointerClickHandler` on Parcel and to the drag handler on World.
See `Assets/Scripts/World/Parcel.cs` for details. SpriteShape is no longer used; notes about
tracing parcel splines describe a deleted implementation.

## Use the engine before writing code

Before adding behaviour, name the built-in component that could do the job. Use it when it fits;
otherwise state the gap briefly and write the script. This check is mandatory before a new
MonoBehaviour over roughly 50 lines. Read `.claude/skills/engine-first/SKILL.md` for new behaviour
and `.claude/skills/script-style/SKILL.md` for field declarations and communication between scripts.

| Job | First option to consider |
| --- | --- |
| World taps | Physics2DRaycaster + IPointerClickHandler |
| World dragging | IBeginDragHandler / IDragHandler on a common ancestor |
| Tap-versus-drag and blocking input through UI | EventSystem routing and pixelDragThreshold |
| Shared object setup | Prefab |
| Renderers that sort together | SortingGroup |
| Animated UI presentation | Animator with explicit states |
| Tunable values | SerializeField and the Inspector |
| Data shared across scenes | ScriptableObject asset; define its runtime/save lifecycle explicitly |
| UI arrangement | Anchors and layout groups |

UnityEvents carry at most one static argument. A Button cannot directly call
`Animator.SetBool(name, value)` with both arguments; `ParcelPanel.SetExpanded(bool)` is the existing
bridge. Unconsumed Animator triggers can latch and unexpectedly fire on the next panel open.
Use the animation skill when touching controllers or clips. Gameplay simulation and resource
accounting belong in behaviour code, not presentation timelines.

## Working with the editor

Use the session's **Unity MCP** tools (`com.coplaydev.unity-mcp`; the configured server name may
appear as `mcp__UnityMCP__*` or `mcp__unityMCP__*`). Check the selected project and editor state
before operating. There is currently no separate CLI game build/test loop or test assembly;
Unity changes are verified in the Editor.

**If Unity MCP is disconnected or failing, stop and tell the user.** Never replace it with a shell
compiler, a guess about compilation, UI automation or a direct request to the server's HTTP endpoint.
If the session lacks its Unity tools, let the user reconnect the MCP connection or restart the
session. Do not claim editor verification from a config file or a green connection screenshot.

Read `.claude/skills/unity-mcp-quirks/SKILL.md` before editor work. After Unity changes:

1. Request `refresh_unity` with `compile: "request"` and `wait_for_ready: true` when scripts changed.
   On the installed Unity 6 integration, a compile request can return before compilation finishes.
   Confirm compilation has completed and the editor is ready before continuing.
2. Read the console with `types: ["error"]`. It must contain zero errors before claiming success;
   investigate errors rather than clearing them to pass. A WebSocket receive warning during domain
   reload is not itself a code error, but loss of tool access still requires stopping.
3. Inspect the affected behaviour in the editor, including input and UI where relevant. Capture
   visual evidence with a capture mode that actually includes the content under review.
4. Explicitly save each modified scene in edit mode and verify its path and clean state. With
   additive scenes, do not pass another scene's path to a save of the active scene; that can Save As
   over the other file. Use the scene-handle approach in the quirks skill.

Documentation-only changes need document, path and consistency checks. Hook/check-script changes
need syntax and relevant isolated behaviour checks; they do not require a Unity compilation.
Do not add test assemblies solely to validate such changes.

## Rendering and camera constraints

- The project uses Linear colour space. SpriteRenderer tint conversion is automatic; mesh vertex
  colours need `color.linear`.
- Selection tints cells in place: `Parcel.Warm` multiplies their existing colours and `Restore`
  reinstates them, cached per cell. `SetTileFlags(cell, TileFlags.None)` must precede tint writes.
  Runtime crop changes go through `Parcel.SetCropTile`, which tints a new tile while selected
  and keeps the cache correct; never repaint growth with `Rebuild`.
- Selection also raises the grid by `Parcel.lift` (0.10). Keep it below 0.24, one cell's vertical
  step, to avoid sorting past the row in front. Parcels currently do not use an Animator for this.
- Temporary editor helper objects must not be saved into scenes/builds. Authored content must be
  saved. Use appropriate `HideFlags` for genuinely transient objects.
- `MapPan` starts at `maxZoomSize` (9); wheel/pinch spans `minZoomSize` (3) to that maximum.
  Selection closes to `pickedSize` (5); deselection returns to the user's previous zoom.
  These are tunable values. The camera is portrait, so orthographic size is its vertical half-span.
- Wheel input bubbles through `IScrollHandler`. UGUI has no pinch interface; the existing pinch
  code uses EnhancedTouch and enables/disables EnhancedTouchSupport with its lifecycle.
- Forest Border overhangs the pannable area (currently about ±36 x ±20 around a 41.5 x 20.75 pan
  area). Check the border at all pan extremes before increasing the maximum zoom. The deleted
  `water_sheet` sprite is no longer a zoom constraint.

## Learning handoffs, commits and review

The user wants to learn Unity as well as build the game. When a `[YOUR TURN]` ticket arrives, use
`.claude/skills/hand-it-over/SKILL.md` to hand over a meaningful, bounded editor task, wait and check
the result. Do not manufacture a Unity task during documentation-only work. Explain when there is
no suitable piece. The hook is `.claude/hooks/your-turn.js`, configured for Claude Code; it counts
commits, not messages. Close a handled ticket with `node .claude/hooks/your-turn.js --close`.
The user can use `#my-turn`, `#you-do-it`, `--every <n>` and `--off`/`--on` as documented in the skill.

Follow `.claude/skills/incremental-commits/SKILL.md`:

- **Every commit, including a snapshot, needs the user's explicit approval.** Finish and verify
  the authorized change, summarize it and show the proposed message before waiting for approval.
- Split substantial work into independently reviewable pieces when useful; do not invent a fixed
  number of pieces for a small change. Request an approved checkpoint before a risky refactor.
- Never commit Unity code whose compilation could not be verified in the Unity console.
- Messages are English, imperative, one line, at most 72 characters: `<type>: <what>` using
  `feat`, `fix`, `refactor` or `chore`. No co-author lines or tool signatures.
- Include `.meta` files with assets and all changed scene files. Include new shared guidance and
  skill links when committing agent setup; leave machine-specific settings and counters out.

Use `.claude/skills/pre-push-review/SKILL.md` before a push or requested cleanup. Review findings
alone do not authorize deletion. Fixes the user has already authorized can proceed within scope.
Keep accepted design and implementation knowledge current as part of each change.

## Language and dictation

Respond in the user's requested language; Turkish is welcome. Keep repository documentation,
code, comments, skills and commit messages in English. Read ordinary dictation errors by context
without remarking on spelling. If a dictated filename, path or literal command is ambiguous and
cannot be resolved from the supplied context, ask instead of silently editing a guessed target.
