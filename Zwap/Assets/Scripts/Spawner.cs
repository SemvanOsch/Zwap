
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class RockSpawner : MonoBehaviour
{
    public GameObject[] itemPrefabs;   // Prefabs van de stenen/items
    public Transform spawnPoint;       // Het SpawnPoint object

    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float spawnSpeedPerScore = 0.002f;
    [SerializeField] private float[] lanePositions = { -3f, 0f, 3f };

    [Header("Sprite Animatie")]
    [SerializeField] private Sprite[] effectSprites;  // Sprites achter elkaar
    [SerializeField] private float effectFrameTime = 0.08f;
    [SerializeField] private int effectSortingOrder = 10;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;

        float multiplier = 1f;

        if (ScoreManager.Instance != null)
            multiplier = 1f + spawnSpeedPerScore * ScoreManager.Instance.GetScore();

        float effectiveInterval = spawnInterval / multiplier;

        if (timer >= effectiveInterval)
        {
            SpawnEntity();
            timer = 0f;
        }
    }

    void SpawnEntity()
    {
        // Kies een willekeurige steen
        int itemIndex = Random.Range(0, itemPrefabs.Length);
        GameObject chosenItem = itemPrefabs[itemIndex];

        // Kies een willekeurige lane
        int laneIndex = Random.Range(0, lanePositions.Length);
        float laneX = lanePositions[laneIndex];

        // Bereken de spawnpositie
        Vector3 spawnPos = new Vector3(
            laneX,
            spawnPoint.position.y,
            spawnPoint.position.z
        );

        GameObject spawnedItem = Instantiate(
            chosenItem,
            spawnPos,
            Quaternion.identity
        );

    if (effectSprites != null && effectSprites.Length > 0)
        {
            StartCoroutine(PlaySpriteAnimation(spawnedItem.transform));
        }
    }

    IEnumerator PlaySpriteAnimation(Transform parent)
    {
    GameObject effectObject = new GameObject("SpawnEffect");

    // Maak de animatie een child van de steen
    effectObject.transform.SetParent(parent);

    // Positie relatief aan de steen
    effectObject.transform.localPosition = Vector3.zero;
    effectObject.transform.localRotation = Quaternion.identity;
    effectObject.transform.localScale = Vector3.one;

    SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
    renderer.sortingOrder = effectSortingOrder;

    while (parent != null) 
    { 
        for (int i = 0; i < effectSprites.Length; i++)
        {
            renderer.sprite = effectSprites[i];

            yield return new WaitForSeconds(effectFrameTime);
            // Controleer of de steen ondertussen is vernietigd
            if (parent == null)
            break;
        }
    } // Verwijder de animatie

    if (effectObject != null)
        Destroy(effectObject);
    }
}
