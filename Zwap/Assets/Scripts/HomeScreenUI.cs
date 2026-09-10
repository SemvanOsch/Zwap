using UnityEngine;
using TMPro;

public class HomeScreenUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI highScoreText;

    private void Start()
    {
        if (SaveManager.Instance != null && highScoreText != null)
        {
            highScoreText.text = SaveManager.Instance.Data.highScore + " M";
        }
    }
}