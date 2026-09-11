
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public enum SpawnZone
{
    Anywhere,    // full width, any X
    EdgesOnly,   // left band OR right band, never the middle
    LeftOnly,    // left band only
    RightOnly,   // right band only
    CenterOnly   // middle band only
}

[Serializable]
public class SpawnableItem
{
    public GameObject prefab;
    public SpawnZone spawnZone = SpawnZone.Anywhere;
}

public class Spawner : MonoBehaviour
{
    [Header("Spawnables")]
    public SpawnableItem[] items;      // each item now carries its own spawn rule
    public Transform spawnPoint;

    [Header("Timing")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float spawnSpeedPerScore = 0.002f;

    [Header("Spawn Area (X range)")]
    [SerializeField] private float minX = -3f;
    [SerializeField] private float maxX = 3f;

    [Header("Center Band (used by CenterOnly / EdgesOnly)")]
    [SerializeField] private float centerMinX = -1f;
    [SerializeField] private float centerMaxX = 1f;

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
        if (items == null || items.Length == 0) return;

        SpawnableItem chosen = items[Random.Range(0, items.Length)];
        if (chosen.prefab == null) return;

        float x = GetRandomX(chosen.spawnZone);
        Vector3 spawnPos = new Vector3(x, spawnPoint.position.y, spawnPoint.position.z);

        Instantiate(chosen.prefab, spawnPos, Quaternion.identity);
    }

    private float GetRandomX(SpawnZone zone)
    {
        switch (zone)
        {
            case SpawnZone.LeftOnly:
                return Random.Range(minX, centerMinX);

            case SpawnZone.RightOnly:
                return Random.Range(centerMaxX, maxX);

            case SpawnZone.CenterOnly:
                return Random.Range(centerMinX, centerMaxX);

            case SpawnZone.EdgesOnly:
                // pick left band or right band, never the middle
                return Random.value < 0.5f
                    ? Random.Range(minX, centerMinX)
                    : Random.Range(centerMaxX, maxX);

            case SpawnZone.Anywhere:
            default:
                return Random.Range(minX, maxX);
        }
    }
}
