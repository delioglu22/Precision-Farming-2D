# The checks that need the editor

Use these for Unity changes or an explicitly requested full Unity audit, not for
documentation-only changes. Run through `mcp__UnityMCP__execute_code` with
`compiler: "codedom"`; keep the snippets compatible with the project's editor runner.
They report findings without saving assets or changing build settings. Stop play mode
before running them; do not stop it automatically without checking the current task.

The reference scan reads already-loaded scenes in their current editor state and opens
other enabled build scenes as temporary preview scenes, closing each in `finally`.
It does not leave Seeder or Fertilizer loaded over the map. Preview-scene loading can
run editor callbacks; inspect the console afterwards. Report any failed or skipped
scene as incomplete verification. Do not save incidental changes from this review.

## 1. Empty `[SerializeField]` references

This project wires almost everything in the Inspector, so a field left empty is the most
likely way for something to silently do nothing. Only `Assembly-CSharp` components are
scanned: a null on a built-in component is usually deliberate (a flat `Image` really does
want no sprite), a null on one of ours usually is not.

```csharp
if (Application.isPlaying) return "still in play mode";
var sb = new System.Text.StringBuilder();
int checkedComps = 0, nulls = 0, failedScenes = 0;
System.Action<GameObject,string> scan = null;
scan = delegate(GameObject go, string where) {
  foreach (var mb in go.GetComponents<MonoBehaviour>()) {
    if (mb == null) { sb.Append("  MISSING SCRIPT on ").Append(where).Append("/").Append(go.name).Append("\n"); nulls++; continue; }
    var t = mb.GetType();
    if (t.Assembly.GetName().Name != "Assembly-CSharp") continue;
    checkedComps++;
    var so = new UnityEditor.SerializedObject(mb);
    var it = so.GetIterator();
    while (it.NextVisible(true)) {
      if (it.propertyType != UnityEditor.SerializedPropertyType.ObjectReference) continue;
      if (it.objectReferenceValue == null) {
        string kind = it.objectReferenceInstanceIDValue == 0 ? "EMPTY" : "MISSING";
        sb.Append("  ").Append(kind).Append(" ").Append(where).Append("/").Append(go.name).Append(" -> ").Append(t.Name).Append(".").Append(it.propertyPath).Append("\n");
        nulls++;
      }
    }
  }
  for (int i = 0; i < go.transform.childCount; i++) scan(go.transform.GetChild(i).gameObject, where + "/" + go.name);
};
var seen = new System.Collections.Generic.HashSet<string>();
foreach (var entry in UnityEditor.EditorBuildSettings.scenes) {
  if (!entry.enabled || !seen.Add(entry.path)) continue;
  string path = entry.path;
  var sc = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
  bool openedPreview = false;
  try {
    if (!sc.IsValid() || !sc.isLoaded) {
      sc = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(path);
      openedPreview = true;
    }
    if (!sc.IsValid() || !sc.isLoaded) throw new System.Exception("scene did not load");
    sb.Append("SCENE ").Append(path).Append(sc.isDirty ? " (unsaved editor state)" : "").Append("\n");
    foreach (var root in sc.GetRootGameObjects()) scan(root, path);
  } catch (System.Exception ex) {
    failedScenes++;
    sb.Append("UNVERIFIED ").Append(path).Append(": ").Append(ex.Message).Append("\n");
  } finally {
    if (openedPreview && sc.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(sc);
  }
}
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets" })) {
  var p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
  var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(p);
  if (go != null) scan(go, "prefab:" + p);
}
return "scanned " + seen.Count + " enabled build scenes, " + checkedComps + " project components, " + nulls + " empty/missing reference(s), " + failedScenes + " unverified scene(s)\n" + sb.ToString();
```

Classify each reported field against its intended use and the known optional references
in `SKILL.md`. A missing serialized target is different from an intentionally empty
field. There is no fixed correct total: adding a scene, button or fallow field changes it.
If no enabled build scenes were found, the scene audit is incomplete.

## 2. The parcel panel's blended poses key the same properties

The parcel panel relies on explicit closed/open/expanded poses; compare their curve
bindings for omissions. This is a check of that panel contract, not a requirement that
unrelated machines, drones or animation layers animate identical properties. If the
panel moves to nested state machines or blend trees, extend the inspection before
claiming full coverage; this snippet reports those structures as unverified.

```csharp
var sb = new System.Text.StringBuilder();
var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Animation/ParcelPanel.controller");
if (ctrl == null) return "UNVERIFIED: ParcelPanel.controller not found";
foreach (var layer in ctrl.layers) {
  sb.Append(layer.name).Append(":\n");
  bool incomplete = layer.stateMachine.stateMachines.Length > 0;
  var all = new System.Collections.Generic.Dictionary<string, int>();
  var clips = new System.Collections.Generic.List<AnimationClip>();
  foreach (var st in layer.stateMachine.states) {
    var cl = st.state.motion as AnimationClip;
    if (cl != null && !clips.Contains(cl)) clips.Add(cl);
    if (cl == null) incomplete = true;
  }
  foreach (var cl in clips) {
    var bindings = new System.Collections.Generic.List<UnityEditor.EditorCurveBinding>();
    bindings.AddRange(UnityEditor.AnimationUtility.GetCurveBindings(cl));
    bindings.AddRange(UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(cl));
    foreach (var b in bindings) {
      string key = b.path + "|" + b.type.FullName + "|" + b.propertyName;
      if (!all.ContainsKey(key)) all[key] = 0;
      all[key] = all[key] + 1;
    }
  }
  foreach (var cl in clips) sb.Append("   ").Append(cl.name).Append("\n");
  int bad = 0;
  foreach (var kv in all)
    if (kv.Value != clips.Count) { sb.Append("   ODD ONE OUT: ").Append(kv.Key).Append(" in ").Append(kv.Value.ToString()).Append("/").Append(clips.Count.ToString()).Append(" clips\n"); bad++; }
  if (incomplete || clips.Count == 0) sb.Append("   UNVERIFIED: nested states, blend trees or missing pose clips require inspection\n");
  else if (bad == 0) sb.Append("   OK - all ").Append(clips.Count.ToString()).Append(" inspected poses key the same properties\n");
}
return sb.ToString();
```

Inspect any mismatch before changing a clip. A matching binding set does not verify
the values, transitions or first-frame appearance; verify the panel behavior in Unity.
