using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lets a scene that was laid over the game take itself away again.
///
/// It closes the scene it is standing in rather than one named in the Inspector, which is
/// what lets the mini game's own Close button reach it: nothing can serialise a reference
/// across a scene boundary, but a component always knows which scene it belongs to.
/// </summary>
[DisallowMultipleComponent]
public class DismissScene : MonoBehaviour
{
    /// <summary>One step back to whatever was underneath. A Button calls this directly.</summary>
    public void Dismiss()
    {
        Scene mine = gameObject.scene;
        if (mine.IsValid() && mine.isLoaded) SceneManager.UnloadSceneAsync(mine);
    }
}
