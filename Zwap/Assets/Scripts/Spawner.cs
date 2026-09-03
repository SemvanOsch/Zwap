using UnityEngine;

public class RockSpawner : MonoBehaviour
{
    public GameObject[] itemPrefabs;   // prefabs to spawn
    public Transform spawnPoint;       // the SpawnPoint object
    public float spawnInterval = 2f;   // seconds between spawns

    public float[] lanePositions = { -3f, 0f, 3f }; // left, middle, right lane X positions

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            SpawnEntity();
            timer = 0f;
        }
    }

    void SpawnEntity()
    {
        
        int itemIndex = Random.Range(0, itemPrefabs.Length);
        GameObject chosenItem = itemPrefabs[itemIndex];
        // pick a random lane
        int laneIndex = Random.Range(0, lanePositions.Length);
        float laneX = lanePositions[laneIndex];

        // build the spawn position using that lane's X, and the SpawnPoint's Y and Z
        Vector3 spawnPos = new Vector3(laneX, spawnPoint.position.y, spawnPoint.position.z);

        // create the rock
        Instantiate(chosenItem, spawnPos, Quaternion.identity);
    }
}