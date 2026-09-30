---
name: unity-mcp-quirks
description: Use when inspecting or editing this project through Unity MCP. Covers safe scene saves, imports, compilation readiness, screenshot modes, reflection and prefab operations. Package-dependent observations must be checked against the installed tools.
---

# Unity MCP working notes

Follow `AGENTS.md` for the hard project constraints, including stopping when the session's Unity
MCP tools are missing, disconnected or failing. Reading package source can explain a tool; it does
not replace editor verification or authorize another connection route.

The package observations below were checked against source in
`Library/PackageCache/com.coplaydev.unity-mcp@045e809812a8`, with Unity `6000.3.22f1` recorded in
`ProjectSettings/ProjectVersion.txt`. They are source findings, not a fresh runtime test. Recheck
the exposed schema and installed package when a version changes; preserve the project safeguards.

## Code execution and inspection

- `execute_code` runs a method body, with `return` for output. Prefer small snippets, straightforward
  syntax and qualified names such as `UnityEngine.Object` where imports could make `Object` ambiguous.
- `compiler: "codedom"` uses `CSharpCodeProvider`. The inspected implementation does not pin a
  language version, so do not claim a fixed C# ceiling from the compiler name. Use the tool's actual
  diagnostics and installed implementation; do not confuse snippet compilation with project compilation.
- CodeDom filters its assembly references. A package type can be present in Unity yet unavailable
  by direct name in a snippet. `unity_reflect`, when exposed, can inspect the live type and signature;
  a read-only reflection lookup through `AppDomain.CurrentDomain.GetAssemblies()` can locate it too.
  `BindingFlags.NonPublic | BindingFlags.Instance` allows inspection of private instance fields.
- Edit-mode inspection does not establish that a component's runtime initialization has happened.
  Read its source and cached state before interpreting a private-method result. Do not invoke
  `Awake`, `OnEnable` or other lifecycle methods as a generic setup shortcut: they may create objects,
  subscribe events or change state twice. Verify runtime behaviour through the normal Play Mode lifecycle.
- A persistent `UnityEvent` listener with `RuntimeOnly` call state is not expected to run from an
  edit-mode `Invoke()`. Inspect its target, method, arguments and call state without changing them;
  use Play Mode to prove the real interaction rather than temporarily rewriting its call state.

## Importing and compiling

- Edit existing scripts with file editing tools rather than deleting and recreating them to satisfy
  a create-only tool. When removing an approved script, first remove or replace its component uses
  so scenes and prefabs do not retain missing-script references. Include the asset's `.meta` file.
- After adding a new script, use `refresh_unity` with `scope: "all"`, `compile: "request"` and
  `wait_for_ready: true`. In the inspected package, `scope: "scripts"` requests compilation without
  importing new files; the full refresh imports them.
- On Unity 6 the inspected `refresh_unity` skips its ready-wait when compilation was requested,
  even if `wait_for_ready` is true. A successful request is not compile completion. Confirm the
  editor is ready and compilation has finished through the session's exposed editor-state tools,
  then read the error console as required by `AGENTS.md`.
- A tool's safety rejection is not permission to perform the same operation through a different
  route. Follow the project stop rule and report the rejected operation.

## Authoring and saving scenes

- Scene edits in Play Mode are discarded on stop. Before every scene-editing `execute_code`, guard
  with `if (Application.isPlaying) return "still in play mode";` and only author in Edit Mode.
- `UI.unity` loaded at runtime by the bootstrap may disappear when Play Mode ends. Check loaded
  scenes before editing. Open `Assets/Scenes/UI.unity` additively when it is absent; keep
  `Seeder.unity` and `Fertilizer.unity` closed while working on the map as required by `AGENTS.md`.
- The inspected `manage_scene` save implementation saves the **active scene**. A different supplied
  path can perform **Save As** on that scene; it does not select another loaded scene. Never pass the
  UI path as a way to select it while Map is active. Save the intended loaded scene explicitly:
  `EditorSceneManager.SaveScene(SceneManager.GetSceneByPath("Assets/Scenes/UI.unity"))`.
  Check the scene is valid and loaded first, then check the returned bool, its path and `isDirty`.
- After changing build settings, confirm `ProjectSettings/EditorBuildSettings.asset` on disk, not
  just `EditorBuildSettings.scenes` in memory. The project's previous persistence issue required
  `AssetDatabase.SaveAssets()` and `EditorApplication.ExecuteMenuItem("File/Save Project")`.
- **Do not force a layout rebuild immediately before saving a scene.** Previous project work found
  that `LayoutRebuilder.ForceRebuildLayoutImmediate` or `Canvas.ForceUpdateCanvases()` before saving
  flattened layout-driven `RectTransform` values in the file while live readings looked correct.
  Preserve this safeguard: save normally and inspect the scene diff for unrelated layout changes.
- `AnimationMode.StartAnimationMode()` and `SampleAnimationClip` can inspect a pose in Edit Mode.
  Sampling changes the live objects: exit animation mode and restore the original resting state
  before saving. A sampled pose verifies clip values, not runtime transitions or input wiring.
- Background Play Mode can advance differently depending on editor settings and focus. Check that
  the editor is actually running and advancing before diagnosing a frozen animation. Manually
  advancing an Animator is a pose diagnostic, not proof that its runtime timing works.

## Screenshots and UI

- A Screen Space - Overlay canvas can appear enormous beside the world in Scene view because its
  dimensions are canvas units. Do not move or scale it to fit the farm. Use Scene view framing or
  layer visibility to inspect the world and canvas separately.
- Camera-rendered captures omit Screen Space - Overlay UI. The installed package also provides
  composited capture, so the old claim that MCP cannot capture Overlay is obsolete. In the inspected
  source, `manage_camera` screenshot delegates to `manage_scene`: specifying `camera` renders that
  camera; omitting it with `include_image: true` in Play Mode selects composited capture. The same
  inline-image request in Edit Mode falls back to a camera. The default file capture uses
  `ScreenCapture` on supported Unity versions. Choose the capture mode deliberately and inspect the
  returned image before claiming that UI is visible or absent.
- Use a project-relative `output_folder` such as `Captures`; the inspected helper also accepts
  absolute paths inside the project but rejects paths outside it. Keep generated captures out of
  commits and remove only temporary artifacts created for the task when cleanup is authorized.
- Scene-view screenshots show the editor viewport, including gizmos and selection. Object framing
  by name can resolve against the active scene in the inspected package, so do not assume a named
  object in an additive scene is missing. Check loaded scenes and target identity first; use an
  untargeted viewport capture when that is sufficient to inspect the result.

## Prefabs and batches

- Instantiate a prefab with `manage_gameobject` action `create` and `prefab_path` when that schema is
  exposed. `manage_prefabs` works on prefab contents and the prefab stage; it is a different job.
- The inspected `batch_execute` defaults to 25 commands, configurable up to a hard cap of 100.
  Its Unity handler executes **all** commands sequentially on the main thread even when `parallel`
  is requested. Inspect each command's result; an outer batch response is not proof all commands worked.
