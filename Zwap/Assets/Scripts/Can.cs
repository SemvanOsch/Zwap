using UnityEngine;

// A can that gives the player a one-time shield when touched. Movement and despawning
// are handled by your other script. Do NOT tag this object "Entity", or picking it up
// would count as a hit.
public class Can : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStart player = other.GetComponentInParent<PlayerStart>();
        if (player != null && player.GiveShield())
            Destroy(gameObject);
    }
}