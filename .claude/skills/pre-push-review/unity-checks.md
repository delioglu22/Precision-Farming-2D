# The two checks that need the editor

Both run through `mcp__UnityMCP__execute_code` with `compiler: "codedom"` (C# 6 — no
interpolated strings, no `?.`, no local functions). Both are read-only: they report and
change nothing. Neither needs play mode; if the editor is in play mode, stop first,
and reopen `UI.unity` additively before running check 1, or its half comes back empty.

## 1. Empty `[SerializeField]` references

This project wires almost everything in the Inspector, so a field left empty is the most
likely way for something to silently do nothing. Only `Assembly-CSharp` components are
scanned: a null on a built-in component is usually deliberate (a flat `Image` really does
want no sprite), a null on one of ours usually is not.

```csharp
if (Application.isPlaying) return "still in play mode";
var sb = new System.Text.StringBuilder();
int checkedComps = 0, nulls = 0;
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
        sb.Append("  ").Append(where).Append("/").Append(go.name).Append(" -> ").Append(t.Name).Append(".").Append(it.name).Append("\n");
        nulls++;
      }
    }
  }
  for (int i = 0; i < go.transform.childCount; i++) scan(go.transform.GetChild(i).gameObject, where);
};
foreach (var path in new string[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/UI.unity" }) {
  var sc = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
  if (!sc.isLoaded) { sb.Append(path).Append(" NOT LOADED - skipped\n"); continue; }
  foreach (var root in sc.GetRootGameObjects()) scan(root, sc.name);
}
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab")) {
  var p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
  var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(p);
  if (go != null) scan(go, "prefab:" + System.IO.Path.GetFileNameWithoutExtension(p));
}
return "scanned " + checkedComps + " project components, " + nulls + " empty reference(s)\n" + sb.ToString();
```

Expected baseline: **30 empty references, all of them correct** — see the known
false positives in `SKILL.md`. A number other than 30 is what deserves a look.

## 2. Every pose of an animator keys the same properties

The project rule from `animation-notes`: a property only one clip mentions is undefined
while blending into that clip, so the panel jumps instead of moving. This walks every
`AnimatorController` in the project and names the odd one out.

```csharp
var sb = new System.Text.StringBuilder();
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AnimatorController")) {
  var p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
  var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(p);
  if (ctrl == null) continue;
  sb.Append(System.IO.Path.GetFileName(p)).Append(":\n");
  var all = new System.Collections.Generic.Dictionary<string, int>();
  var clips = new System.Collections.Generic.List<AnimationClip>();
  foreach (var layer in ctrl.layers)
    foreach (var st in layer.stateMachine.states) {
      var cl = st.state.motion as AnimationClip;
      if (cl != null && !clips.Contains(cl)) clips.Add(cl);
    }
  foreach (var cl in clips)
    foreach (var b in UnityEditor.AnimationUtility.GetCurveBindings(cl)) {
      string key = b.path + "|" + b.propertyName;
      if (!all.ContainsKey(key)) all[key] = 0;
      all[key] = all[key] + 1;
    }
  foreach (var cl in clips) sb.Append("   ").Append(cl.name).Append(" curves=").Append(UnityEditor.AnimationUtility.GetCurveBindings(cl).Length.ToString()).Append("\n");
  int bad = 0;
  foreach (var kv in all)
    if (kv.Value != clips.Count) { sb.Append("   ODD ONE OUT: ").Append(kv.Key).Append(" in ").Append(kv.Value.ToString()).Append("/").Append(clips.Count.ToString()).Append(" clips\n"); bad++; }
  if (bad == 0) sb.Append("   OK - all ").Append(clips.Count.ToString()).Append(" poses key the same properties\n");
}
return sb.ToString();
```

Expected baseline: `ParcelPanel` 3/3, all OK. `Parcel` no longer has a controller -
the tilemap `Parcel` drives its lift and highlight directly, not through an Animator.
`Minigame` was already gone before this note was last checked; if a controller by
that name turns up again, it is new, not a regression.
