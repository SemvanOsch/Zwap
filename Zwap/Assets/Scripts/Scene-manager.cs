using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneButtonHandler : MonoBehaviour
{
    [SerializeField] private SceneField sceneToLoad;

    public void GoToScene()
    {
        // Make sure time/audio aren't left frozen from a paused state
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(sceneToLoad);
    }
}