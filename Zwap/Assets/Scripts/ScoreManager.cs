using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Scoring")]
    [SerializeField] private float secondsPerPoint = 1f; // time between +1

    private int score;
    private float timer;

    void Start()
    {
        score = 0;
        UpdateText();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= secondsPerPoint)
        {
            // handles multiple points if a frame takes longer than the interval
            int points = Mathf.FloorToInt(timer / secondsPerPoint);
            score += points;
            timer -= points * secondsPerPoint;
            UpdateText();
        }
    }

    // Call this from other scripts if you ever want to award bonus points.
    public void AddScore(int amount)
    {
        score += amount;
        UpdateText();
    }

    public int GetScore() => score;

    private void UpdateText()
    {
        if (scoreText != null)
            scoreText.text = score.ToString();
            GameData.Instance.score = score; 
    }
}
