using UnityEngine;

public class MoveDown : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float speedPerScore = 2f;
    [SerializeField] private float despawnY = -10f; // Y position below which the object gets destroyed

    void Update()
    {
        float multiplier = 1f;
        if (ScoreManager.Instance != null)
            multiplier = 1f + speedPerScore * ScoreManager.Instance.GetScore();

        transform.Translate(Vector3.down * speed * multiplier * Time.deltaTime);

        if (transform.position.y < despawnY)
        {
            Destroy(gameObject);
        }
    }
}