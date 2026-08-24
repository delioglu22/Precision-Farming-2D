# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Precision Farming 2D** — a game about automating farmland and optimizing that automation.
See `docs/design.md` for the core loop and what has actually been decided; keep that document short
and about the game, not about implementation. `docs/art.md` is its sibling for how the game looks —
the grid, the light, the palette, and what a new piece of art has to obey. Neither document carries
implementation; that lives in this file and in the scripts' own comments.

Three scenes. `Assets/Scenes/SampleScene.unity` is the one you play from; `Assets/Scenes/UI.unity`
holds the canvas and the EventSystem and is pulled in additively by `SceneBootstrap`;
`Assets/Scenes/Seeder.unity` is the seeder's mini game, laid over the top by `SeederLauncher` when
the parcel page's Optimize button is pressed and taken away again by its own Close button. All three
are in the build settings. Edit the UI by opening `UI.unity` — alongside the map scene is fine, the
bootstrap will not load it twice.

**Leave `Seeder.unity` closed while working on the map.** Left open in the editor it is loaded like
any other scene, so pressing Play draws its canvas over everything and the editor logs a second
EventSystem. At runtime that never happens: it is loaded on demand and `LoneEventSystem` stands the
spare one down.

**`World` has to stay a common ancestor of everything clickable.** The EventSystem finds a drag
handler by walking *up the hierarchy* from whatever was pressed, and `MapPan` lives on `World`.
Move `Parcels` or `Water` out from under it and dragging the map silently stops working.

Three prefabs are built from the same slab: `Parcel.prefab` (clickable, has a crop),
`PondSlab.prefab` (two steps of water) and `ForestSlab.prefab` (a canopy fill). They are siblings,
not variants, so a change to the slab's layers has to be made in all three.

**Adding a parcel**: instantiate the prefab, then set its name, position, `ParcelFootprint.footprint`
and the `SortingGroup`'s order. Nothing else. There is no sprite to pick and no collider to fit — the
five layers are traced from the footprint and `autoUpdateCollider` bakes the outline.

The group's order is a baked topological depth, so a new parcel needs the whole order recomputed —
the rule is that A draws before B when B sits further along an isometric axis and they overlap on the
other one. It lives on the `SortingGroup`, not on a renderer: the renderers inside a parcel use 0–4
for their own stacking and must stay that way.

See `docs/art.md` for what a parcel looks like and `ParcelFootprint` / `ParcelLayer` for how it is
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

## SpriteShape and animation traps

Both sets of hard-won traps now live in skills, so they load only when the work needs them:
`.claude/skills/spriteshape-notes/` (fill textures, baking order, edge sprites, stale screenshots) and
`.claude/skills/animation-notes/` (clip properties, growing the panel, path bindings).

## Rendering notes

- The project is **Linear** color space. `SpriteRenderer.color` and `SpriteShapeRenderer.color` both
  convert automatically; `Mesh.SetColors` does not — write mesh vertex colors as `color.linear`.
- A parcel's flat colours (outline, rim, both side faces) are **renderer tints over one white
  texture**, not one texture per colour. Adding a colour means a swatch, not a file.
- Editor-generated objects should carry `HideFlags.DontSaveInEditor | DontSaveInBuild` if they are
  transient, or they bloat the scene file. Authored content is the opposite: it belongs in the scene.
- The view rests at orthographic size **6.4** and closes to **4.8** while a parcel is open, both
  driven by `MapPan` and both tunable in the Inspector. 6.4 is a ceiling rather than a taste: the
  water sprite is 26 x 14 world units, so a taller view runs off its edge and shows the camera's
  clear colour. Pulling further back means enlarging `water_sheet` first.

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
