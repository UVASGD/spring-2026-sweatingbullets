using UnityEngine;

namespace Player
{
    /// <summary>
    /// Increases nerves when the player aims down sights without firing for too long.
    /// Resets its timer when the player fires or stops aiming.
    /// </summary>
    public class ADSNervesInput : NervesInput
    {
        [Header("ADS Settings")]
        [SerializeField] private float delayBeforeIncrease = 0.5f;
        [SerializeField] private float increaseRate = 3.5f;
        [SerializeField] private WeaponController weaponController;

        private float _adsTimer;

        private void Reset()
        {
            maxContribution = 20f;
        }

        private void Awake()
        {
            ResolveWeaponControllerReference();
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

        protected override float CalculateNervesDelta()
        {
            if (weaponController == null)
            {
                Debug.LogWarning("ADSNervesInput: WeaponController reference is null");
                return 0f;
            }

            if (!weaponController.IsAiming)
            {
                ResetAdsTimerIfNeeded("ADSNervesInput: Stopped aiming, resetting timer");
                return 0f;
            }

            _adsTimer += Time.deltaTime;

            if (_adsTimer < delayBeforeIncrease)
            {
                //Debug.Log($"ADSNervesInput: Aiming but timer not reached threshold ({_adsTimer:F2}/{delayBeforeIncrease})");
                return 0f;
            }

            float delta = increaseRate * Time.deltaTime;
            return delta;
        }

        private void HandleWeaponFired()
        {
            _adsTimer = 0f;
        }

        private void ResolveWeaponControllerReference()
        {
            if (weaponController != null)
            {
                Debug.Log("ADSNervesInput: WeaponController reference already assigned in inspector");
                return;
            }

            weaponController = GetComponentInParent<WeaponController>();
            if (weaponController != null)
                Debug.Log("ADSNervesInput: Found WeaponController via GetComponentInParent");
            else
                Debug.LogError("ADSNervesInput: Could not find WeaponController in parent hierarchy");
        }

        private void ResetAdsTimerIfNeeded(string reason)
        {
            if (_adsTimer > 0f)
                Debug.Log(reason);

            _adsTimer = 0f;
        }
    }
}
