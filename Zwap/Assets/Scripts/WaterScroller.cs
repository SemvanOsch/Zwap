using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scrolls a UI RawImage texture by moving uvRect.y only.
/// W and H stay at 1 at all times — this prevents any stretching.
///
/// SETUP:
///   1. Attach this script to your WaterBackground GameObject.
///   2. Make sure the Raw Image's UV Rect is set to X=0, Y=0, W=1, H=1.
///   3. The texture MUST have Wrap Mode set to "Repeat" in its Import Settings.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class WaterScroller : MonoBehaviour
{
    [Header("Scroll Speed")]
    [Tooltip("How fast the texture scrolls vertically. Positive = upward scroll (cave moving up effect).")]
    public float scrollSpeedY = 0.1f;

    [Tooltip("Horizontal scroll speed. Leave at 0 for a straight vertical scroll.")]
    public float scrollSpeedX = 0f;

    private RawImage rawImage;
    private float offsetX;
    private float offsetY;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();

        // Always reset to a clean UV rect — W=1, H=1 prevents stretching.
        rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        offsetX = 0f;
        offsetY = 0f;
    }

    void Update()
    {
        offsetX += scrollSpeedX * Time.deltaTime;
        offsetY += scrollSpeedY * Time.deltaTime;

        // Wrap offsets so the float never grows unboundedly (avoids precision loss over long sessions).
        offsetX %= 1f;
        offsetY %= 1f;

        // Only Y changes — W and H are always 1, so the texture never stretches.
        rawImage.uvRect = new Rect(offsetX, offsetY, 1f, 1f);
    }

#if UNITY_EDITOR
    // Resets UV rect when values are changed in the Inspector during Play mode.
    void OnValidate()
    {
        if (rawImage != null)
            rawImage.uvRect = new Rect(offsetX, offsetY, 1f, 1f);
    }
#endif
}
