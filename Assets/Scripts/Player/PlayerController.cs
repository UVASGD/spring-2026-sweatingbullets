using UnityEngine;
using System.Collections;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private DeathCameraController deathCam;
        [SerializeField] private MonoBehaviour movementScript;
        [SerializeField] private GameObject gunActual;
        [SerializeField] private GameObject gunViewmodel;

        private Rigidbody _rb;
        private bool _isDead;
        private bool _hasGun;
        private int _bulletCount;

        public bool HasGun => _hasGun;
        public int BulletCount => _bulletCount;

        void Start()
        {
            _rb = GetComponent<Rigidbody>();
            CacheGunReferences();
            _hasGun = false;
            _bulletCount = 0;
            UpdateGunVisuals();
        }

        public void Hit(Vector3 hitPoint, Vector3 hitDirection)
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log("Omg! The player died. oof");

            // Disable player movement
            if (movementScript)
                movementScript.enabled = false;

            // Activate death camera
            if (deathCam)
                deathCam.ActivateDeathCam();

            StartCoroutine(DelayedFall(hitPoint, hitDirection));
        }

        IEnumerator DelayedFall(Vector3 hitPoint, Vector3 hitDirection)
        {
            // Clear existing movement
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            // Enable physics
            _rb.isKinematic = false;
            _rb.useGravity = true;

            // Allow the capsule to rotate and fall
            _rb.constraints = RigidbodyConstraints.None;

            // Apply force where the player was hit
            _rb.AddForceAtPosition(hitDirection * 3f, hitPoint, ForceMode.Impulse);

            // Add a little torque so the capsule tips over
            _rb.AddTorque(transform.right * 1f, ForceMode.Impulse);

            yield return null;
        }

        public void GiveGun()
        {
            if (_hasGun)
            {
                return;
            }

            _hasGun = true;
            UpdateGunVisuals();
        }

        public void AddAmmo(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _bulletCount += amount;
        }

        public bool TryConsumeBullet()
        {
            if (!_hasGun || _bulletCount <= 0)
            {
                return false;
            }

            _bulletCount--;
            return true;
        }

        private void CacheGunReferences()
        {
            if (gunActual == null)
            {
                gunActual = FindChildGameObject("gun_actual");
            }

            if (gunViewmodel == null)
            {
                gunViewmodel = FindChildGameObject("gun_viewmodel");
            }
        }

        private GameObject FindChildGameObject(string childName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child != transform && child.name == childName)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private void UpdateGunVisuals()
        {
            if (gunActual != null)
            {
                gunActual.SetActive(_hasGun);
            }

            if (gunViewmodel != null)
            {
                gunViewmodel.SetActive(_hasGun);
            }
        }
    }
}
