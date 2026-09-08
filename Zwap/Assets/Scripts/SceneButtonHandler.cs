using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneButtonHandler : MonoBehaviour
{
    [SerializeField] private SceneField sceneToLoad;

    public void GoToScene()
    {
        SceneManager.LoadScene(sceneToLoad);
    }
}