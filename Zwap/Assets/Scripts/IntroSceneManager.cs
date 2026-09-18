using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class IntroScreen
{
    public Texture[] images;           // drag one or more textures/PNGs here - they play one after another, evenly split across displayDuration
    public float displayDuration = 1f; // TOTAL time this screen stays up (shared across all its images), in seconds
    [TextArea]
    public string captionText;         // optional - leave empty for no text on this screen. Shown for the whole screen, across all its images.

    [Header("Optional extras")]
    public bool showFishOverlay;           // if true, the player's currently selected skin is drawn on top for this screen's whole duration
    public float lastFrameExtraDuration;  // optional - extra seconds added on top of this screen's last frame only. Leave at 0 for even spacing.
    public Vector2 overlayStartPos;       // anchored position (on overlayImage's RectTransform) where the overlay starts this screen
    public Vector2 overlayEndPos;         // anchored position it has drifted to by the end of this screen. Leave both the same for no movement.
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
    [SerializeField] private IntroScreen[] introScreens;   // add/remove freely - each screen can hold several sprites shown evenly across its displayDuration
    [SerializeField] private Image overlayImage;           // optional decorative layer (the selected skin/fish) - place it above introImage in the Hierarchy

    [Header("Fish / Skin")]
    [SerializeField] private SkinPreview skinPreview;      // drag in your existing SkinPreview component (any hierarchy depth, active or not - a direct reference works either way)

    [Header("Skip")]
    [SerializeField] private Button skipButton;            // optional - an invisible full-screen button over introImage; tapping it skips straight to the next screen

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

        Sprite selectedSkinSprite = GetSelectedSkinSprite();

        // 1) Zoom the whole home screen UI in toward the target point
        if (zoomRoot != null && zoomTargetRect != null)
            yield return StartCoroutine(ZoomUI());

        // 2) Play each screen's sprites, evenly spaced within that screen's window
        if (introImage != null)
        {
            for (int i = 0; i < introScreens.Length; i++)
            {
                IntroScreen screen = introScreens[i];
                if (screen.images == null || screen.images.Length == 0)
                    continue;

                if (introCaptionText != null)
                {
                    bool hasCaption = !string.IsNullOrEmpty(screen.captionText);
                    introCaptionText.text = screen.captionText;
                    introCaptionText.gameObject.SetActive(hasCaption);
                }

                // Optional decorative overlay - the player's selected skin - that sits on top for this whole screen
                bool hasOverlay = overlayImage != null && screen.showFishOverlay && selectedSkinSprite != null;
                if (overlayImage != null)
                {
                    if (hasOverlay)
                    {
                        overlayImage.sprite = selectedSkinSprite;
                        overlayImage.rectTransform.anchoredPosition = screen.overlayStartPos;
                        overlayImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        overlayImage.gameObject.SetActive(false);
                    }
                }

                bool isLastScreen = i == introScreens.Length - 1;
                float perImageDuration = screen.displayDuration / screen.images.Length;
                float totalScreenDuration = screen.displayDuration + screen.lastFrameExtraDuration;
                float elapsedBeforeFrame = 0f;

                for (int j = 0; j < screen.images.Length; j++)
                {
                    Texture image = screen.images[j];
                    if (image == null) continue;

                    introImage.texture = image;
                    introImage.gameObject.SetActive(true);

                    bool isLastImage = j == screen.images.Length - 1;
                    float thisFrameDuration = perImageDuration + (isLastImage ? screen.lastFrameExtraDuration : 0f);

                    // Work out how far the overlay should have drifted by the start and
                    // end of THIS frame, as a fraction of the screen's total duration -
                    // this keeps its motion one smooth, steady drift across every frame
                    // in the screen (including the held-longer last one), not a jump per frame.
                    Vector2 fromPos = screen.overlayStartPos;
                    Vector2 toPos = screen.overlayStartPos;
                    if (hasOverlay && totalScreenDuration > 0f)
                    {
                        float fromProgress = elapsedBeforeFrame / totalScreenDuration;
                        float toProgress = (elapsedBeforeFrame + thisFrameDuration) / totalScreenDuration;
                        fromPos = Vector2.Lerp(screen.overlayStartPos, screen.overlayEndPos, fromProgress);
                        toPos = Vector2.Lerp(screen.overlayStartPos, screen.overlayEndPos, toProgress);
                    }

                    yield return StartCoroutine(WaitOrSkip(thisFrameDuration, hasOverlay ? overlayImage : null, fromPos, toPos));

                    elapsedBeforeFrame += thisFrameDuration;

                    if (skipRequested)
                    {
                        // Skip jumps straight to the next screen, same as before -
                        // it doesn't just advance to the next sprite within this screen.
                        skipRequested = false;
                        break;
                    }

                    if (!(isLastScreen && isLastImage))
                        introImage.gameObject.SetActive(false);
                    // if it's the very last image of the very last screen, leave it
                    // active - it'll stay on top until the new scene finishes loading,
                    // avoiding a flash of the home screen underneath.
                }
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

        if (overlayImage != null)
            overlayImage.gameObject.SetActive(false);

        if (zoomRoot != null)
        {
            zoomRoot.localPosition = originalLocalPosition;
            zoomRoot.localScale = originalLocalScale;
        }

        SetObjectsActive(objectsToHideDuringZoom, true);
        isPlaying = false;
    }

    // Simply defers to the existing SkinPreview component, so there's a single
    // source of truth for "which skin is selected" rather than two copies of
    // the skin list and PlayerPrefs key.
    private Sprite GetSelectedSkinSprite()
    {
        return skinPreview != null ? skinPreview.GetSelectedSkinSprite() : null;
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

    private IEnumerator WaitOrSkip(float duration, Image overlay, Vector2 overlayFromPos, Vector2 overlayToPos)
    {
        if (overlay != null)
            overlay.rectTransform.anchoredPosition = overlayFromPos;

        float t = 0f;
        while (t < duration && !skipRequested)
        {
            t += Time.unscaledDeltaTime;

            if (overlay != null)
            {
                float progress = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
                overlay.rectTransform.anchoredPosition = Vector2.Lerp(overlayFromPos, overlayToPos, progress);
            }

            yield return null;
        }

        if (overlay != null)
            overlay.rectTransform.anchoredPosition = overlayToPos;
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