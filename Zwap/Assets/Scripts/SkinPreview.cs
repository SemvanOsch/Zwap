using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SkinUnlockEntry
{
    public Image image;         // de skin-afbeelding: achtergrondkleur + witte rand tot unlock
    public Text unlockText;     // bv. "Nodig: 1000 punten" -- wordt verborgen zodra unlocked
    public Button skinButton;   // de knop erop; wordt uitgeschakeld tot unlock
    public int requiredScore;

    // Automatisch aangemaakt/beheerd door SkinPreview, niet zelf invullen.
    [System.NonSerialized] public Image outlineBackImage;
    [System.NonSerialized] public Sprite originalSprite;
}

public class SkinPreview : MonoBehaviour
{
    [SerializeField] private Image targetImage; // leeg = gebruikt zijn eigen Image
    [SerializeField] private Sprite[] skinSprites; // index moet overeenkomen met de skin-index van de knoppen

    [Header("Skin Unlocks")]
    [SerializeField] private SkinUnlockEntry[] unlockEntries; // uitbreidbaar: voeg gewoon een nieuwe entry toe
    [Tooltip("Dikte van de witte outline (in pixels) voor nog niet unlockte skins.")]
    [SerializeField] private Vector2 outlineThickness = new Vector2(2f, 2f);
    [Tooltip("Vulkleur voor locked skins -- zet dit gelijk aan je achtergrondkleur zodat de vulling 'onzichtbaar' lijkt.")]
    [SerializeField] private Color lockedFillColor = new Color32(0x2D, 0x1C, 0xBD, 0xFF); // #2D1CBD

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        int skin = PlayerPrefs.GetInt(PlayerStart.SkinPrefsKey, 0);
        SetSkin(skin);

        ApplyUnlocks();
    }

    // Zet elke nog niet unlockte skin op "pure achtergrondkleur vulling + witte
    // outline, knop uit". Eenmaal unlocked: normale, volledig zichtbare afbeelding.
    private void ApplyUnlocks()
    {
        if (unlockEntries == null) return;

        int highScore = SaveManager.Instance != null ? SaveManager.Instance.Data.highScore : 0;

        foreach (var entry in unlockEntries)
        {
            if (entry == null || entry.image == null) continue;

            if (entry.originalSprite == null)
                entry.originalSprite = entry.image.sprite;

            bool unlocked = highScore >= entry.requiredScore;

            EnsureOutlineBackImage(entry);

            if (unlocked)
            {
                entry.image.sprite = entry.originalSprite;
                entry.image.color = Color.white;
            }
            else
            {
                // Puur gekleurde versie i.p.v. een tint, zodat het echte platte
                // achtergrondblauw is in plaats van een "laagje" over de originele kleuren.
                entry.image.sprite = GetSolidColorSprite(entry.originalSprite, lockedFillColor);
                entry.image.color = Color.white;
            }

            if (entry.outlineBackImage != null)
                entry.outlineBackImage.gameObject.SetActive(!unlocked);

            if (entry.skinButton != null)
            {
                entry.skinButton.interactable = unlocked;

                ColorBlock colors = entry.skinButton.colors;
                colors.normalColor = Color.white;
                colors.disabledColor = Color.white; // de sprite zelf draagt nu de locked-kleur, niet de tint
                entry.skinButton.colors = colors;
            }

            if (entry.unlockText != null)
                entry.unlockText.gameObject.SetActive(!unlocked);
        }
    }

    // Maakt (eenmalig) een witte kopie van de sprite vlak ACHTER de echte afbeelding,
    // met een Outline-component erop. Omdat de echte afbeelding erbovenop zit, wordt
    // alleen het randje van deze witte kopie zichtbaar -- de rest wordt overlapt.
    private void EnsureOutlineBackImage(SkinUnlockEntry entry)
    {
        if (entry.outlineBackImage != null) return;

        var backGO = new GameObject(entry.image.name + "_LockOutline", typeof(RectTransform), typeof(Image));
        backGO.transform.SetParent(entry.image.transform.parent, false);

        RectTransform srcRT = entry.image.rectTransform;
        RectTransform backRT = backGO.GetComponent<RectTransform>();
        backRT.anchorMin = srcRT.anchorMin;
        backRT.anchorMax = srcRT.anchorMax;
        backRT.pivot = srcRT.pivot;
        backRT.anchoredPosition = srcRT.anchoredPosition;
        backRT.sizeDelta = srcRT.sizeDelta;

        // Vlak voor de echte afbeelding in de hiërarchie = erachter getekend.
        backGO.transform.SetSiblingIndex(entry.image.transform.GetSiblingIndex());

        var backImage = backGO.GetComponent<Image>();
        backImage.sprite = GetSolidColorSprite(entry.originalSprite, Color.white);
        backImage.color = Color.white;
        backImage.raycastTarget = false; // mag nooit kliks onderscheppen

        var outline = backGO.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = outlineThickness;

        entry.outlineBackImage = backImage;
    }

    // Genereert (en cachet) een puur gekleurde versie van een sprite: zelfde
    // alpha-vorm, maar RGB overal vervangen door 'color'. Nodig omdat Image.color
    // alleen vermenigvuldigt met de bestaande sprite-kleuren, en dus nooit een
    // kleurrijke sprite tot \u00e9\u00e9n platte kleur kan herleiden.
    private static readonly Dictionary<(Sprite, Color), Sprite> solidColorCache = new Dictionary<(Sprite, Color), Sprite>();

    private static Sprite GetSolidColorSprite(Sprite source, Color color)
    {
        if (source == null) return null;

        var key = (source, color);
        if (solidColorCache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        Texture2D tex = source.texture;
        Rect rect = source.textureRect;
        int x0 = Mathf.FloorToInt(rect.x);
        int y0 = Mathf.FloorToInt(rect.y);
        int w = Mathf.RoundToInt(rect.width);
        int h = Mathf.RoundToInt(rect.height);

        Color[] pixels;
        try
        {
            pixels = tex.GetPixels(x0, y0, w, h);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"SkinPreview: kan geen gekleurde versie maken van '{source.name}' — zet 'Read/Write Enabled' aan in de Texture Import Settings van deze sprite. Gebruik voorlopig de gewone sprite.", null);
            return source;
        }

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(color.r, color.g, color.b, pixels[i].a * color.a);

        var newTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        newTex.SetPixels(pixels);
        newTex.Apply();

        Sprite result = Sprite.Create(newTex, new Rect(0, 0, w, h),
            new Vector2(source.pivot.x / w, source.pivot.y / h), source.pixelsPerUnit);

        solidColorCache[key] = result;
        return result;
    }

    public void SetSkin(int index)
    {
        if (targetImage == null || skinSprites == null) return;
        if (index < 0 || index >= skinSprites.Length) return;

        targetImage.sprite = skinSprites[index];
    }

    public void SelectSkin(int index)
    {
        PlayerPrefs.SetInt(PlayerStart.SkinPrefsKey, index);
        PlayerPrefs.Save();

        SetSkin(index);
    }

    // Lets other scripts (e.g. the intro sequence) read the player's currently
    // selected skin sprite directly, without needing their own copy of
    // skinSprites or knowing about PlayerStart.SkinPrefsKey. Works even if this
    // GameObject is inactive or nested deep in a menu - it doesn't depend on
    // Awake having run, it reads PlayerPrefs itself each time it's called.
    public Sprite GetSelectedSkinSprite()
    {
        if (skinSprites == null || skinSprites.Length == 0) return null;

        int index = PlayerPrefs.GetInt(PlayerStart.SkinPrefsKey, 0);
        if (index < 0 || index >= skinSprites.Length) return null;

        return skinSprites[index];
    }
}