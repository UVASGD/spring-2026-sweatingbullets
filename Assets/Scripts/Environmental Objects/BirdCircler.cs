using UnityEngine;

public class BirdCircler : MonoBehaviour
{
    public Vector3 centerPoint;
    public float radius = 10f;
    public float speed = 1f;
    public float heightOffset = 5f;

    public Vector3 boundsSize = new Vector3(50, 20, 50);

    private float angle;

    void Start()
    {
        angle = Random.Range(0f, 360f);
    }

    void Update()
    {
        angle += speed * Time.deltaTime;

        float rad = angle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(rad) * radius,
            heightOffset,
            Mathf.Sin(rad) * radius
        );

        Vector3 targetPos = centerPoint + offset;

        // Clamp within bounds
        Vector3 halfBounds = boundsSize / 2f;
        targetPos.x = Mathf.Clamp(targetPos.x, centerPoint.x - halfBounds.x, centerPoint.x + halfBounds.x);
        targetPos.y = Mathf.Clamp(targetPos.y, centerPoint.y - halfBounds.y, centerPoint.y + halfBounds.y);
        targetPos.z = Mathf.Clamp(targetPos.z, centerPoint.z - halfBounds.z, centerPoint.z + halfBounds.z);

        // Face movement direction
        Vector3 direction = (targetPos - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        transform.position = targetPos;
    }
}