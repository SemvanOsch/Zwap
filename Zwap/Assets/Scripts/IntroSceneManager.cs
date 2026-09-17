using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class IntroScreen
{
    public Texture image;              // just drag a texture/PNG here
    public float displayDuration = 1f; // how long this screen stays up, in seconds
    [TextArea]
    public string captionText;         // optional - leave empty for no text on this screen
}

public class IntroSceneManager : MonoBehaviour
{
    [Header("UI Zoom Effect")]
    [SerializeField] private RectTransform zoomRoot;       // parent that holds ALL home screen content (background, logo, button, etc.)
    [SerializeField] private RectTransform zoomTargetRect; // draw this box around the area you want to end up filling the screen
    [SerializeField] private float extraZoomMultiplier = 1f; // 1 = box exactly fills the screen at the end, >1 = zoom in a bit further
    [SerializeField] private float zoomDuration = 0.5f;    // seconds for the zoom to play

    [Header("Intro Screens")]
    [SerializeField] private RawImage introImage;          // one RawImage, positioned/sized how you like, layered above the home screen in the Hierarchy
    [SerializeField] private TMP_Text introCaptionText;    // optional - a text element layered over introImage, for screens that have captionText set
    [SerializeField] private IntroScreen[] introScreens;   // add/remove freely - just drag textures in, set each duration

    [Header("Skip")]
    [SerializeField] private Button skipButton;            // optional - an invisible full-screen button over introImage; tapping it skips to the next screen instantly

    [Header("Home Screen (optional)")]
    [SerializeField] private GameObject[] objectsToHideDuringZoom; // e.g. Play button, highscore board

    [Header("Next Scene")]
    [SerializeField] private SceneField gameScene;

    private bool isPlaying;
    private bool skipRequested;
    private Vector3 originalLocalPosition;
    private Vector3 originalLocalScale;

    private void Awake()
    {
        if (zoomRoot != null)
        {
            originalLocalPosition = zoomRoot.localPosition;
            originalLocalScale = zoomRoot.localScale;
        }

        ClearAllScreens();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (skipButton != null)
            skipButton.onClick.AddListener(SkipCurrentScreen);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (skipButton != null)
            skipButton.onClick.RemoveListener(SkipCurrentScreen);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Runs every time ANY scene finishes loading - guarantees a clean
        // state whenever Home-Screen comes back, regardless of how it was loaded.
        ClearAllScreens();
    }

    // Hook this up to the Play button's OnClick() in the Inspector.
    public void PlayPressed()
    {
        if (isPlaying) return; // ignore extra clicks while the sequence is running
        isPlaying = true;

        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        SetObjectsActive(objectsToHideDuringZoom, false);

        // 1) Zoom the whole home screen UI in toward the target point
        if (zoomRoot != null && zoomTargetRect != null)
            yield return StartCoroutine(ZoomUI());

        // 2) Show each texture over the home screen, one at a time
        if (introImage != null)
        {
            for (int i = 0; i < introScreens.Length; i++)
            {
                IntroScreen screen = introScreens[i];
                if (screen.image == null) continue;

                introImage.texture = screen.image;
                introImage.gameObject.SetActive(true);

                if (introCaptionText != null)
                {
                    bool hasCaption = !string.IsNullOrEmpty(screen.captionText);
                    introCaptionText.text = screen.captionText;
                    introCaptionText.gameObject.SetActive(hasCaption);
                }

                yield return StartCoroutine(WaitOrSkip(screen.displayDuration));

                bool isLastScreen = i == introScreens.Length - 1;
                if (!isLastScreen)
                    introImage.gameObject.SetActive(false);
                // if it's the last screen, leave it active - it'll stay on
                // top until the new scene finishes loading, avoiding a
                // flash of the home screen underneath.
            }
        }

        // 3) Load the game scene
        SceneManager.LoadScene(gameScene);
    }

    // Call this any time you want to guarantee nothing is left showing.
    public void ClearAllScreens()
    {
        if (introImage != null)
            introImage.gameObject.SetActive(false);

        if (introCaptionText != null)
            introCaptionText.gameObject.SetActive(false);

        if (zoomRoot != null)
        {
            zoomRoot.localPosition = originalLocalPosition;
            zoomRoot.localScale = originalLocalScale;
        }

        SetObjectsActive(objectsToHideDuringZoom, true);
        isPlaying = false;
    }

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;

        foreach (GameObject obj in objects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }

    // Hook this up to an invisible full-screen button's OnClick() so tapping
    // during a slide skips its remaining wait time immediately.
    public void SkipCurrentScreen()
    {
        skipRequested = true;
    }

    private IEnumerator WaitOrSkip(float duration)
    {
        skipRequested = false;

        float t = 0f;
        while (t < duration && !skipRequested)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        skipRequested = false;
    }

    private IEnumerator ZoomUI()
    {
        // Capture the target box's position in zoomRoot's own local space,
        // while zoomRoot is still at its original scale/position.
        Vector3 targetLocal = zoomRoot.InverseTransformPoint(zoomTargetRect.position);

        Rect rootRect = zoomRoot.rect;
        Vector2 targetSize = zoomTargetRect.rect.size;

        // Uniform scale (not separate X/Y) so artwork never stretches.
        // Uses the larger of the two ratios so the box fully covers the
        // screen even if its aspect ratio doesn't exactly match.
        float scaleFactor = Mathf.Max(rootRect.width / targetSize.x, rootRect.height / targetSize.y)
                             * extraZoomMultiplier;

        Vector3 startScale = originalLocalScale;
        Vector3 endScale = originalLocalScale * scaleFactor;

        Vector3 startPos = originalLocalPosition;
        // Solve for the position that puts the target's point back at the
        // original center once scaled up, so the box ends up filling the screen.
        Vector3 endPos = originalLocalPosition - Vector3.Scale(targetLocal, new Vector3(scaleFactor, scaleFactor, 0f));

        float t = 0f;
        while (t < zoomDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / zoomDuration);

            zoomRoot.localScale = Vector3.Lerp(startScale, endScale, progress);
            zoomRoot.localPosition = Vector3.Lerp(startPos, endPos, progress);

            yield return null;
        }

        zoomRoot.localScale = endScale;
        zoomRoot.localPosition = endPos;
    }
}