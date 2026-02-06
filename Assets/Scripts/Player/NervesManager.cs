using SUPERCharacter;
using UnityEngine;

namespace Player
{
    public class NervesManager : MonoBehaviour
    {
        [Header("ADS Nerves")]
        [SerializeField] private float adsDelayBeforeIncrease = 3f;
        [SerializeField] private float adsIncreaseRate = 5f;
        [SerializeField] private float adsMaxContribution = 30f;

        [Header("Recovery")]
        [SerializeField] private float standingStillDecreaseRate = 10f;
        [SerializeField] private float crouchingDecreaseRate = 15f;

        [Header("References")]
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private SUPERCharacterAIO characterController;
        [SerializeField] private NervesVisualEffectsController visualController;
        [SerializeField] private NervesAudioController audioController;

        [Header("Debug")]
        [SerializeField] private float currentNerves;
        [SerializeField] private float adsAccumulatedNerves;

        private float _adsTimer;

        private void Awake()
        {
            if (weaponController == null)
                weaponController = GetComponentInChildren<WeaponController>();

            if (characterController == null)
                characterController = GetComponentInParent<SUPERCharacterAIO>();

            if (visualController == null)
                visualController = GetComponentInChildren<NervesVisualEffectsController>();

            if (audioController == null)
                audioController = GetComponentInChildren<NervesAudioController>();
        }

        private void OnEnable()
        {
            if (weaponController != null)
                weaponController.OnWeaponFired += HandleWeaponFired;
        }

        private void OnDisable()
        {
            if (weaponController != null)
                weaponController.OnWeaponFired -= HandleWeaponFired;
        }

        private void Update()
        {
            bool isAiming = weaponController != null && weaponController.IsAiming;

            if (isAiming)
            {
                _adsTimer += Time.deltaTime;

                if (_adsTimer >= adsDelayBeforeIncrease)
                {
                    float delta = adsIncreaseRate * Time.deltaTime;
                    float remaining = Mathf.Max(0f, adsMaxContribution - adsAccumulatedNerves);

                    if (remaining > 0f)
                    {
                        float applied = Mathf.Min(delta, remaining);
                        adsAccumulatedNerves += applied;
                        IncreaseNerves(applied);
                    }
                }
            }
            else
            {
                _adsTimer = 0f;

                bool isCrouching = characterController != null && characterController.isCrouching;
                bool isStandingStill = characterController != null && characterController.isIdle;

                if (isCrouching || isStandingStill)
                {
                    float rate = isCrouching ? crouchingDecreaseRate : standingStillDecreaseRate;
                    DecreaseNerves(rate * Time.deltaTime);
                }
            }

            if (visualController != null)
                visualController.SetNervesLevel(currentNerves);

            if (audioController != null)
                audioController.SetNervesLevel(currentNerves);
        }

        private void HandleWeaponFired()
        {
            if (weaponController != null && weaponController.IsAiming)
                _adsTimer = 0f;
        }

        private void IncreaseNerves(float amount)
        {
            currentNerves = Mathf.Clamp(currentNerves + amount, 0f, 100f);
        }

        private void DecreaseNerves(float amount)
        {
            float before = currentNerves;
            currentNerves = Mathf.Clamp(currentNerves - amount, 0f, 100f);

            float actualDecrease = before - currentNerves;
            adsAccumulatedNerves = Mathf.Max(0f, adsAccumulatedNerves - actualDecrease);
        }
    }
}
