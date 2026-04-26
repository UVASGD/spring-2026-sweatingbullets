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

        [Header("Accuracy")]
        [Tooltip("Miss chance at 0 player nerves.")]
        [SerializeField, Range(0f, 1f)] private float baseMissChance = 0.05f;
        [Tooltip("Miss chance at 100 player nerves. Player nerves climb with time and combat events, so this caps how forgiving the enemy gets late round.")]
        [SerializeField, Range(0f, 1f)] private float maxMissChance = 0.45f;
        [Tooltip("Maximum angular offset (degrees) applied to a missed shot's raycast.")]
        [SerializeField, Range(0f, 15f)] private float maxMissAngleDegrees = 3f;

        public float Range {get; set;}

        public float aimTime { get; set; }

        private bool _isPlayerHit = false;
        private Transform player;
        private NervesManager nervesManager;

        public void SetPlayer(Transform p)
        {
            player = p;
        }

        public void SetNervesManager(NervesManager nm)
        {
            nervesManager = nm;
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

            // Roll miss chance based on player nerves (high nerves = more enemy misses, thematic relief).
            float nervesNormalized = nervesManager != null
                ? Mathf.Clamp01(nervesManager.CurrentNerves / 100f)
                : 0f;
            float missChance = Mathf.Lerp(baseMissChance, maxMissChance, nervesNormalized);
            if (UnityEngine.Random.value < missChance)
            {
                dir = ApplyMissOffset(dir);
            }

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

        private Vector3 ApplyMissOffset(Vector3 dir)
        {
            // Build a perpendicular frame around dir so yaw/pitch stay relative to the shot.
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right = right.normalized;
            Vector3 up = Vector3.Cross(dir, right).normalized;

            float yaw = UnityEngine.Random.Range(-maxMissAngleDegrees, maxMissAngleDegrees);
            float pitch = UnityEngine.Random.Range(-maxMissAngleDegrees, maxMissAngleDegrees);
            return Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, right) * dir;
        }
    }
}