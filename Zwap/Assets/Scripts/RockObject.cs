using System.Collections;
using UnityEngine;

public class RockWaterEffect : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float frameLength = 0.15f;
    [SerializeField] private Vector2 offset = Vector2.zero; // verschuif richting stroomrichting indien nodig
    [SerializeField] private int sortingOrder = -1; // lager dan de rots = tekent erachter (zelfde Sorting Layer!)

    private void Start()
    {
        if (frames == null || frames.Length == 0) return;

        var fx = new GameObject("WaterSwirl");
        fx.transform.SetParent(transform, false);
        fx.transform.localPosition = offset;

        var sr = fx.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;

        StartCoroutine(Animate(sr));
    }

    private IEnumerator Animate(SpriteRenderer sr)
    {
        int i = 0;
        while (true)
        {
            sr.sprite = frames[i];
            i = (i + 1) % frames.Length;
            yield return new WaitForSeconds(frameLength);
        }
    }
}