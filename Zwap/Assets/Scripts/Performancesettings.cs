using UnityEngine;

// No need to add this to a scene: it runs by itself once, before the first scene loads.
// Keeps frame pacing consistent across phones and removes some silent per-frame costs.
public static class PerformanceSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // Let targetFrameRate decide the pace instead of the phone's own vsync/refresh rate.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // A single long frame (first spawn, scene load, ...) can no longer make
        // Time.deltaTime jump by up to a third of a second, or queue a pile of
        // FixedUpdate/physics steps afterwards, which is what makes slow phones stutter.
        Time.maximumDeltaTime = 0.1f;

        // Debug.Log is slow on Android (it captures a stack trace). Keep it in the
        // editor and in Development Builds only.
        Debug.unityLogger.logEnabled = Debug.isDebugBuild || Application.isEditor;
    }
}