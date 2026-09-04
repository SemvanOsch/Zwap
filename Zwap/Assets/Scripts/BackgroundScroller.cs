using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{
    public float scrollSpeed = 2f;
    public float resetPoint = -10f;    // how far down before resetting
    public float startPoint = 10f;     // where it resets back to

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        // move the tilemap downward
        transform.position += Vector3.down * scrollSpeed * Time.deltaTime;

        // reset back to start when it goes too far
        if (transform.position.y <= resetPoint)
        {
            transform.position = new Vector3(
                transform.position.x,
                startPoint,
                transform.position.z
            );
        }
    }
}
