using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Brings the UI scene in alongside the map. The UI lives in its own scene so
/// the map scene holds nothing but the world - a Screen Space canvas is a
/// 1080x1920 unit rectangle in the Scene view, and next to a 24x12 island it
/// swamps everything.
/// </summary>
[DisallowMultipleComponent]
public class SceneBootstrap : MonoBehaviour
{
    [Tooltip("Loaded additively on start. Must be listed in the build settings.")]
    [SerializeField] string uiScene = "UI";

    // Start, not Awake: while editing, the UI scene is often already open beside
    // this one, and during Awake it is not in the scene list yet. Loading it a
    // second time gives two EventSystems and two panels.
    void Start()
    {
        if (string.IsNullOrEmpty(uiScene)) return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).name == uiScene) return;
        }

        SceneManager.LoadScene(uiScene, LoadSceneMode.Additive);
    }
}
