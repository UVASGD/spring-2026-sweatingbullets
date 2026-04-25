using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using Enemy;

namespace Player
{
    public class WeaponController : MonoBehaviour
    {
        public readonly struct ShotResolutionContext
        {
            public ShotResolutionContext(
                Vector3 origin,
                Vector3 direction,
                float range,
                bool hitEnemy,
                bool hitSomething,
                float hitDistance)
            {
                Origin = origin;
                Direction = direction;
                Range = range;
                HitEnemy = hitEnemy;
                HitSomething = hitSomething;
                HitDistance = hitDistance;
            }

            public Vector3 Origin { get; }
            public Vector3 Direction { get; }
            public float Range { get; }
            public bool HitEnemy { get; }
            public bool HitSomething { get; }
            public float HitDistance { get; }
        }

        [Header("Input References")] public InputActionReference fireAction;
        public InputActionReference hammerPullAction;
        public InputActionReference aimDownSightsAction;


        [Header("Scene References")] public Animator gunAnimator;
        public GameObject muzzleFlashPrefab;
        public GameObject gunsmokePrefab;
        public Transform muzzlePoint;
        public Transform smokeSpawnPoint;
        public AudioSource weaponAudio;
        public Camera playerCamera;
        public Transform aimPosition;
        public Transform HipFirePosition;
        [SerializeField] private Transform viewmodelRoot;
        [Header("Pickup Equip")]
        [SerializeField, Min(0f)] private float pickupEquipDuration = 0.25f;
        [SerializeField] private Vector3 pickupStartLocalOffset = new Vector3(0.35f, -0.45f, 0.55f);
        [SerializeField] private Vector3 pickupStartLocalEulerOffset = new Vector3(18f, -25f, 8f);

        private const string FireTrigger = "Fire";
        private const string HammerPullBool = "HammerPull";
        private bool _isAiming = false;
        private float _hipToAimZOffset;
        [SerializeField] private bool startsWithWeapon = true;
        [SerializeField] private int startingAmmo = 0;
        private bool _hasWeapon;
        private int _ammoCount;
        private Coroutine _pickupEquipRoutine;
        private bool _isEquippingWeapon;

        public bool IsAiming => _isAiming;
        public bool HasWeapon => _hasWeapon;
        public int AmmoCount => _ammoCount;

        public event Action OnWeaponFired;
        public event Action<ShotResolutionContext> OnWeaponShotResolved;

        private Vector3
            _originalSmokeLocalPos; // cached hip-fire local position of smoke spawn point

        [Header("Audio")] public AudioClip dryFireSound;
        public AudioClip cockingSound;
        public AudioClip fireSound;

        [Header("Trigger Delay (Nerves)")]
        [Tooltip("Optional — if assigned, high nerves add a hesitation before firing")]
        public NervesManager nervesManager;
        [Tooltip("Max trigger delay at 100 nerves (seconds)")]
        [Range(0f, 0.5f)]
        public float maxTriggerDelay = 0.15f;

        public float aimSpeed = 25f;
        public float range = 100f;
        private bool _isHammerCocked = false;
        private bool _isFiring = false;

        [Header("Walking Sway")]
        [Tooltip("Player rigidbody whose horizontal speed drives the sway. Auto-resolved from parent if left empty.")]
        public Rigidbody playerRigidbody;
        [Tooltip("Peak local-space offset of the sway at reference speed")]
        public float swayAmplitude = 0.015f;
        [Tooltip("Sway oscillation frequency (Hz) at reference speed")]
        public float swayFrequency = 6f;
        [Tooltip("Horizontal speed at which sway reaches full amplitude/frequency")]
        public float swayReferenceSpeed = 5f;
        [Tooltip("Sway multiplier while aiming down sights")]
        [Range(0f, 1f)]
        public float swayAdsMultiplier = 0.25f;
        [Tooltip("How fast sway weight eases in/out as speed changes (per second)")]
        public float swaySmoothing = 8f;

        private Vector3 _baseLocalPosition;
        private float _swayPhase;
        private float _smoothedSwayWeight;

        public bool
            canFireWeapon =
                false; // modified by event OnStateExit() in the HammerPull state in the Animator for the player's gun. Script called "Pulled.cs"

        private void Awake()
        {
            if (viewmodelRoot == null)
                viewmodelRoot = transform;

            _hasWeapon = startsWithWeapon;
            _ammoCount = Mathf.Max(0, startingAmmo);
            ApplyWeaponVisibility();
        }

        private void Start()
        {
            // Making the smoke look like it's coming from the viewmodel when aiming
            if (HipFirePosition != null && aimPosition != null)
            {
                _hipToAimZOffset =
                    Math.Abs(
                        HipFirePosition.localPosition.z - aimPosition.localPosition.z -
                        0.5f /*included some offset for customization*/);
            }

            if (smokeSpawnPoint != null)
                _originalSmokeLocalPos = smokeSpawnPoint.localPosition; // cache hip-fire local position

            _baseLocalPosition = transform.localPosition;
            if (playerRigidbody == null) playerRigidbody = GetComponentInParent<Rigidbody>();
        }

        private void Update()
        {
            if (!_hasWeapon)
                return;

            if (_isEquippingWeapon)
                return;

            // Move the base position toward aim or hip target — sway is layered on top.
            Vector3 targetBase = _isAiming ? aimPosition.localPosition : HipFirePosition.localPosition;
            _baseLocalPosition = Vector3.MoveTowards(_baseLocalPosition, targetBase, aimSpeed * Time.deltaTime);

            if (_isAiming)
            {
                smokeSpawnPoint.localPosition = Vector3.MoveTowards(smokeSpawnPoint.localPosition,
                    aimPosition.localPosition - new Vector3(0, 0, _hipToAimZOffset), aimSpeed * Time.deltaTime);
            }
            else
            {
                smokeSpawnPoint.localPosition = _originalSmokeLocalPos;
            }

            // Walking sway driven by player horizontal speed
            float speed = 0f;
            if (playerRigidbody != null)
            {
                Vector3 v = playerRigidbody.linearVelocity;
                speed = new Vector2(v.x, v.z).magnitude;
            }
            float speedFactor = swayReferenceSpeed > 0f ? Mathf.Clamp01(speed / swayReferenceSpeed) : 0f;
            float targetWeight = speedFactor * (_isAiming ? swayAdsMultiplier : 1f);
            _smoothedSwayWeight = Mathf.MoveTowards(_smoothedSwayWeight, targetWeight, swaySmoothing * Time.deltaTime);

            _swayPhase += _smoothedSwayWeight * swayFrequency * Time.deltaTime * Mathf.PI * 2f;
            Vector3 swayOffset = new Vector3(
                Mathf.Sin(_swayPhase) * swayAmplitude,
                Mathf.Sin(_swayPhase * 2f) * swayAmplitude * 0.5f,
                0f) * _smoothedSwayWeight;

            transform.localPosition = _baseLocalPosition + swayOffset;
        }

        private void OnEnable()
        {
            if (fireAction != null)
            {
                fireAction.action.Enable();
                fireAction.action.performed += FireWeapon; // Subscribe to the "performed" event
            }

            if (hammerPullAction != null)
            {
                hammerPullAction.action.Enable();
                hammerPullAction.action.performed += PullHammer;
            }

            if (aimDownSightsAction != null)
            {
                aimDownSightsAction.action.Enable();
                aimDownSightsAction.action.performed += HandleAimPerformed;
                aimDownSightsAction.action.canceled += HandleAimCanceled;
            }
            else
            {
                Debug.LogWarning("WeaponController: aimDownSightsAction is null!");
            }
        }

        private void OnDisable()
        {
            if (fireAction != null)
            {
                fireAction.action.performed -= FireWeapon; // Unsubscribe
                fireAction.action.Disable();
            }

            if (hammerPullAction != null)
            {
                hammerPullAction.action.performed -= PullHammer;
                hammerPullAction.action.Disable();
            }

            if (aimDownSightsAction != null)
            {
                aimDownSightsAction.action.performed -= HandleAimPerformed;
                aimDownSightsAction.action.canceled -= HandleAimCanceled;
                aimDownSightsAction.action.Disable();
            }
        }

        private void HandleAimPerformed(InputAction.CallbackContext context)
        {
            if (!_hasWeapon)
                return;

            _isAiming = true;
            Debug.Log("WeaponController: ADS performed, _isAiming = true");
        }

        private void HandleAimCanceled(InputAction.CallbackContext context)
        {
            _isAiming = false;
            Debug.Log("WeaponController: ADS canceled, _isAiming = false");
        }

        private void FireWeapon(InputAction.CallbackContext context)
        {
            if (!_hasWeapon || _isEquippingWeapon)
                return;

            if (!_isHammerCocked)
            {
                print("can't fire");
               if (weaponAudio != null && dryFireSound != null)
                {
                    weaponAudio.pitch = 1f;
                    weaponAudio.PlayOneShot(dryFireSound);
                }
                return;
            }

            if (!canFireWeapon || _isFiring) return;

            if (_ammoCount <= 0)
            {
                print("out of ammo");
                if (weaponAudio != null && dryFireSound != null)
                    weaponAudio.PlayOneShot(dryFireSound);
                return;
            }

            // Calculate trigger delay based on nerves
            float delay = 0f;
            if (nervesManager != null && maxTriggerDelay > 0f)
            {
                float nervesNormalized = Mathf.Clamp01(nervesManager.currentNerves / 100f);
                // Randomize slightly so it's not a predictable fixed delay
                delay = nervesNormalized * maxTriggerDelay * UnityEngine.Random.Range(0.6f, 1f);
            }

            if (delay > 0.001f)
                StartCoroutine(DelayedFire(delay));
            else
                ExecuteShot();
        }

        private IEnumerator DelayedFire(float delay)
        {
            _isFiring = true;
            yield return new WaitForSeconds(delay);
            ExecuteShot();
            _isFiring = false;
        }

        private void ExecuteShot()
        {
            if (!_hasWeapon || _ammoCount <= 0)
                return;

            _ammoCount--;

            // Fire
            if (gunAnimator != null)
            {
                gunAnimator.SetTrigger(FireTrigger);
            }

            // Create particles
            if (muzzleFlashPrefab != null && gunsmokePrefab != null && muzzlePoint != null && smokeSpawnPoint != null)
            {
                GameObject flash = Instantiate(muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation,
                    muzzlePoint);
                GameObject smoke = Instantiate(gunsmokePrefab, smokeSpawnPoint.position, smokeSpawnPoint.rotation);
                Destroy(flash, 0.5f);
                Destroy(smoke, 5f);
            }

            // Create raycast + shot
            if (weaponAudio != null && fireSound != null)
            {
                weaponAudio.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                weaponAudio.PlayOneShot(fireSound);
            }
            Vector3 shotOrigin = playerCamera.transform.position;
            Vector3 shotDirection = playerCamera.transform.forward;
            bool hitEnemy = false;
            bool hitSomething = false;
            float hitDistance = range;
            RaycastHit hit;

            if (Physics.Raycast(
                    shotOrigin,
                    shotDirection,
                    out hit,
                    range,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                hitSomething = true;
                hitDistance = hit.distance;
                // Debug.Log("Hit: " + hit.transform.name);

                // Check if the object we hit has the EnemyHealth script
                EnemyAI enemy = hit.transform.GetComponentInParent<EnemyAI>();
                if (enemy != null)
                {
                    enemy.Hit(hit.point, shotDirection);
                    hitEnemy = true;
                }
                else{
                    //Check if an environmental object is hit
                    IEnvironmentalObject environmentalObject = hit.transform.GetComponentInChildren<IEnvironmentalObject>();
                    if (environmentalObject != null){
                        environmentalObject.HitByPlayer();
                    }
                }
            }

            OnWeaponShotResolved?.Invoke(new ShotResolutionContext(
                shotOrigin,
                shotDirection,
                range,
                hitEnemy,
                hitSomething,
                hitDistance));
            print("fired");
            OnWeaponFired?.Invoke();
            _isHammerCocked = false;
            canFireWeapon = false;
            if (gunAnimator != null)
                gunAnimator.SetBool(HammerPullBool, _isHammerCocked);
        }

        private void PullHammer(InputAction.CallbackContext context)
        {
            if (!_hasWeapon || _isEquippingWeapon)
                return;

            // Don't allow pulling hammer if it's already cocked or firing
            if (_isHammerCocked || (gunAnimator != null && gunAnimator.GetCurrentAnimatorStateInfo(0).IsName("Fire"))) return;
            // cock hammer
            _isHammerCocked = true;
            if (gunAnimator != null) gunAnimator.SetBool(HammerPullBool, true);
            if (weaponAudio != null && cockingSound != null)
            {
                weaponAudio.pitch = 1f;
                weaponAudio.PlayOneShot(cockingSound);
            }
        }

        public void OnHammerPullFinished() 
            // Apparently this function will get called bc I put an animation event inside HammerPull??
        {
            canFireWeapon = true;
        }

        public void SetHasWeapon(bool hasWeapon)
        {
            bool gainedWeapon = hasWeapon && !_hasWeapon;

            _hasWeapon = hasWeapon;
            if (!_hasWeapon)
            {
                if (_pickupEquipRoutine != null)
                {
                    StopCoroutine(_pickupEquipRoutine);
                    _pickupEquipRoutine = null;
                }

                _isEquippingWeapon = false;
                _isAiming = false;
                _isHammerCocked = false;
                canFireWeapon = false;
                if (gunAnimator != null)
                    gunAnimator.SetBool(HammerPullBool, false);
            }

            ApplyWeaponVisibility();

            if (gainedWeapon && pickupEquipDuration > 0f && viewmodelRoot != null)
                _pickupEquipRoutine = StartCoroutine(EquipWeaponFromPickup());
        }

        public void AddAmmo(int amount)
        {
            _ammoCount += Mathf.Max(0, amount);
        }

        private void ApplyWeaponVisibility()
        {
            if (viewmodelRoot == null)
                return;

            viewmodelRoot.gameObject.SetActive(_hasWeapon);
        }

        private IEnumerator EquipWeaponFromPickup()
        {
            _isEquippingWeapon = true;
            _isAiming = false;

            Vector3 targetLocalPosition = HipFirePosition != null ? HipFirePosition.localPosition : transform.localPosition;
            Quaternion targetLocalRotation = viewmodelRoot.localRotation;
            Vector3 startLocalPosition = targetLocalPosition + pickupStartLocalOffset;
            Quaternion startLocalRotation = targetLocalRotation * Quaternion.Euler(pickupStartLocalEulerOffset);

            transform.localPosition = startLocalPosition;
            viewmodelRoot.localRotation = startLocalRotation;

            float elapsed = 0f;
            while (elapsed < pickupEquipDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pickupEquipDuration);
                float easedT = Mathf.SmoothStep(0f, 1f, t);

                transform.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, easedT);
                viewmodelRoot.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, easedT);

                yield return null;
            }

            transform.localPosition = targetLocalPosition;
            viewmodelRoot.localRotation = targetLocalRotation;
            if (smokeSpawnPoint != null)
                smokeSpawnPoint.localPosition = _originalSmokeLocalPos;

            _isEquippingWeapon = false;
            _pickupEquipRoutine = null;
        }
    }
}
