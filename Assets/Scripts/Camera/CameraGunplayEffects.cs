using UnityEngine;
using Player;

/// <summary>
/// Impulse-based camera and gun feedback for aiming and shooting:
/// - FOV shift when ADS-ing
/// - Multi-phase recoil with per-shot variation and micro-shake
/// - Subtle procedural sway while aiming
/// - Separate (exaggerated) gun rotation recoil
/// </summary>
public class CameraGunplayEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private Camera playerCamera;
    [Tooltip("The gun's root transform (for rotational recoil kick)")]
    [SerializeField] private Transform gunTransform;
    [Tooltip("Optional — if assigned, higher nerves = more recoil variation")]
    [SerializeField] private NervesManager nervesManager;

    [Header("ADS FOV")]
    [Range(30f, 90f)]
    [SerializeField] private float adsFOV = 55f;
    [Range(1f, 30f)]
    [SerializeField] private float fovLerpSpeed = 10f;

    [Header("Camera Recoil")]
    [Tooltip("Base upward pitch in degrees")]
    [Range(0f, 10f)]
    [SerializeField] private float recoilPitch = 2.5f;
    [Tooltip("Per-shot pitch variation (±%)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float pitchVariation = 0.15f;
    [Tooltip("Rightward yaw bias (positive = right-handed shooter)")]
    [Range(-2f, 2f)]
    [SerializeField] private float recoilYawBias = 0.3f;
    [Tooltip("Random yaw spread on top of bias")]
    [Range(0f, 3f)]
    [SerializeField] private float recoilYawRandom = 0.5f;

    [Header("Recoil Timing")]
    [Tooltip("Duration of the initial snap phase (seconds)")]
    [Range(0.02f, 0.2f)]
    [SerializeField] private float snapDuration = 0.07f;
    [Tooltip("Duration of the overcorrect phase")]
    [Range(0.02f, 0.3f)]
    [SerializeField] private float overcorrectDuration = 0.12f;
    [Tooltip("How far past center the overcorrect goes (fraction of kick)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float overcorrectFraction = 0.15f;
    [Tooltip("Duration of the final settle phase")]
    [Range(0.05f, 1f)]
    [SerializeField] private float settleDuration = 0.3f;

    [Header("Recoil Micro-Shake")]
    [Tooltip("Intensity of high-frequency shake during snap phase (degrees)")]
    [Range(0f, 3f)]
    [SerializeField] private float microShakeIntensity = 0.6f;
    [Tooltip("Frequency of micro-shake oscillation")]
    [Range(10f, 120f)]
    [SerializeField] private float microShakeFrequency = 60f;

    [Header("Gun Recoil")]
    [Tooltip("Multiplier for gun rotation vs camera (gun kicks harder)")]
    [Range(0f, 5f)]
    [SerializeField] private float gunRecoilMultiplier = 2.5f;
    [Tooltip("Backward pitch of the gun (wrist absorbing kick)")]
    [Range(0f, 15f)]
    [SerializeField] private float gunKickbackPitch = 4f;

    [Header("ADS Sway")]
    [Range(0f, 2f)]
    [SerializeField] private float adsSwayAmount = 0.3f;
    [Tooltip("Max sway multiplier at 100 nerves (1 = no extra sway)")]
    [Range(1f, 5f)]
    [SerializeField] private float adsSwayNervesMultiplier = 3f;
    [Range(0.1f, 5f)]
    [SerializeField] private float adsSwaySpeed = 1.5f;

    private float _defaultFOV;
    private float _swayTime;

    // Camera recoil state
    private Vector3 _lastCameraOffset;

    // Per-shot recoil instance
    private bool _recoilActive;
    private float _recoilTimer;
    private float _recoilTotalDuration;
    private Vector3 _shotKick;        // the full kick for this shot
    private Vector3 _shotOvercorrect;  // overcorrect target
    private float _shotSnapDur;
    private float _shotOvercorrectDur;
    private float _shotSettleDur;
    private float _shotMicroShake;

    // Gun recoil state
    private Vector3 _lastGunOffset;
    private Vector3 _shotGunKick;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponent<Camera>();
        _defaultFOV = playerCamera.fieldOfView;
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

    private void LateUpdate()
    {
        bool aiming = weaponController != null && weaponController.IsAiming;

        // Remove last frame's offsets
        transform.localEulerAngles -= _lastCameraOffset;
        if (gunTransform != null)
            gunTransform.localEulerAngles -= _lastGunOffset;

        UpdateFOV(aiming);
        Vector3 recoilOffset = UpdateRecoil();
        Vector3 gunRecoilOffset = UpdateGunRecoil();
        Vector3 swayOffset = UpdateADSSway(aiming);

        // Apply combined camera offset
        _lastCameraOffset = recoilOffset + swayOffset;
        transform.localEulerAngles += _lastCameraOffset;

        // Apply gun offset
        _lastGunOffset = gunRecoilOffset;
        if (gunTransform != null)
            gunTransform.localEulerAngles += _lastGunOffset;
    }

    private void HandleWeaponFired()
    {
        // Nerves add variation: 0 nerves = base variation, 100 nerves = double variation
        float nervesNormalized = 0f;
        if (nervesManager != null)
            nervesNormalized = Mathf.Clamp01(nervesManager.currentNerves / 100f);
        float variationScale = 1f + nervesNormalized;

        // Per-shot randomized pitch
        float pitchThisShot = recoilPitch * (1f + Random.Range(-pitchVariation, pitchVariation) * variationScale);

        // Asymmetric yaw: biased right with random spread, more erratic with nerves
        float yawThisShot = recoilYawBias + Random.Range(-recoilYawRandom, recoilYawRandom) * variationScale;

        // Small random roll for that organic torque feel
        float rollThisShot = Random.Range(-0.3f, 0.3f) * variationScale;

        _shotKick = new Vector3(-pitchThisShot, yawThisShot, rollThisShot);
        _shotOvercorrect = -_shotKick * overcorrectFraction;

        // Slightly randomize timing too
        _shotSnapDur = snapDuration * Random.Range(0.85f, 1.15f);
        _shotOvercorrectDur = overcorrectDuration * Random.Range(0.9f, 1.1f);
        _shotSettleDur = settleDuration * Random.Range(0.9f, 1.1f);
        _recoilTotalDuration = _shotSnapDur + _shotOvercorrectDur + _shotSettleDur;

        _shotMicroShake = microShakeIntensity * (1f + nervesNormalized * 0.5f);

        // Gun gets an exaggerated version plus backward pitch
        _shotGunKick = _shotKick * gunRecoilMultiplier + new Vector3(-gunKickbackPitch, 0f, 0f);

        _recoilTimer = 0f;
        _recoilActive = true;
    }

    private void UpdateFOV(bool aiming)
    {
        float targetFOV = aiming ? adsFOV : _defaultFOV;
        float newFOV = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, fovLerpSpeed * Time.deltaTime);
        playerCamera.fieldOfView = Mathf.Min(newFOV, _defaultFOV);
    }

    private Vector3 UpdateRecoil()
    {
        if (!_recoilActive)
            return Vector3.zero;

        _recoilTimer += Time.deltaTime;

        if (_recoilTimer >= _recoilTotalDuration)
        {
            _recoilActive = false;
            return Vector3.zero;
        }

        Vector3 offset;

        if (_recoilTimer < _shotSnapDur)
        {
            // Phase 1: Snap — fast kick with micro-shake
            float t = _recoilTimer / _shotSnapDur;
            float easeOut = 1f - (1f - t) * (1f - t); // fast start, decelerate
            offset = _shotKick * easeOut;

            // Micro-shake: high-frequency jitter that fades as snap completes
            float shakeFade = 1f - t;
            float shakeX = Mathf.Sin(_recoilTimer * microShakeFrequency) * _shotMicroShake * shakeFade;
            float shakeY = Mathf.Sin(_recoilTimer * microShakeFrequency * 1.3f + 1f) * _shotMicroShake * 0.7f * shakeFade;
            offset += new Vector3(shakeX, shakeY, 0f);
        }
        else if (_recoilTimer < _shotSnapDur + _shotOvercorrectDur)
        {
            // Phase 2: Overcorrect — slide past center
            float t = (_recoilTimer - _shotSnapDur) / _shotOvercorrectDur;
            float easeInOut = t * t * (3f - 2f * t); // smoothstep
            offset = Vector3.Lerp(_shotKick, _shotOvercorrect, easeInOut);
        }
        else
        {
            // Phase 3: Settle — ease back to zero
            float t = (_recoilTimer - _shotSnapDur - _shotOvercorrectDur) / _shotSettleDur;
            float easeOut = 1f - (1f - t) * (1f - t);
            offset = Vector3.Lerp(_shotOvercorrect, Vector3.zero, easeOut);
        }

        return offset;
    }

    private Vector3 UpdateGunRecoil()
    {
        if (!_recoilActive || gunTransform == null)
            return Vector3.zero;

        // Gun follows the same phase timing but with exaggerated values
        // and recovers slightly faster for visual snap
        float gunTimer = _recoilTimer * 1.1f; // gun recovers a touch faster than camera

        if (gunTimer >= _recoilTotalDuration)
            return Vector3.zero;

        Vector3 offset;

        if (gunTimer < _shotSnapDur)
        {
            float t = gunTimer / _shotSnapDur;
            float easeOut = 1f - (1f - t) * (1f - t);
            offset = _shotGunKick * easeOut;
        }
        else if (gunTimer < _shotSnapDur + _shotOvercorrectDur)
        {
            float t = (gunTimer - _shotSnapDur) / _shotOvercorrectDur;
            float easeInOut = t * t * (3f - 2f * t);
            Vector3 gunOvercorrect = -_shotGunKick * overcorrectFraction;
            offset = Vector3.Lerp(_shotGunKick, gunOvercorrect, easeInOut);
        }
        else
        {
            float t = (gunTimer - _shotSnapDur - _shotOvercorrectDur) / _shotSettleDur;
            float easeOut = 1f - (1f - t) * (1f - t);
            Vector3 gunOvercorrect = -_shotGunKick * overcorrectFraction;
            offset = Vector3.Lerp(gunOvercorrect, Vector3.zero, easeOut);
        }

        return offset;
    }

    private Vector3 UpdateADSSway(bool aiming)
    {
        if (!aiming)
        {
            _swayTime = 0f;
            return Vector3.zero;
        }

        float nervesNormalized = 0f;
        if (nervesManager != null)
            nervesNormalized = Mathf.Clamp01(nervesManager.currentNerves / 100f);
        float nervesScale = Mathf.Lerp(1f, adsSwayNervesMultiplier, nervesNormalized);
        float sway = adsSwayAmount * nervesScale;

        _swayTime += Time.deltaTime * adsSwaySpeed;

        float swayX = Mathf.Sin(_swayTime * 1.0f) * sway;
        float swayY = Mathf.Sin(_swayTime * 0.7f + 0.5f) * sway * 0.6f;

        return new Vector3(swayX, swayY, 0f);
    }
}