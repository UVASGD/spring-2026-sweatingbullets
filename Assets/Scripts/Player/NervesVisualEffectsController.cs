using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;

namespace Player
{
    /// <summary>
    /// Controls nerves-based visual effects: vignette, color tint, desaturation, chromatic aberration, and lens distortion.
    /// Press 'N' to trigger demo mode (3s ramp up, 2.5s hold, 5s fade out).
    /// </summary>
    public class NervesVisualEffectsController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Volume postProcessVolume;

        [Header("Effect Intensities")]
        [SerializeField] private float maxVignetteIntensity = 0.95f;
        [SerializeField] private float maxDesaturation = -50f;
        [SerializeField] private Color maxNervesTint = new Color(1f, 0.5f, 0.5f, 1f);
        [SerializeField] private float maxChromaticAberration = 0.5f;
        [SerializeField] private float maxLensDistortion = -0.2f;

        [Header("Timing")]
        [SerializeField] private float demoIntensifyDuration = 3f;
        [SerializeField] private float demoFadeDuration = 5f;
        [SerializeField] private float transitionSpeed = 2f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private string debugToggleKeyName = "n";
        [SerializeField] private bool isDemoActive = false;

        [Header("Intensity Curves")]
        [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Vignette _vignette;
        private ColorAdjustments _colorAdjustments;
        private ChromaticAberration _chromaticAberration;
        private LensDistortion _lensDistortion;
        private float _currentNervesLevel = 0f;
        private float _visualNervesLevel = 0f;
        private Coroutine _demoCoroutine;

        private void Start()
        {
            // Ensure we have a smooth ease-in-out curve instead of linear
            if (intensityCurve.length == 2 && intensityCurve[0].value == 0 && intensityCurve[1].value == 1)
                intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

            // Find the Volume component (fallback to search if not assigned)
            if (postProcessVolume == null)
                postProcessVolume = GetComponent<Volume>() ?? FindFirstObjectByType<Volume>();

            // Get all required post-processing overrides from the volume profile
            if (postProcessVolume != null)
            {
                postProcessVolume.isGlobal = true; // Apply effects to entire camera
                postProcessVolume.profile.TryGet(out _vignette);
                postProcessVolume.profile.TryGet(out _colorAdjustments);
                postProcessVolume.profile.TryGet(out _chromaticAberration);
                postProcessVolume.profile.TryGet(out _lensDistortion);

                // Warn if any required effects are missing from the profile
                if (_vignette == null) Debug.LogWarning("Vignette not found in profile.");
                if (_colorAdjustments == null) Debug.LogWarning("ColorAdjustments not found in profile.");
                if (_chromaticAberration == null) Debug.LogWarning("ChromaticAberration not found in profile.");
                if (_lensDistortion == null) Debug.LogWarning("LensDistortion not found in profile.");
            }
            else
            {
                Debug.LogError("No Volume component assigned.");
            }

            // Initialize with clean state (no effects visible)
            UpdateVisuals(true);
        }

        private void Update()
        {
            HandleDebugInput();
            // Only smooth transitions when not in demo mode (demo controls _visualNervesLevel directly)
            if (!isDemoActive) SmoothVisuals();
        }

        /// <summary>
        /// Main API: Sets the nerves level (0-100) for visual effects.
        /// </summary>
        public void SetNervesLevel(float level)
        {
            if (isDemoActive) return; // Don't interfere with demo sequence
            _currentNervesLevel = Mathf.Clamp(level, 0f, 100f);
        }

        private void SmoothVisuals()
        {
            // Smoothly move visual level toward target level using configured speed
            if (Mathf.Approximately(_visualNervesLevel, _currentNervesLevel)) return;
            _visualNervesLevel = Mathf.MoveTowards(_visualNervesLevel, _currentNervesLevel, transitionSpeed * 100f * Time.deltaTime);
            UpdateVisuals();
        }

        private void UpdateVisuals(bool force = false)
        {
            // Convert nerves level (0-100) to normalized (0-1) and apply curve for smooth transitions
            float rawNormalizedNerves = _visualNervesLevel / 100f;
            float normalizedNerves = intensityCurve.Evaluate(rawNormalizedNerves); // KEY: Prevents snapping at low levels

            // Apply all visual effects using the curved intensity value
            if (_vignette != null)
            {
                _vignette.active = true;
                _vignette.intensity.overrideState = true;
                _vignette.intensity.value = normalizedNerves * maxVignetteIntensity;
            }

            if (_colorAdjustments != null)
            {
                _colorAdjustments.active = true;
                _colorAdjustments.saturation.overrideState = true;
                _colorAdjustments.saturation.value = normalizedNerves * maxDesaturation;
                _colorAdjustments.colorFilter.overrideState = true;
                _colorAdjustments.colorFilter.value = Color.Lerp(Color.white, maxNervesTint, normalizedNerves);
            }

            if (_chromaticAberration != null)
            {
                _chromaticAberration.active = true;
                _chromaticAberration.intensity.overrideState = true;
                _chromaticAberration.intensity.value = normalizedNerves * maxChromaticAberration;
            }

            if (_lensDistortion != null)
            {
                _lensDistortion.active = true;
                _lensDistortion.intensity.overrideState = true;
                _lensDistortion.intensity.value = normalizedNerves * maxLensDistortion;
            }

            // Keep volume active with high priority to maintain our "neutral" values when nerves = 0
            if (postProcessVolume != null)
            {
                if (!postProcessVolume.enabled) postProcessVolume.enabled = true;
                postProcessVolume.priority = 999;
            }

            // Debug logging at extreme states for troubleshooting
            if (showDebugInfo && (normalizedNerves > 0.99f || normalizedNerves < 0.01f))
            {
                string state = normalizedNerves < 0.01f ? "IDLE" : "MAX";
                Debug.Log($"[Nerves] {state} | Level: {_visualNervesLevel:F4} | Vig: {_vignette.intensity.value:F2}");
            }
        }

        private void HandleDebugInput()
        {
            if (Keyboard.current == null) return;
            var key = Keyboard.current[debugToggleKeyName] as UnityEngine.InputSystem.Controls.KeyControl;
            if (key != null && key.wasPressedThisFrame)
            {
                if (!isDemoActive) StartDemo();
                else StopDemo();
            }
        }

        private void StartDemo()
        {
            isDemoActive = true;
            if (showDebugInfo) Debug.Log("Demo: STARTED");
            if (_demoCoroutine != null) StopCoroutine(_demoCoroutine);
            _demoCoroutine = StartCoroutine(DemoSequence());
        }

        private void StopDemo()
        {
            isDemoActive = false;
            if (showDebugInfo) Debug.Log("Demo: STOPPED");
            if (_demoCoroutine != null)
            {
                StopCoroutine(_demoCoroutine);
                _demoCoroutine = null;
            }
            _currentNervesLevel = 0f;
        }

        private System.Collections.IEnumerator DemoSequence()
        {
            // PHASE 1: Ramp up to max intensity with ease-in curve
            float elapsed = 0f;
            float startLevel = _visualNervesLevel;

            while (elapsed < demoIntensifyDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / demoIntensifyDuration;
                float smoothT = t * t; // Ease-in for gradual start
                _visualNervesLevel = Mathf.Lerp(startLevel, 100f, smoothT);
                UpdateVisuals();
                yield return null;
            }

            // Ensure we hit exactly 100% before holding
            _visualNervesLevel = 100f;
            UpdateVisuals();
            
            // PHASE 2: Hold at peak intensity for dramatic effect
            yield return new WaitForSeconds(2.5f);

            // PHASE 3: Fade out with ease-out curve for smooth return to normal
            elapsed = 0f;
            startLevel = 100f;

            while (elapsed < demoFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / demoFadeDuration;
                float smoothT = 1f - (1f - t) * (1f - t); // Ease-out for gentle finish
                _visualNervesLevel = Mathf.Lerp(startLevel, 0f, smoothT);
                UpdateVisuals();
                yield return null;
            }

            // Ensure we hit exactly 0% and clean up
            _visualNervesLevel = 0f;
            UpdateVisuals();

            isDemoActive = false;
            _demoCoroutine = null;
            if (showDebugInfo) Debug.Log("Demo: COMPLETED");
        }

        private void OnGUI()
        {
            if (showDebugInfo)
            {
                GUILayout.BeginArea(new Rect(10, 10, 350, 180));
                GUILayout.Label($"Nerves: {_currentNervesLevel:F1}");
                GUILayout.Label($"Visual: {_visualNervesLevel:F1}");
                GUILayout.Label($"Vignette: {(_vignette != null ? _vignette.intensity.value : 0f):F2}");
                GUILayout.Label($"Saturation: {(_colorAdjustments != null ? _colorAdjustments.saturation.value : 0f):F1}");
                GUILayout.Label($"Aberration: {(_chromaticAberration != null ? _chromaticAberration.intensity.value : 0f):F2}");
                GUILayout.Label($"Distortion: {(_lensDistortion != null ? _lensDistortion.intensity.value : 0f):F2}");
                GUILayout.Label($"Demo (Press '{debugToggleKeyName}'): {(isDemoActive ? "ACTIVE" : "OFF")}");
                GUILayout.EndArea();
            }
        }
    }
}
