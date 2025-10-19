using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Ensures we are not paused after a scene load, resets timeScale to 1,
/// and enables the usual XRI action maps so movement & UI work immediately.
/// </summary>
public class SceneBootstrap : MonoBehaviour
{
    [Header("Optional: your Input Actions (XRI Default Input Actions)")]
    public InputActionAsset actions;

    [Tooltip("These maps usually cover HMD, hands, and locomotion.")]
    public string[] mapsToEnable = new[]
    {
        "XRI Head",
        "XRI LeftHand",
        "XRI RightHand",
        "XRI LeftHand Locomotion",
        "XRI RightHand Locomotion",
        "UI"
    };

    void Awake()
    {
        // Make sure the game is actually running forward
        Time.timeScale = 1f;

        #if UNITY_EDITOR
        // If the editor is paused for any reason, unpause it on scene load
        if (EditorApplication.isPaused)
            EditorApplication.isPaused = false;
        #endif

        // Re-enable action maps (in case previous scene or gating disabled them)
        if (actions != null)
        {
            foreach (var name in mapsToEnable)
            {
                var map = actions.FindActionMap(name, throwIfNotFound: false);
                if (map != null && !map.enabled)
                    map.Enable();
            }
        }
    }
}
