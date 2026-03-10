using UnityEngine;
using Enemy;
using Unity;

public class ExplodingBarrelController : MonoBehaviour, IEnvironmentalObject
{
    public GameObject explodeFXPrefab;
    public float explosionRadius = 5f;

    public bool drawGizmos = true;

    void Explode(){
        Instantiate(explodeFXPrefab, transform.position, transform.rotation);
        Destroy(this.transform.parent.gameObject);

        // Check for nearby objects to apply explosion effects
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider nearbyObject in colliders)
        {
            // Check if the nearby object has an EnemyAI script and apply damage
            EnemyAI enemy = nearbyObject.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.ExplosionHit((enemy.transform.position - transform.position).normalized); // You can customize the hit parameters as needed
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
