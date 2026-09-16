using UnityEngine;
using UnityEngine.UI;

public class SkinPreview : MonoBehaviour
{
    [SerializeField] private Image targetImage; // leeg = gebruikt zijn eigen Image
    [SerializeField] private Sprite[] skinSprites; // index moet overeenkomen met de skin-index van de knoppen

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
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
