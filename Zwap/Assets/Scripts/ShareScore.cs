using System.Collections;
using System.IO;
using UnityEngine;

// Hook one of the public methods below to the "Share Score" button's
// OnClick () in the Game-over scene.
//
// Requires the free "Native Share" plugin by yasirkula:
//   Window > Package Manager > + > Add package from git URL
//   https://github.com/yasirkula/UnityNativeShare.git
//
// The native share sheet only appears on a real iOS/Android build,
// not in the Unity Editor.
public class ShareScore : MonoBehaviour
{
    [Header("Message")]
    // {0} is replaced with the player's score.
    [SerializeField, TextArea] private string messageTemplate =
        "I scored {0} M in Zwap! Can you beat me?";

    [SerializeField] private string subject = "My Zwap score";

    [Header("Options")]
    [SerializeField] private bool includeScreenshot = true;

    // Text-only share. Safe to call directly from a button.
    public void Share()
    {
        new NativeShare()
            .SetSubject(subject)
            .SetText(BuildMessage())
            .Share();
    }

    // Button entry point: shares a screenshot when enabled, text otherwise.
    public void ShareWithScreenshot()
    {
        if (includeScreenshot)
            StartCoroutine(CaptureAndShare());
        else
            Share();
    }

    private IEnumerator CaptureAndShare()
    {
        // Wait so the whole frame (incl. UI) is rendered before capturing.
        yield return new WaitForEndOfFrame();

        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        string path = Path.Combine(Application.temporaryCachePath, "zwap-score.png");
        File.WriteAllBytes(path, shot.EncodeToPNG());
        Destroy(shot);

        new NativeShare()
            .SetSubject(subject)
            .SetText(BuildMessage())
            .AddFile(path)
            .Share();
    }

    private string BuildMessage()
    {
        int score = GameData.Instance != null ? GameData.Instance.score : 0;
        return string.Format(messageTemplate, score);
    }
}
