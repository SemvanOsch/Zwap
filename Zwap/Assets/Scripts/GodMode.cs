using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GodMode : MonoBehaviour
{
    // Static, so PlayerStart can read it without any reference to the pause menu.
    public static bool Enabled { get; private set; }

    [Tooltip("Optional. If assigned, the toggle shows the current state whenever the pause menu opens.")]
    [SerializeField] private Toggle uiToggle;

    // Runs once per play session / app launch: start false and subscribe to scene loads.
    // This works even if the pause menu object is inactive, and with domain reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        Enabled = false;
        SceneManager.sceneLoaded -= OnSceneLoaded; // avoid double-subscribing
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Every (non-additive) scene load or reload resets god mode to off.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            Enabled = false;
    }

    private void OnEnable()
    {
        // Sync the checkbox with the real state without firing onValueChanged
        if (uiToggle != null)
            uiToggle.SetIsOnWithoutNotify(Enabled);
    }

    // Hook this to a UI Toggle's OnValueChanged (choose the "Dynamic bool" version at the top of the list).
    public void SetGodMode(bool value)
    {
        Enabled = value;
    }

    // Or hook this to a plain Button's OnClick.
    public void ToggleGodMode()
    {
        Enabled = !Enabled;
        if (uiToggle != null)
            uiToggle.SetIsOnWithoutNotify(Enabled);
    }
}