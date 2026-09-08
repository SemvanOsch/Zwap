using UnityEngine;
using TMPro; // if using TextMeshPro (recommended/default in modern Unity)

public class ScoreDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text HighscoreText;
    
    private void Start()
    {
        if (GameData.Instance == null)
        {
            Debug.LogWarning("GameData.Instance is null — did the game start from the correct first scene?");
            return;
        }

        scoreText.text = "Score: " + GameData.Instance.score;
        HighscoreText.text = "Highscore: " + GameData.Instance.Highscore;
    }
}

 