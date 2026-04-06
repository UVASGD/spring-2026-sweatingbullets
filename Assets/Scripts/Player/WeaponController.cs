using System;
using System.Collections;
using NUnit.Framework.Constraints;
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

        private const string FireTrigger = "Fire";
        private const string HammerPullBool = "HammerPull";
        private bool _isAiming = false;
        private float _hipToAimZOffset;

        public bool IsAiming => _isAiming;

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
        private PlayerController _playerController;
        private bool _isFiring = false;

        public bool
            canFireWeapon =
                false; // modified by event OnStateExit() in the HammerPull state in the Animator for the player's gun. Script called "Pulled.cs"

        private void Awake()
        {
            _playerController = GetComponentInParent<PlayerController>();
        }

        private void Start()
        {
            if (_playerController == null)
            {
                _playerController = GetComponentInParent<PlayerController>();
            }

            // Making the smoke look like it's coming from the viewmodel when aiming
            _hipToAimZOffset =
                Math.Abs(
                    HipFirePosition.localPosition.z - aimPosition.localPosition.z -
                    0.5f /*included some offset for customization*/);
            _originalSmokeLocalPos = smokeSpawnPoint.localPosition; // cache hip-fire local position

        }

        private void Update()
        {
            // aiming button held
            if (_isAiming && transform.localPosition != aimPosition.localPosition)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, aimPosition.localPosition,
                    aimSpeed * Time.deltaTime);
                // why is localposition used here and regular position is used in the next if statement? Don't ask me. Because it works that way. lol
                smokeSpawnPoint.localPosition = Vector3.MoveTowards(smokeSpawnPoint.localPosition,
                    aimPosition.localPosition - new Vector3(0, 0, _hipToAimZOffset), aimSpeed * Time.deltaTime);
            }

            // aiming button let go
            if (!_isAiming && transform.localPosition != HipFirePosition.localPosition)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, HipFirePosition.localPosition,
                    aimSpeed * Time.deltaTime);
                smokeSpawnPoint.localPosition = _originalSmokeLocalPos;
            }
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
                aimDownSightsAction.action.performed +=
                    ctx => { _isAiming = true; Debug.Log("WeaponController: ADS performed, _isAiming = true"); };
                aimDownSightsAction.action.canceled +=
                    ctx => { _isAiming = false; Debug.Log("WeaponController: ADS canceled, _isAiming = false"); };
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
                aimDownSightsAction.action.Disable();
            }
        }



        private void FireWeapon(InputAction.CallbackContext context)
        {
            if (!PlayerHasGun())
            {
                return;
            }

            if (!_isHammerCocked)
            {
                PlayDryFire();
                return;
            }

            if (!canFireWeapon) return;
            if (_playerController == null || !_playerController.TryConsumeBullet())
            {
                PlayDryFire();
                ResetHammerState();
                return;
            }
            if (!canFireWeapon || _isFiring) return;

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
            // Fire
            if (gunAnimator != null)
            {
                gunAnimator.SetTrigger(FireTrigger);
            }

            // Create particles
            if (muzzleFlashPrefab != null && gunsmokePrefab != null && muzzlePoint != null)
            {
                GameObject flash = Instantiate(muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation,
                    muzzlePoint);
                GameObject smoke = Instantiate(gunsmokePrefab, smokeSpawnPoint.position, smokeSpawnPoint.rotation);
                Destroy(flash, 0.5f);
                Destroy(smoke, 5f);
            }

            // Create raycast + shot
            if (weaponAudio != null && fireSound != null) weaponAudio.PlayOneShot(fireSound);
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
            ResetHammerState();
        }

        private void PullHammer(InputAction.CallbackContext context)
        {
            if (!PlayerHasGun() || gunAnimator == null)
            {
                return;
            }

            // Don't allow pulling hammer if it's already cocked or firing
            if (_isHammerCocked || gunAnimator.GetCurrentAnimatorStateInfo(0).IsName("Fire")) return;
            // cock hammer
            _isHammerCocked = true;
            if (gunAnimator != null) gunAnimator.SetBool(HammerPullBool, true);
            if (weaponAudio != null && cockingSound != null) weaponAudio.PlayOneShot(cockingSound);
        }

        public void OnHammerPullFinished() 
            // Apparently this function will get called bc I put an animation event inside HammerPull??
        {
            canFireWeapon = true;
        }

        private bool PlayerHasGun()
        {
            return _playerController != null && _playerController.HasGun;
        }

        private void PlayDryFire()
        {
            if (weaponAudio != null && dryFireSound != null)
            {
                weaponAudio.PlayOneShot(dryFireSound);
            }
        }

        private void ResetHammerState()
        {
            _isHammerCocked = false;
            canFireWeapon = false;

            if (gunAnimator != null)
            {
                gunAnimator.SetBool(HammerPullBool, false);
            }
        }
    }
}
