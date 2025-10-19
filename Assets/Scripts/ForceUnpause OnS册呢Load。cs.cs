// ForceUnpauseOnSceneLoad.cs
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor; // editor-only pause control
#endif

/// <summary>
/// Ensures the editor is not paused and timeScale is 1
/// every time a scene loads (Play mode).
/// Does not need to be on any GameObject.
/// </summary>
public static class ForceUnpauseOnSceneLoad
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Unpause()
    {
        // Make sure the game's clock is running (in case it was set to 0)
        Time.timeScale = 1f;

        #if UNITY_EDITOR
        // If the Unity Editor is paused, unpause it
        if (EditorApplication.isPaused)
            EditorApplication.isPaused = false;
        #endif
    }
}
