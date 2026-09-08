using UnityEngine;

public class GameData : MonoBehaviour
{
    public static GameData Instance;

    public int score;
    public int Highscore;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
