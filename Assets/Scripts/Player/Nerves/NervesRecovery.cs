using SUPERCharacter;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Handles nerves recovery (decrease) when the player is standing still or crouching
    /// while not aiming. Returns a positive value representing how much nerves should decrease.
    /// </summary>
    public class NervesRecovery : MonoBehaviour
    {
        [Header("Recovery Rates")]
        [SerializeField] private float standingStillDecreaseRate = 12f;
        [SerializeField] private float crouchingDecreaseRate = 18f;
        [Tooltip("Slow recovery while moving (not idle, not crouched, not sprinting, not aiming).")]
        [SerializeField] private float walkingDecreaseRate = 4f;

        [Header("Recovery Delay")]
        [SerializeField] private float recoveryDelay = 1.0f;

        [Header("References")]
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private SUPERCharacterAIO characterController;

        private float _recoveryTimer;

        private void Awake()
        {
            if (weaponController == null)
                weaponController = GetComponentInChildren<WeaponController>();

            if (characterController == null)
                characterController = GetComponentInParent<SUPERCharacterAIO>();

            if (weaponController == null)
                Debug.LogWarning("NervesRecovery: Could not find WeaponController in parent hierarchy — aiming check will be skipped.");
            if (characterController == null)
                Debug.LogError("NervesRecovery: Could not find SUPERCharacterAIO in parent hierarchy — recovery will never trigger!");
        }

        /// <summary>
        /// Returns the amount of nerves to decrease this frame. Returns 0 if conditions aren't met.
        /// </summary>
        public float Evaluate()
        {
            if (weaponController != null && weaponController.IsAiming)
            {
                _recoveryTimer = 0f;
                return 0f;
            }

            if (characterController == null)
                return 0f;

            if (characterController.isSprinting)
            {
                _recoveryTimer = 0f;
                return 0f;
            }

            _recoveryTimer += Time.deltaTime;
            if (_recoveryTimer < recoveryDelay)
                return 0f;

            if (characterController.isCrouching)
                return crouchingDecreaseRate * Time.deltaTime;

            if (characterController.isIdle)
                return standingStillDecreaseRate * Time.deltaTime;

            return walkingDecreaseRate * Time.deltaTime;
        }
    }
}
