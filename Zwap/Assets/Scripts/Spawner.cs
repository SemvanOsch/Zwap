using UnityEngine;

public class RockSpawner : MonoBehaviour
{
    public GameObject rockPrefab;      // the Rock prefab
    public Transform spawnPoint;       // the SpawnPoint object
    public float spawnInterval = 2f;   // seconds between spawns

    public float[] lanePositions = { -3f, 0f, 3f }; // left, middle, right lane X positions

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            SpawnRock();
            timer = 0f;
        }
    }

    void SpawnRock()
    {
        // pick a random lane
        int laneIndex = Random.Range(0, lanePositions.Length);
        float laneX = lanePositions[laneIndex];

        // build the spawn position using that lane's X, and the SpawnPoint's Y and Z
        Vector3 spawnPos = new Vector3(laneX, spawnPoint.position.y, spawnPoint.position.z);

        // create the rock
        Instantiate(rockPrefab, spawnPos, Quaternion.identity);
    }
}