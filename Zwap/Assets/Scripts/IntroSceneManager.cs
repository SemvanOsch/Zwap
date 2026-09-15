using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class IntroScreen
{
    public Texture image;              // just drag a texture/PNG here
    public float displayDuration = 1f; // how long this screen stays up, in seconds
}

public class IntroSceneManager : MonoBehaviour
{
    [Header("UI Zoom Effect")]
    [SerializeField] private RectTransform zoomRoot;       // parent that holds ALL home screen content (background, logo, button, etc.)
    [SerializeField] private RectTransform zoomTargetRect; // the point to zoom toward - can be the button itself, or an empty RectTransform placed a bit above it
    [SerializeField] private float targetZoomScale = 2f;   // 2 = zoomed in to 2x, 1 = no zoom
    [SerializeField] private float zoomDuration = 0.5f;    // seconds for the zoom to play

    [Header("Intro Screens")]
    [SerializeField] private RawImage introImage;          // one RawImage, positioned/sized how you like, layered above the home screen in the Hierarchy
    [SerializeField] private IntroScreen[] introScreens;   // add/remove freely - just drag textures in, set each duration

    [Header("Home Screen (optional)")]
    [SerializeField] private CanvasGroup homeScreenCanvasGroup; // disables home buttons while the sequence plays

    [Header("Next Scene")]
    [SerializeField] private SceneField gameScene;

    private bool isPlaying;
    private Vector2 originalPivot;
    private Vector3 originalLocalPosition;
    private Vector3 originalLocalScale;

    private void Awake()
    {
        if (zoomRoot != null)
        {
            originalPivot = zoomRoot.pivot;
            originalLocalPosition = zoomRoot.localPosition;
            originalLocalScale = zoomRoot.localScale;
        }

        ClearAllScreens();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
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
        SetHomeScreenInteractable(false);

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
                yield return new WaitForSecondsRealtime(screen.displayDuration);

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

        if (zoomRoot != null)
        {
            zoomRoot.pivot = originalPivot;
            zoomRoot.localPosition = originalLocalPosition;
            zoomRoot.localScale = originalLocalScale;
        }

        SetHomeScreenInteractable(true);
        isPlaying = false;
    }

    private void SetHomeScreenInteractable(bool value)
    {
        if (homeScreenCanvasGroup == null) return;

        homeScreenCanvasGroup.interactable = value;
        homeScreenCanvasGroup.blocksRaycasts = value;
    }

    private IEnumerator ZoomUI()
    {
        // Move zoomRoot's pivot to line up with the target point, WITHOUT
        // moving anything visually - this makes scaling naturally converge
        // on that point instead of the center of the screen.
        Vector2 pivot = WorldPointToPivot(zoomRoot, zoomTargetRect.position);
        SetPivotPreservingPosition(zoomRoot, pivot);

        Vector3 startScale = zoomRoot.localScale;
        Vector3 endScale = Vector3.one * targetZoomScale;

        float t = 0f;
        while (t < zoomDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / zoomDuration);
            zoomRoot.localScale = Vector3.Lerp(startScale, endScale, progress);
            yield return null;
        }

        zoomRoot.localScale = endScale;
    }

    // Converts a world-space point into a 0-1 pivot fraction within rt's own rect.
    private Vector2 WorldPointToPivot(RectTransform rt, Vector3 worldPoint)
    {
        Vector3 localPoint = rt.InverseTransformPoint(worldPoint);
        Rect rect = rt.rect;

        float px = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float py = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        return new Vector2(px, py);
    }

    // Changes a RectTransform's pivot without visually moving it -
    // standard Unity trick since changing pivot alone shifts anchoredPosition.
    private void SetPivotPreservingPosition(RectTransform rt, Vector2 newPivot)
    {
        Vector2 size = rt.rect.size;
        Vector2 deltaPivot = rt.pivot - newPivot;

        Vector3 deltaPosition = new Vector3(
            deltaPivot.x * size.x * rt.localScale.x,
            deltaPivot.y * size.y * rt.localScale.y,
            0f);

        rt.pivot = newPivot;
        rt.localPosition -= deltaPosition;
    }
}