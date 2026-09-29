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

        // Space.World so the fall stays vertical even when something (e.g. RockRotation
        // on the rock) has spun this transform — otherwise "down" would rotate with it
        // and the object would spiral sideways. Objects that never rotate sit at identity,
        // so world-down and self-down are the same for them: no behaviour change there.
        transform.Translate(Vector3.down * speed * multiplier * Time.deltaTime, Space.World);

        if (transform.position.y < despawnY)
        {
            Destroy(gameObject);
        }
    }
}