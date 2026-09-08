using UnityEngine;

public class SimpleAnimation : MonoBehaviour
{
    public Sprite[] frames;

    public float frameTime = 0.08f;
    public float pauseTime = 1.3f;

    public AudioSource audioSource;

    private SpriteRenderer spriteRenderer;
    private bool isPlaying = false;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;
    }

    public void Play()
    {
        if (!isPlaying)
        {
            StartCoroutine(PlayAnimation());
        }
    }

    System.Collections.IEnumerator PlayAnimation()
    {
        isPlaying = true;

        // GELUID START GELIJKTIJDIG MET DE ANIMATIE
        if (audioSource != null)
        {
            audioSource.Play();
        }

        // Zichtbaar
        spriteRenderer.enabled = true;

        // Frames 1-2
        yield return StartCoroutine(PlayFrames(0, 2));

        // Pauze: onzichtbaar
        spriteRenderer.enabled = false;
        yield return new WaitForSeconds(pauseTime);

        // Weer zichtbaar
        spriteRenderer.enabled = true;

        // Frames 3-5
        yield return StartCoroutine(PlayFrames(2, 5));

        // Pauze: onzichtbaar
        spriteRenderer.enabled = false;
        yield return new WaitForSeconds(pauseTime);

        // Weer zichtbaar
        spriteRenderer.enabled = true;

        // Frames 6-10
        yield return StartCoroutine(PlayFrames(5, 10));

        // Animatie klaar: GameObject verwijderen
        Destroy(gameObject);
    }

    System.Collections.IEnumerator PlayFrames(int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            spriteRenderer.sprite = frames[i];

            yield return new WaitForSeconds(frameTime);
        }
    }
}