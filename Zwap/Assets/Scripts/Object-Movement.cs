using UnityEngine;

public class MoveDown : MonoBehaviour
{
    public float speed = 5f;
    public float despawnY = -10f; // Y position below which the object gets destroyed

    void Update()
    {
        transform.Translate(Vector3.down * speed * Time.deltaTime);

        if (transform.position.y < despawnY)
        {
            Destroy(gameObject);
        }
    }
}