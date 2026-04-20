using System.Collections;
using SUPERCharacter;
using UnityEngine;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private DeathCameraController deathCam;
        [SerializeField] private MonoBehaviour movementScript;
        [SerializeField] private GameObject gunActual;
        [SerializeField] private GameObject gunViewmodel;
        [SerializeField] private Camera playerCamera;
        [Header("Starting Loadout")]
        [SerializeField] private bool startWithGun;
        [SerializeField] private int startingBulletCount;
        [Header("Gun Drop Physics")]
        [SerializeField] private float dropForwardForce = 2f;
        [SerializeField] private float dropUpForce = 1.5f;
        [SerializeField] private float dropAngularVelocity = 5f;
        [Header("Interaction Crosshair")]
        [SerializeField] private Color crosshairIdleColor = Color.white;
        [SerializeField] private Color crosshairInteractableColor = Color.green;
        [SerializeField] private float crosshairLineLength = 8f;
        [SerializeField] private float crosshairThickness = 2f;
        [SerializeField] private float crosshairGap = 4f;

        private Rigidbody _rb;
        private SUPERCharacterAIO _characterController;
        private Texture2D _crosshairTexture;
        private bool _isDead;
        private bool _hasGun;
        private bool _isLookingAtInteractable;
        private int _bulletCount;

        public bool HasGun => _hasGun;
        public int BulletCount => _bulletCount;

        void Start()
        {
            _rb = GetComponent<Rigidbody>();
            _characterController = GetComponent<SUPERCharacterAIO>();
            CacheGunReferences();
            _hasGun = startWithGun;
            _bulletCount = Mathf.Max(0, startingBulletCount);
            UpdateGunVisuals();
            EnsureCrosshairTexture();
        }

        private void LateUpdate()
        {
            UpdateInteractionTargetState();
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

        public void GiveGun(int bulletCount = 0)
        {
            if (_hasGun)
            {
                return;
            }

            _hasGun = true;
            _bulletCount = bulletCount;
            UpdateGunVisuals();
        }

        public void DropGun(GameObject pickupPrefab)
        {
            if (!_hasGun || pickupPrefab == null)
            {
                return;
            }

            _hasGun = false;
            UpdateGunVisuals();

            int savedBullets = _bulletCount;
            _bulletCount = 0;

            Vector3 spawnPos = playerCamera != null
                ? playerCamera.transform.position + playerCamera.transform.forward * 0.8f
                : transform.position + Vector3.up * 1.5f;
            Quaternion spawnRot = playerCamera != null
                ? playerCamera.transform.rotation
                : Quaternion.identity;

            GameObject pickupObject = Instantiate(pickupPrefab, spawnPos, spawnRot);

            GunPickup gunPickup = pickupObject.GetComponent<GunPickup>();
            if (gunPickup != null)
            {
                gunPickup.Initialize(savedBullets);
            }

            Rigidbody pickupRb = pickupObject.GetComponent<Rigidbody>();
            if (pickupRb != null)
            {
                Vector3 throwDir = playerCamera != null
                    ? playerCamera.transform.forward * dropForwardForce + Vector3.up * dropUpForce
                    : Vector3.forward * dropForwardForce + Vector3.up * dropUpForce;
                pickupRb.linearVelocity = throwDir;
                pickupRb.angularVelocity = new Vector3(
                    UnityEngine.Random.Range(-dropAngularVelocity, dropAngularVelocity),
                    UnityEngine.Random.Range(-dropAngularVelocity, dropAngularVelocity),
                    UnityEngine.Random.Range(-dropAngularVelocity, dropAngularVelocity));
            }
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

        private void UpdateInteractionCrosshair()
        {
            if (!ShouldShowInteractionCrosshair())
            {
                return;
            }

            EnsureCrosshairTexture();
            if (_crosshairTexture == null)
            {
                return;
            }

            Color originalColor = GUI.color;
            GUI.color = _isLookingAtInteractable ? crosshairInteractableColor : crosshairIdleColor;

            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            float halfThickness = crosshairThickness * 0.5f;

            GUI.DrawTexture(new Rect(centerX - halfThickness, centerY - crosshairGap - crosshairLineLength, crosshairThickness, crosshairLineLength), _crosshairTexture);
            GUI.DrawTexture(new Rect(centerX - halfThickness, centerY + crosshairGap, crosshairThickness, crosshairLineLength), _crosshairTexture);
            GUI.DrawTexture(new Rect(centerX - crosshairGap - crosshairLineLength, centerY - halfThickness, crosshairLineLength, crosshairThickness), _crosshairTexture);
            GUI.DrawTexture(new Rect(centerX + crosshairGap, centerY - halfThickness, crosshairLineLength, crosshairThickness), _crosshairTexture);

            GUI.color = originalColor;
        }

        private void UpdateInteractionTargetState()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<SUPERCharacterAIO>();
            }

            if (!ShouldShowInteractionCrosshair() || _characterController == null)
            {
                _isLookingAtInteractable = false;
                return;
            }

            _isLookingAtInteractable = _characterController.TryGetCurrentInteractable(out _);
        }

        private bool ShouldShowInteractionCrosshair()
        {
            if (_isDead || _hasGun)
            {
                return false;
            }

            if (_characterController == null)
            {
                _characterController = GetComponent<SUPERCharacterAIO>();
            }

            if (_characterController == null)
            {
                return false;
            }

            return _characterController.cameraPerspective != PerspectiveModes._3rdPerson ||
                   _characterController.showCrosshairIn3rdPerson;
        }

        private void EnsureCrosshairTexture()
        {
            if (_crosshairTexture != null)
            {
                return;
            }

            _crosshairTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _crosshairTexture.SetPixel(0, 0, Color.white);
            _crosshairTexture.Apply();
        }

        private void OnGUI()
        {
            UpdateInteractionCrosshair();
        }
    }
}
