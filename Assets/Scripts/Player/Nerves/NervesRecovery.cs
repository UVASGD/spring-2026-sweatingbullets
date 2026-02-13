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

        [Header("References")]
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private SUPERCharacterAIO characterController;

        private void Awake()
        {
            if (weaponController == null)
                weaponController = GetComponentInParent<WeaponController>();

            if (characterController == null)
                characterController = GetComponentInParent<SUPERCharacterAIO>();
        }

        /// <summary>
        /// Returns the amount of nerves to decrease this frame. Returns 0 if conditions aren't met.
        /// </summary>
        public float Evaluate()
        {
            if (weaponController != null && weaponController.IsAiming)
                return 0f;

            if (characterController == null)
                return 0f;

            if (characterController.isCrouching)
                return crouchingDecreaseRate * Time.deltaTime;

            if (characterController.isIdle)
                return standingStillDecreaseRate * Time.deltaTime;

            return 0f;
        }
    }
}
