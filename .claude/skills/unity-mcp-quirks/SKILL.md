---
name: unity-mcp-quirks
description: MUST use this skill when working in the editor through the Unity MCP tools. It carries the traps on execute_code, refresh_unity, manage_gameobject, manage_camera, batch_execute and unity_reflect that have each cost a round trip: CodeDom's C# 6 limit, importing a new script, scene edits lost in play mode, the Overlay canvas that screenshots cannot capture, and how to instantiate a prefab.
---

# Tool quirks that will otherwise cost you a round trip

- `execute_code` with `compiler: "codedom"` is **C# 6 only** — no local functions, no interpolated
  strings, no `?.`. `Object` is an ambiguous reference there; write `UnityEngine.Object`.
- `execute_code` runs as a method body with `return` for output. Reflection
  (`BindingFlags.NonPublic | BindingFlags.Instance`) is the way to inspect private component state.
- `manage_camera` action `screenshot` needs a **project-relative** `output_folder` (e.g. `Captures`);
  absolute paths outside the project are rejected. That folder is not gitignored — delete it when done.
- `AssetDatabase.DeleteAsset` is blocked by MCP safety checks. Delete files from the shell instead.
- When deleting a script, remove the GameObject that uses it **first**, or the scene keeps a missing
  script reference.
- `create_script` refuses to overwrite an existing file. Use `Write`/`Edit`, or remove the file first.
- `refresh_unity` with `scope: "scripts"` **does not import new files** — a brand new `.cs` will not
  compile and you get `CS0246: type could not be found`. Use `scope: "all"` after adding a file.
- **The editor is often left in play mode.** Scene edits made there are silently discarded on stop, so
  every scene-editing `execute_code` must open with
  `if (Application.isPlaying) return "still in play mode";`.
- Leaving play mode **unloads the additively loaded `UI.unity`**. Reopen it with
  `EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)` before editing the panel, or the next
  `transform.Find("Parcel Panel")` comes back null and reads as "the panel is gone".
- `manage_scene` action `save` saves the **active** scene, whatever you pass as `path` — with both
  scenes open that is `SampleScene`, and the UI edits stay unsaved while the call reports success.
  Save the other one by name:
  `EditorSceneManager.SaveScene(SceneManager.GetSceneByPath("Assets/Scenes/UI.unity"))`, then check
  the returned bool and `scene.isDirty`.
- `EditorBuildSettings.scenes` does not reach disk on its own. It reads back correctly in memory and
  still ships a broken build. Follow it with `AssetDatabase.SaveAssets()` and
  `ExecuteMenuItem("File/Save Project")`, then confirm in `ProjectSettings/EditorBuildSettings.asset`.
- Play mode barely ticks while the editor window is unfocused, so animations look frozen and timers
  do not advance. Do not read that as a bug — drive it by hand, e.g. `animator.Update(0.1f)` in a loop.
- A Screen Space - Overlay canvas is a **1080x1920 unit** rectangle in the Scene view, next to a 24x12
  island. Select it and press F to find it. It only looks that way in the Scene view — Overlay is
  composited over the camera's output and has no place in the world, so never "fix" it by moving or
  scaling the canvas; Unity recomputes that transform every frame. Hide it instead: the canvas is on
  the `UI` layer, so the Scene view's Layers dropdown switches it off. The screenshot tool cannot
  capture Overlay canvases, so verify UI by assertion, not by picture. The reason, so nobody
  re-tests it: the capture resolves to `Main Camera` even when `camera` is omitted, and
  camera-rendered captures exclude Overlay by design. Checked by moving the panel on-screen with
  its `Image`s confirmed visible — it still did not appear.
- A persistent `UnityEvent` listener defaults to `m_CallState = 2` (`RuntimeOnly`), so
  `button.onClick.Invoke()` in edit mode does **nothing**. That is not a broken wiring. To prove a
  binding without play mode, set that field to `1` through `SerializedObject`, invoke, then put it back.
- `AnimationMode.StartAnimationMode()` plus `SampleAnimationClip` lays a clip's pose onto the real
  objects in edit mode, so poses can be measured (`RectTransform.GetWorldCorners`) without entering
  play mode. Stop animation mode and restore the resting pose afterwards.
- Bash heredocs (`cat > file << 'EOF'`) do work here, despite an earlier note to the contrary.
- **Never force a layout rebuild before saving a scene.** `LayoutRebuilder.ForceRebuildLayoutImmediate`
  or `Canvas.ForceUpdateCanvases()` ahead of `SaveScene` writes **zeroes** over every
  layout-driven `RectTransform` in the file - anchors, `m_AnchoredPosition` and `m_SizeDelta` all
  flatten - while the live objects still read the correct numbers, so it only shows up in
  `git diff`. In `UI.unity` that is 30 lines of silent damage to `Actions`, `Readings` and
  `Machines`. A plain `SaveScene` with no rebuild is safe: the same scene saved untouched comes
  back as a one-line diff. Do not try to help the layout along.
- `execute_code`'s CodeDom compiler does **not** reference every Unity assembly, so a `typeof(...)`
  against a package type can fail to compile even though the package is installed — this cost a
  round trip on `UnityEngine.U2D.SpriteShapeController` back when the parcels used it. When a type
  will not resolve, find it reflectively: loop `AppDomain.CurrentDomain.GetAssemblies()` and
  `asm.GetType("Namespace.Type")`. `UnityEngine.Tilemaps` does resolve normally.
- `capture_source: "scene_view"` **cannot frame an object in an additively loaded scene**. Both
  `UI` and `Parcel Panel` come back as "Target GameObject not found" while `UI.unity` is loaded and
  they plainly exist. Capture the whole viewport instead, or select the object first. That viewport
  shot is live and shows gizmos, selection outlines and the toolbar — the one way to *see* the editor.
- `unity_reflect` answers "does this type really expose that member?" against the live editor —
  `search` for a type, `get_type` for its members, `get_member` for one signature. It is cheaper
  than a failed `execute_code` round trip and it is how you check the engine before writing against
  it. `unity_docs` fetches the manual when reflection is not enough.
- Instantiating a prefab is `manage_gameobject` action `create` with `prefab_path`. `manage_prefabs`
  is a different job — opening and saving the prefab stage — and reaching for it here wastes a turn.
- `batch_execute` takes 25 commands by default (hard cap 100), and `parallel` only ever parallelises
  **read-only** commands: a batch that writes runs sequentially no matter what you pass.
- A `MonoBehaviour` without `[ExecuteAlways]` never runs `Awake`, `OnEnable` or `OnDisable` in edit
  mode — Unity only calls them in Play Mode. Reflection-invoking a private method like
  `OnSelectionChanged` to test it from `execute_code` silently does nothing useful if that method
  reads fields the real `Awake` would have cached, because they are still null/default. Invoke
  `Awake` by reflection first, in the same call, to get a result that means anything.
