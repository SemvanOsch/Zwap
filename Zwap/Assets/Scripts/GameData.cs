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

        Highscore = SaveManager.Instance.Data.highScore;
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
