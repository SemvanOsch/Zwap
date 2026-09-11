using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Scoring")]
    [SerializeField] private float secondsPerPoint = 1f; // base time between +1

    [Header("Acceleration")]
    [SerializeField] private float scoreRatePerScore = 0.002f; // +0.2% scoring rate per point
    [SerializeField] private float maxMultiplier = 5f;         // rate caps at 5x the base

    private int score;
    private float timer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        score = 0;
        UpdateText();
    }

    void Update()
    {
        timer += Time.deltaTime;

        float multiplier = Mathf.Min(1f + scoreRatePerScore * score, maxMultiplier);
        float effectiveInterval = secondsPerPoint / multiplier;

        if (timer >= effectiveInterval)
        {
            // handles multiple points if a frame takes longer than the interval
            int points = Mathf.FloorToInt(timer / effectiveInterval);
            score += points;
            timer -= points * effectiveInterval;
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
    }
}