using System.Collections;
using UnityEngine;
using Player;
namespace Enemy
{
    public class EnemyShoot : MonoBehaviour
    {
        [Header("Scene References")]
        // I don't think I'm gonna animate the gun because who's gonna see it lol
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject gunsmokeParticlePrefab;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Transform smokeParticleSpawnPoint;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Transform enemyGunRaycastOrigin;
        
        [Header("Audio")] 
        //public AudioClip dryFireSound;
        [SerializeField] private AudioClip cockingSound;
        [SerializeField] private AudioClip fireSound;

        public float Range {get; set;}

        public float aimTime { get; set; }

        private bool _isPlayerHit = false;
        private Transform player;

        public void SetPlayer(Transform p)
        {
            player = p;
        }

        public void PlayCockingSound()
        {
            if (audioSource != null && cockingSound != null)
                audioSource.PlayOneShot(cockingSound);
        }

        public void FireWeapon() // aim delay will be built in here
        {
            // Create particles
            if (muzzleFlashPrefab != null && muzzlePoint != null)
            {
                GameObject flash = Instantiate(muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
                GameObject smoke = Instantiate(gunsmokeParticlePrefab, smokeParticleSpawnPoint.position, smokeParticleSpawnPoint.rotation);
                Destroy(flash, 0.5f);
                Destroy(smoke, 5f);
            }
    
            // Audio
            if (audioSource != null && fireSound != null) audioSource.PlayOneShot(fireSound);
    
            // Raycast
            RaycastHit hit;

            Vector3 origin = enemyGunRaycastOrigin.position;
            Vector3 dir = (player.position - origin).normalized; 

            Debug.DrawRay(origin, dir * Range, Color.green, 1f); 

            if (Physics.Raycast(origin, dir, out hit, Range, ~0, QueryTriggerInteraction.Collide))
            {
                Debug.Log($"The enemy has hit {hit.collider.name} at distance {hit.distance}");

                PlayerController pc = hit.collider.GetComponentInParent<PlayerController>();
                if (pc != null)
                {
                    pc.Hit(hit.point, dir);
                }
            }
            else
            {
                Debug.Log("The enemy hit nothing");
            }


            Debug.Log($"Range={Range} origin={enemyGunRaycastOrigin.position} forward={enemyGunRaycastOrigin.forward}");
            Debug.DrawRay(enemyGunRaycastOrigin.position, enemyGunRaycastOrigin.forward * Mathf.Max(Range, 0.1f), Color.red, 1f);
            Debug.Log($"Distance to player = {Vector3.Distance(enemyGunRaycastOrigin.position, player.transform.position)}");
        }
    }
}