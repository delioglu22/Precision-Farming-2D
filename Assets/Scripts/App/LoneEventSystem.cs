using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Stands this scene's EventSystem down when the game already has one.
///
/// The mini game carries its own so that its scene can be opened and played on its own. Laid
/// over the game it would be the second in the session, and two of them fight over every
/// click - Unity says as much in the console and disables one of them.
///
/// EventSystem.current is whichever one woke first, so a scene loaded in later already finds
/// it pointing at the one that was up before.
/// </summary>
[RequireComponent(typeof(EventSystem))]
[DisallowMultipleComponent]
public class LoneEventSystem : MonoBehaviour
{
    void Awake()
    {
        if (EventSystem.current == null || EventSystem.current == GetComponent<EventSystem>()) return;

        // Stood down before it is destroyed: Destroy waits until the end of the frame, and in
        // the meantime Unity would still run this EventSystem's OnEnable and complain about
        // there being a second one. Deactivating first means that message never happens.
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
