using System;
using NUnit.Framework.Constraints;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Enemy;

namespace Player
{
    public class WeaponController : MonoBehaviour
    {
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

        private GameObject
            _regularSmokeSpawnPoint; // I have this instead of a separate gameobject so that smoke spawn will always be dependent on the aim position and hip fire position

        [Header("Audio")] public AudioClip dryFireSound;
        public AudioClip cockingSound;
        public AudioClip fireSound;

        public float aimSpeed = 25f;
        public float range = 100f;
        private bool _isHammerCocked = false;

        public bool
            canFireWeapon =
                false; // modified by event OnStateExit() in the HammerPull state in the Animator for the player's gun. Script called "Pulled.cs"

        private void Start()
        {
            // Making the smoke look like it's coming from the viewmodel when aiming
            _hipToAimZOffset =
                Math.Abs(
                    HipFirePosition.localPosition.z - aimPosition.localPosition.z -
                    0.5f /*included some offset for customization*/);
            _regularSmokeSpawnPoint = new GameObject();
            _regularSmokeSpawnPoint.transform.SetParent(transform);
            _regularSmokeSpawnPoint.transform.SetPositionAndRotation(smokeSpawnPoint.position,
                smokeSpawnPoint.rotation); // caching smoke spawn in hip fire position

        }

        private void Update()
        {
            // aiming button held
            if (_isAiming && transform.position != aimPosition.position)
            {
                transform.position = Vector3.MoveTowards(transform.position, aimPosition.position,
                    aimSpeed * Time.deltaTime);
                // why is localposition used here and regular position is used in the next if statement? Don't ask me. Because it works that way. lol
                smokeSpawnPoint.localPosition = Vector3.MoveTowards(smokeSpawnPoint.localPosition,
                    aimPosition.localPosition - new Vector3(0, 0, _hipToAimZOffset), aimSpeed * Time.deltaTime);
            }

            // aiming button let go
            if (!_isAiming && transform.position != HipFirePosition.position)
            {
                transform.position = Vector3.MoveTowards(transform.position, HipFirePosition.position,
                    aimSpeed * Time.deltaTime);
                smokeSpawnPoint.position = Vector3.MoveTowards(smokeSpawnPoint.position,
                    _regularSmokeSpawnPoint.transform.position, aimSpeed * Time.deltaTime);
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
                aimDownSightsAction.action.performed += OnAimPerformed;
                aimDownSightsAction.action.canceled += OnAimCanceled;
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
                aimDownSightsAction.action.performed -= OnAimPerformed;
                aimDownSightsAction.action.canceled -= OnAimCanceled;
                aimDownSightsAction.action.Disable();
            }
        }

        private void OnAimPerformed(InputAction.CallbackContext context)
        {
            _isAiming = true;
        }

        private void OnAimCanceled(InputAction.CallbackContext context)
        {
            _isAiming = false;
        }

        private void FireWeapon(InputAction.CallbackContext context)
        {
            if (!_isHammerCocked)
            {
                print("can't fire");
                if (weaponAudio != null && dryFireSound != null) // play click sound when dry firing
                    weaponAudio.PlayOneShot(dryFireSound);
                return;
            }

            if (!canFireWeapon) return;

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
            RaycastHit hit;

            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, range))
            {
                Debug.Log("Hit: " + hit.transform.name);

                // Check if the object we hit has the EnemyHealth script
                EnemyAI enemy = hit.transform.GetComponent<EnemyAI>();
                if (enemy != null)
                {
                    enemy.Hit(hit.point, playerCamera.transform.forward);
                }
            }

            print("fired");
            OnWeaponFired?.Invoke();
            _isHammerCocked = false;
            canFireWeapon = false;
            gunAnimator.SetBool(HammerPullBool, _isHammerCocked);
        }

        private void PullHammer(InputAction.CallbackContext context)
        {
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
    }
}