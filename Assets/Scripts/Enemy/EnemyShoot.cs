using System.Collections;
using UnityEngine;
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
            if (Physics.Raycast(enemyGunRaycastOrigin.position, enemyGunRaycastOrigin.forward, out hit, Range))
            {
                PlayerController player = hit.transform.GetComponent<PlayerController>();
                if (player != null)
                {
                    player.Hit(hit.point, enemyGunRaycastOrigin.forward);
                }
            }
        }
    }
}