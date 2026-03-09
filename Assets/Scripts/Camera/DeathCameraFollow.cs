using UnityEngine;

public class DeathCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    private Vector3 offset;

    void Start()
    {
        if (target)
            offset = transform.position - target.position;
    }

    void LateUpdate()
    {
        if (!target) return;

        transform.position = target.position + offset;
    }
}