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
        [Header("Starting Loadout")]
        [SerializeField] private bool startWithGun;
        [SerializeField] private int startingBulletCount;
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

        public void GiveGun()
        {
            if (_hasGun)
            {
                return;
            }

            _hasGun = true;
            UpdateGunVisuals();
        }

        public void DropGun(GameObject pickupPrefab, float groundProbeHeight, float spawnYOffset)
        {
            if (!_hasGun)
            {
                return;
            }

            _hasGun = false;
            UpdateGunVisuals();

            Vector3 groundPoint = ResolveDroppedGunPosition(groundProbeHeight);
            Vector3 spawnPosition = groundPoint + Vector3.up * Mathf.Max(0.5f, spawnYOffset + 0.5f);
            GameObject pickupObject = pickupPrefab != null
                ? Instantiate(pickupPrefab, spawnPosition, Quaternion.identity)
                : CreateFallbackGunPickup(spawnPosition);

            if (pickupObject.GetComponent<GunPickup>() == null)
            {
                pickupObject.AddComponent<GunPickup>();
            }

            EnableDroppedPickupPhysics(pickupObject, groundPoint);
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

        private Vector3 ResolveDroppedGunPosition(float groundProbeHeight)
        {
            float probeHeight = Mathf.Max(0.1f, groundProbeHeight);
            Vector3 rayOrigin = transform.position + Vector3.up * probeHeight;

            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    probeHeight * 2f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return transform.position;
        }

        private GameObject CreateFallbackGunPickup(Vector3 position)
        {
            GameObject pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pickupObject.name = "GunPickup";
            pickupObject.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
            pickupObject.transform.localScale = new Vector3(0.18f, 0.45f, 0.18f);

            Renderer renderer = pickupObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.2f, 0.2f, 0.2f);
            }

            return pickupObject;
        }

        private void EnableDroppedPickupPhysics(GameObject pickupObject, Vector3 groundPoint)
        {
            if (pickupObject == null)
            {
                return;
            }

            Rigidbody pickupRigidbody = pickupObject.GetComponent<Rigidbody>();
            if (pickupRigidbody == null)
            {
                pickupRigidbody = pickupObject.AddComponent<Rigidbody>();
            }

            if (!HasSolidCollider(pickupObject))
            {
                SnapPickupToGround(pickupObject, groundPoint);
                pickupRigidbody.isKinematic = true;
                pickupRigidbody.useGravity = false;
                pickupRigidbody.linearVelocity = Vector3.zero;
                pickupRigidbody.angularVelocity = Vector3.zero;
                return;
            }

            pickupRigidbody.isKinematic = false;
            pickupRigidbody.useGravity = true;
            pickupRigidbody.linearVelocity = Vector3.zero;
            pickupRigidbody.angularVelocity = Vector3.zero;
            pickupRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            pickupRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void SnapPickupToGround(GameObject pickupObject, Vector3 groundPoint)
        {
            Bounds? pickupBounds = GetPickupBounds(pickupObject);
            if (!pickupBounds.HasValue)
            {
                pickupObject.transform.position = groundPoint;
                return;
            }

            Vector3 position = pickupObject.transform.position;
            position += Vector3.up * (groundPoint.y - pickupBounds.Value.min.y);
            pickupObject.transform.position = position;
        }

        private Bounds? GetPickupBounds(GameObject pickupObject)
        {
            bool hasBounds = false;
            Bounds combinedBounds = default;

            foreach (Collider pickupCollider in pickupObject.GetComponentsInChildren<Collider>())
            {
                if (pickupCollider == null || !pickupCollider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = pickupCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(pickupCollider.bounds);
                }
            }

            return hasBounds ? combinedBounds : null;
        }

        private bool HasSolidCollider(GameObject pickupObject)
        {
            foreach (Collider pickupCollider in pickupObject.GetComponentsInChildren<Collider>())
            {
                if (pickupCollider != null && pickupCollider.enabled && !pickupCollider.isTrigger)
                {
                    return true;
                }
            }

            return false;
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
