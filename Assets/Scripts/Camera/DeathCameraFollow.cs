using UnityEngine;

public class DeathCameraFollow : MonoBehaviour
{
    private Transform player;

    [SerializeField] private float distance = 5f;
    [SerializeField] private float height = 2f;
    [SerializeField] private float smoothSpeed = 5f;

    [SerializeField] private float orbitSpeed = 20f;

    private bool freezeCamera = false;
    private Vector3 orbitCenter;

    public void SetTarget(Transform target)
    {
        player = target;
    }

    public void FreezeCamera()
    {
        if (player == null) return;

        freezeCamera = true;
        orbitCenter = player.position + Vector3.up * 1f;
    }

    void LateUpdate()
    {
        if (player == null) return;

        if (freezeCamera)
        {
            // Orbit around the player's death position
            transform.RotateAround(orbitCenter, Vector3.up, orbitSpeed * Time.deltaTime);

            transform.LookAt(orbitCenter);
            return;
        }

        Vector3 desiredPos = player.position - player.forward * distance + Vector3.up * height;

        RaycastHit hit;

        if (Physics.Linecast(player.position, desiredPos, out hit))
        {
            transform.position = hit.point;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
        }

        transform.LookAt(player);
    }
}