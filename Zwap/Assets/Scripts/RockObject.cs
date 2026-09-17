using System.Collections;
using UnityEngine;

public class RockWaterEffect : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float frameLength = 0.15f;
    [SerializeField] private Vector2 offset = Vector2.zero; // verschuif richting stroomrichting indien nodig
    [SerializeField] private int sortingOrder = -1; // lager dan de rots = tekent erachter (zelfde Sorting Layer!)

    [Header("Dimensie-kleuren")]
    [Tooltip("1 kleur per ControlType (index = (int)ControlType), plus eventueel extra kleuren aan het eind voor specifieke inverted-varianten (zie Inverted Overrides).")]
    [SerializeField] private Color[] dimensionColors = new Color[8]; // 6 basis + 2 dedicated inverted, voor nu
    [SerializeField] private int currentDimensionIndex = 0; // startkleur bij spawn

    [System.Serializable]
    public class InvertedColorOverride
    {
        public ControlType type;
        [Tooltip("Index in dimensionColors die dit type gebruikt zodra het inverted is.")]
        public int invertedColorIndex;
    }

    [Tooltip("Alleen de types die hier genoemd staan krijgen een eigen kleur voor hun inverted-variant. Elk ander type gebruikt gewoon zijn normale kleur, ook als het inverted is.")]
    [SerializeField] private InvertedColorOverride[] invertedOverrides;

    private SpriteRenderer sr;
    private bool subscribedToSwitcher;
    private bool subscribedToReveal;
    private bool transitioning;
    private Color transitionFrom;
    private Color transitionTo;

    private void OnEnable()
    {
        TrySubscribeSwitcher();
        TrySubscribeReveal();
    }

    private void Start()
    {
        // Safety net: if ControlSwitcher/BackgroundReveal weren't set yet during
        // OnEnable (script init order), subscribe now that every Awake has run.
        TrySubscribeSwitcher();
        TrySubscribeReveal();

        if (frames == null || frames.Length == 0) return;

        int dimensionCount = System.Enum.GetValues(typeof(ControlType)).Length;
        if (dimensionColors == null || dimensionColors.Length < dimensionCount)
        {
            Debug.LogWarning($"RockWaterEffect: dimensionColors heeft {(dimensionColors?.Length ?? 0)} kleuren, maar er zijn {dimensionCount} ControlTypes. Vergroot de array (Size) anders krijgen hogere dimensies allemaal de laatste kleur.", this);
        }

        // Pak de dimensie die NU echt actief is, i.p.v. altijd bij index 0 te starten —
        // anders toont een net gespawnde rots de verkeerde kleur tot de volgende wissel.
        if (ControlSwitcher.Instance != null && dimensionColors != null && dimensionColors.Length > 0)
            currentDimensionIndex = ComputeDimensionIndex(ControlSwitcher.Instance.CurrentControl, ControlSwitcher.Instance.IsInverted);

        var fx = new GameObject("WaterSwirl");
        fx.transform.SetParent(transform, false);
        fx.transform.localPosition = offset;

        sr = fx.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;

        ApplyDimensionColor();
        StartCoroutine(Animate(sr));
    }

    private void OnDisable()
    {
        if (subscribedToSwitcher && ControlSwitcher.Instance != null)
            ControlSwitcher.Instance.OnControlChanged -= HandleControlChanged;
        subscribedToSwitcher = false;

        if (subscribedToReveal && BackgroundReveal.Instance != null)
            BackgroundReveal.Instance.OnRevealProgress -= HandleRevealProgress;
        subscribedToReveal = false;
    }

    private void TrySubscribeSwitcher()
    {
        if (subscribedToSwitcher || ControlSwitcher.Instance == null) return;
        ControlSwitcher.Instance.OnControlChanged += HandleControlChanged;
        subscribedToSwitcher = true;
    }

    private void TrySubscribeReveal()
    {
        if (subscribedToReveal || BackgroundReveal.Instance == null) return;
        BackgroundReveal.Instance.OnRevealProgress += HandleRevealProgress;
        subscribedToReveal = true;
    }

    // Basis-index = (int)type. Alleen als dit type in invertedOverrides staat EN
    // inverted is, wordt de aparte, daarvoor aangewezen index gebruikt.
    private int ComputeDimensionIndex(ControlType type, bool inverted)
    {
        if (inverted && invertedOverrides != null)
        {
            foreach (var o in invertedOverrides)
            {
                if (o.type == type)
                    return Mathf.Clamp(o.invertedColorIndex, 0, dimensionColors.Length - 1);
            }
        }

        return Mathf.Clamp((int)type, 0, dimensionColors.Length - 1);
    }

    private void HandleControlChanged(ControlType newControl)
    {
        if (dimensionColors == null || dimensionColors.Length == 0) return;

        // Het event geeft alleen het ControlType door; IsInverted lezen we, net als
        // ControlBackground doet, los uit de switcher — die staat er al bij tegen de
        // tijd dat dit event afgaat.
        bool inverted = ControlSwitcher.Instance != null && ControlSwitcher.Instance.IsInverted;
        int newIndex = ComputeDimensionIndex(newControl, inverted);

        if (BackgroundReveal.Instance == null)
        {
            // Geen BackgroundReveal in de scene: gewoon direct wisselen.
            currentDimensionIndex = newIndex;
            ApplyDimensionColor();
            return;
        }

        // De daadwerkelijke Lerp gebeurt hierna in HandleRevealProgress, op hetzelfde
        // tempo als de groeiende cirkel.
        transitionFrom = sr != null ? sr.color : dimensionColors[currentDimensionIndex];
        transitionTo = dimensionColors[newIndex];
        currentDimensionIndex = newIndex;
        transitioning = true;
    }

    private void HandleRevealProgress(float t)
    {
        if (!transitioning || sr == null) return;
        sr.color = Color.Lerp(transitionFrom, transitionTo, t);
        if (t >= 1f) transitioning = false;
    }

    // Handmatige/losse override, bv. om een startkleur te zetten zonder op de
    // switcher/reveal te wachten. Wisselt altijd direct, niet vloeiend.
    public void SetDimension(int index)
    {
        currentDimensionIndex = index;
        ApplyDimensionColor();
    }

    private void ApplyDimensionColor()
    {
        if (sr == null || dimensionColors == null || dimensionColors.Length == 0) return;
        sr.color = dimensionColors[Mathf.Clamp(currentDimensionIndex, 0, dimensionColors.Length - 1)];
    }

    private IEnumerator Animate(SpriteRenderer sr)
    {
        int previous = -1;
        while (true)
        {
            int i = frames.Length > 1 ? Random.Range(0, frames.Length) : 0;
            while (i == previous)
                i = Random.Range(0, frames.Length);

            sr.sprite = frames[i];
            previous = i;
            yield return new WaitForSeconds(frameLength);
        }
    }
}