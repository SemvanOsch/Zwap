using UnityEngine;

// A can that gives the player a one-time shield when touched. Movement and despawning
// are handled by your other script. Do NOT tag this object "Entity", or picking it up
// would count as a hit.
public class Can : MonoBehaviour
{
    // Max tilt in degrees applied once when the can spawns. The actual rotation is a
    // random value between -maxSpawnTilt and +maxSpawnTilt, so the can leans left or right.
    [SerializeField] private float maxSpawnTilt = 20f;

    private void Awake()
    {
        float tilt = Random.Range(-maxSpawnTilt, maxSpawnTilt);
        transform.rotation = Quaternion.Euler(0f, 0f, tilt);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStart player = other.GetComponentInParent<PlayerStart>();
        if (player != null && player.GiveShield())
            Destroy(gameObject);
    }
}