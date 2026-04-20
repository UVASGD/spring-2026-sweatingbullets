using UnityEngine;
using Enemy;
using Unity;
using Player;

public class ExplodingBarrelController : MonoBehaviour, IEnvironmentalObject
{
    public GameObject explodeFXPrefab;
    public float explosionRadius = 5f;

    public bool drawGizmos = true;
    [SerializeField] private float playerHeightOffset = 1.0f; 
    void Explode()
    {
        Instantiate(explodeFXPrefab, transform.position, transform.rotation);
        Destroy(this.transform.parent.gameObject);

        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider nearbyObject in colliders)
        {
            EnemyAI enemy = nearbyObject.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.ExplosionHit((enemy.transform.position - transform.position).normalized);
            }

            PlayerController playerController = nearbyObject.GetComponent<PlayerController>();
            if (playerController != null)
            {
                Vector3 playerAimPosition = playerController.transform.position + Vector3.up * playerHeightOffset;
                Vector3 rayOrigin = transform.position;
                Vector3 rayDirection = (playerAimPosition - rayOrigin).normalized;

                if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, explosionRadius))
                {
                    playerController.Hit(hit.point, rayDirection);
                }
                else
                {
                    // Fallback if raycast misses — still hit the player
                    playerController.Hit(playerAimPosition, rayDirection);
                }
            }
        }
    }

    public void HitByPlayer(){
        Explode();
    }

    void OnDrawGizmosSelected(){
        if(drawGizmos){
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
