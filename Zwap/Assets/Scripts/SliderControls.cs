using UnityEngine;
using UnityEngine.UI;

// Reads the two on-screen sliders used by Slider control mode. The bottom
// slider drives the X position, the right slider drives the Y position. Both
// report 0..1; PlayerStart maps those onto the play area and glides the fish
// toward the resulting point (see PlayerStart.GetSliderTarget).
//
// Put this on the slider panel GameObject that ControlSwitcher toggles for
// ControlType.Slider, and wire both Slider references in the Inspector.
public class SliderControls : MonoBehaviour
{
    [Tooltip("Horizontal slider along the bottom (Direction: Left To Right). Drives X.")]
    [SerializeField] private Slider bottomSlider;

    [Tooltip("Vertical slider up the right side (Direction: Bottom To Top). Drives Y.")]
    [SerializeField] private Slider rightSlider;

    // Current handle positions as (x, y) in 0..1. Falls back to centre for a
    // missing slider so a half-wired panel parks the fish in the middle rather
    // than slamming it into a corner.
    public Vector2 Get01()
    {
        float x = bottomSlider != null ? bottomSlider.value : 0.5f;
        float y = rightSlider  != null ? rightSlider.value  : 0.5f;
        return new Vector2(x, y);
    }

    // Moves the handles to match a point the fish is already at, WITHOUT firing
    // onValueChanged. Called when Slider mode becomes active so the handles line
    // up with the fish instead of teleporting it to wherever they happened to be.
    public void SetFromNormalized(Vector2 v)
    {
        if (bottomSlider != null) bottomSlider.SetValueWithoutNotify(Mathf.Clamp01(v.x));
        if (rightSlider  != null) rightSlider.SetValueWithoutNotify(Mathf.Clamp01(v.y));
    }
}
