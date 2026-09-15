using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class IntroScreen
{
    public Sprite image;               // just drag a PNG/sprite here
    public float displayDuration = 1f; // how long this screen stays up, in seconds
}

public class IntroSceneManager : MonoBehaviour
{
    [Header("Button Zoom Effect")]
    [SerializeField] private RectTransform buttonToZoom;   // the Play button's RectTransform
    [SerializeField] private float zoomScale = 1.3f;       // how big it grows (1 = no change)
    [SerializeField] private float zoomDuration = 0.2f;    // seconds for the zoom to play

    [Header("Intro Screens")]
    [SerializeField] private Image introImage;             // one Image, positioned/sized how you like, layered above the home screen in the Hierarchy
    [SerializeField] private IntroScreen[] introScreens;   // add/remove freely - just drag sprites in, set each duration

    [Header("Home Screen (optional)")]
    [SerializeField] private CanvasGroup homeScreenCanvasGroup; // disables home buttons while the sequence plays

    [Header("Next Scene")]
    [SerializeField] private SceneField gameScene;

    private bool isPlaying;

    private void Awake()
    {
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

        // 1) Zoom the button
        if (buttonToZoom != null)
            yield return StartCoroutine(ZoomButton());

        // 2) Show each sprite over the home screen, one at a time
        if (introImage != null)
        {
            foreach (IntroScreen screen in introScreens)
            {
                if (screen.image == null) continue;

                introImage.sprite = screen.image;
                introImage.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(screen.displayDuration);
                introImage.gameObject.SetActive(false);
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

        if (buttonToZoom != null)
            buttonToZoom.localScale = Vector3.one;

        SetHomeScreenInteractable(true);
        isPlaying = false;
    }

    private void SetHomeScreenInteractable(bool value)
    {
        if (homeScreenCanvasGroup == null) return;

        homeScreenCanvasGroup.interactable = value;
        homeScreenCanvasGroup.blocksRaycasts = value;
    }

    private IEnumerator ZoomButton()
    {
        Vector3 startScale = buttonToZoom.localScale;
        Vector3 targetScale = startScale * zoomScale;

        float t = 0f;
        while (t < zoomDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / zoomDuration);
            buttonToZoom.localScale = Vector3.Lerp(startScale, targetScale, progress);
            yield return null;
        }

        buttonToZoom.localScale = targetScale;
    }
}