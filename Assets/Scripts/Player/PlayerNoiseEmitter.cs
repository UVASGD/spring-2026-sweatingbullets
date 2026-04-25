using UnityEngine;
using SUPERCharacter;

namespace Player
{
    public enum NoiseType
    {
        None,
        Footstep,
        Gunshot,
    }

    public class PlayerNoiseEmitter : MonoBehaviour
    {
        [Header("Noise Levels")]
        [SerializeField] private float walkingNoise = 0.3f;
        [SerializeField] private float sprintingNoise = 0.7f;
        [SerializeField] private float gunshotNoise = 1.0f;
        [SerializeField] private float gunshotDecayTime = 1.5f;
        
        private SUPERCharacterAIO _characterController;
        private WeaponController _weaponController;

        private float _currentNoise;
        private float _gunshotTimer;
        private NoiseType _lastNoiseType;

        public float NoiseLevel => _currentNoise;
        public Vector3 Position => transform.position;
        public NoiseType LastNoiseType => _lastNoiseType;

        private void Awake()
        {
            _characterController = GetComponent<SUPERCharacterAIO>();
            _weaponController = GetComponentInChildren<WeaponController>();
        }

        private void OnEnable()
        {
            if (_weaponController != null)
                _weaponController.OnWeaponFired += OnGunshot;
        }

        private void OnDisable()
        {
            if (_weaponController != null)
                _weaponController.OnWeaponFired -= OnGunshot;
        }

        private void Update()
        {
            float movementNoise = 0f;

            if (_characterController != null && !_characterController.isIdle)
            {
                if (_characterController.isSprinting)
                    movementNoise = sprintingNoise;
                else if (_characterController.isCrouching)
                    movementNoise = 0f;
                else
                    movementNoise = walkingNoise;
            }

            // Gunshot noise decays over time
            float gunshotLevel = 0f;
            if (_gunshotTimer > 0f)
            {
                gunshotLevel = gunshotNoise * (_gunshotTimer / gunshotDecayTime);
                _gunshotTimer -= Time.deltaTime;
            }

            // Take the louder of the two
            if (gunshotLevel > movementNoise)
            {
                _currentNoise = gunshotLevel;
                _lastNoiseType = NoiseType.Gunshot;
            }
            else if (movementNoise > 0f)
            {
                _currentNoise = movementNoise;
                _lastNoiseType = NoiseType.Footstep;
            }
            else
            {
                _currentNoise = 0f;
                _lastNoiseType = NoiseType.None;
            }
        }

        private void OnGunshot()
        {
            _gunshotTimer = gunshotDecayTime;
        }
    }
}
