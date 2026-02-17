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
        [SerializeField] private float standingStillDecreaseRate = 10f;
        [SerializeField] private float crouchingDecreaseRate = 15f;

        [Header("Recovery Delay")]
        [SerializeField] private float recoveryDelay = 1.5f;

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
                Debug.Log("NervesRecovery!!!!!!!!!!!!!!!!!!!!!!: Blocked — player is aiming.");
                return 0f;
            }

            Debug.Log("AJKHBFKJABDFKJSBDF<KSJBDFS<KFJBS<KFJBSKDF");

            if (characterController == null)
                return 0f;

            bool canRecover = characterController.isCrouching || characterController.isIdle;

            if (!canRecover)
            {
                _recoveryTimer = 0f;
                return 0f;
            }

            _recoveryTimer += Time.deltaTime;
            if (_recoveryTimer < recoveryDelay)
                return 0f;

            if (characterController.isCrouching)
                return crouchingDecreaseRate * Time.deltaTime;

            return standingStillDecreaseRate * Time.deltaTime;
        }
    }
}
