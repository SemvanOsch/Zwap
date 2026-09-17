using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SkinUnlockEntry
{
    public Image image;         // de skin-afbeelding die zwart wordt tot unlock
    public Text unlockText;     // bv. "Nodig: 1000 punten" -- wordt verborgen zodra unlocked
    public Button skinButton;   // de knop erop; wordt uitgeschakeld tot unlock
    public int requiredScore;
}

public class SkinPreview : MonoBehaviour
{
    [SerializeField] private Image targetImage; // leeg = gebruikt zijn eigen Image
    [SerializeField] private Sprite[] skinSprites; // index moet overeenkomen met de skin-index van de knoppen

    [Header("Skin Unlocks")]
    [SerializeField] private SkinUnlockEntry[] unlockEntries; // uitbreidbaar: voeg gewoon een nieuwe entry toe

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        int skin = PlayerPrefs.GetInt(PlayerStart.SkinPrefsKey, 0);
        SetSkin(skin);

        ApplyUnlocks();
    }

    // Zet elke skin op "blacked out + knop uit" totdat de highscore hoog genoeg is.
    private void ApplyUnlocks()
    {
        if (unlockEntries == null) return;

        int highScore = SaveManager.Instance != null ? SaveManager.Instance.Data.highScore : 0;

        foreach (var entry in unlockEntries)
        {
            if (entry == null) continue;

            bool unlocked = highScore >= entry.requiredScore;

            if (entry.image != null)
                entry.image.color = unlocked ? Color.white : Color.black;

            if (entry.skinButton != null)
            {
                entry.skinButton.interactable = unlocked;

                // Zorg dat 'disabled' er ook echt volledig zwart uitziet i.p.v.
                // Unity's standaard grijze dim-kleur.
                ColorBlock colors = entry.skinButton.colors;
                colors.normalColor = Color.white;
                colors.disabledColor = Color.black;
                entry.skinButton.colors = colors;
            }

            if (entry.unlockText != null)
                entry.unlockText.gameObject.SetActive(!unlocked);
        }
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
}