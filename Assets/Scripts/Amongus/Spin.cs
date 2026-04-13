using UnityEngine;

public class SkyArcSpinner : MonoBehaviour
{
    [Header("Orbit Settings")]
    public float speed = 5f;           // degrees per second
    public Vector3 axis = Vector3.up;  // spin axis (tilt this per arc)
    public bool randomizeStart = true;

    void Start()
    {
        if (randomizeStart)
            transform.Rotate(axis, Random.Range(0f, 360f));
    }

    void Update()
    {
        transform.Rotate(axis, speed * Time.deltaTime, Space.World);
    }
}