using System;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundScroller : MonoBehaviour
{
    public RawImage img;
    public float x;
    public float y;

    [SerializeField] private float speedPerScore = 0.002f;
    
    private void Update()
    {
        float multiplier = 1f;
        if (ScoreManager.Instance != null)
            multiplier = 1f + speedPerScore * ScoreManager.Instance.GetScore();

        Vector2 scroll = new Vector2(x, y) * multiplier * Time.deltaTime;
        img.uvRect = new Rect(img.uvRect.position + scroll, img.uvRect.size);
    }
}