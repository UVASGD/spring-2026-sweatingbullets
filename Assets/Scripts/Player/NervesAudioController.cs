using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    /// <summary>
    /// Controls nerves-based audio effects: breathing/heartbeat that scales with nerves level.
    /// Press 'N' to trigger demo mode (3s ramp up, 2.5s hold, 5s fade out).
    /// </summary>
    public class NervesAudioController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource audioSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip nervesAudioClip;

        [Header("Audio Settings")]
        [SerializeField] private float maxVolume = 1f;
        [SerializeField] private float minPitch = 0.8f;
        [SerializeField] private float maxPitch = 1.5f;

        [Header("Timing")]
        [SerializeField] private float demoIntensifyDuration = 4f;
        [SerializeField] private float demoFadeDuration = 5f;
        [SerializeField] private float transitionSpeed = 2f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private string debugToggleKeyName = "n";
        [SerializeField] private bool isDemoActive = false;

        [Header("Intensity Curves")]
        [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private float _currentNervesLevel = 0f;
        private float _visualNervesLevel = 0f;
        private Coroutine _demoCoroutine;

        private void Start()
        {
            // Ensure we have a smooth ease-in-out curve instead of linear
            if (intensityCurve.length == 2 && intensityCurve[0].value == 0 && intensityCurve[1].value == 1)
                intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

            // Find or create AudioSource if not assigned
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                Debug.LogWarning("No AudioSource assigned, created one automatically.");
            }

            // Configure AudioSource for looping
            if (nervesAudioClip != null)
            {
                audioSource.clip = nervesAudioClip;
                audioSource.loop = true;
                audioSource.playOnAwake = false;
            }
            else
            {
                Debug.LogWarning("No nervesAudioClip assigned.");
            }

            // Initialize with clean state (silent)
            UpdateAudio(true);
        }

        private void Update()
        {
            HandleDebugInput();
            // Only smooth transitions when not in demo mode (demo controls _visualNervesLevel directly)
            if (!isDemoActive) SmoothAudio();
        }

        /// <summary>
        /// Main API: Sets the nerves level (0-100) for audio effects.
        /// </summary>
        public void SetNervesLevel(float level)
        {
            if (isDemoActive) return; // Don't interfere with demo sequence
            _currentNervesLevel = Mathf.Clamp(level, 0f, 100f);
        }

        private void SmoothAudio()
        {
            // Smoothly move visual level toward target level using configured speed
            if (Mathf.Approximately(_visualNervesLevel, _currentNervesLevel)) return;
            _visualNervesLevel = Mathf.MoveTowards(_visualNervesLevel, _currentNervesLevel, transitionSpeed * 100f * Time.deltaTime);
            UpdateAudio();
        }

        private void UpdateAudio(bool force = false)
        {
            if (audioSource == null) return;

            // Convert nerves level (0-100) to normalized (0-1) and apply curve for smooth transitions
            float rawNormalizedNerves = _visualNervesLevel / 100f;
            float normalizedNerves = intensityCurve.Evaluate(rawNormalizedNerves);

            // Apply volume scaling
            audioSource.volume = normalizedNerves * maxVolume;

            // Apply pitch scaling (lerp between min and max pitch)
            audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, normalizedNerves);

            // Start/stop audio based on whether there's any intensity
            if (normalizedNerves > 0.001f)
            {
                if (!audioSource.isPlaying && nervesAudioClip != null)
                    audioSource.Play();
            }
            else
            {
                if (audioSource.isPlaying)
                    audioSource.Stop();
            }

            // Debug logging at extreme states for troubleshooting
            if (showDebugInfo && (normalizedNerves > 0.99f || normalizedNerves < 0.01f))
            {
                string state = normalizedNerves < 0.01f ? "IDLE" : "MAX";
                Debug.Log($"[NervesAudio] {state} | Level: {_visualNervesLevel:F4} | Vol: {audioSource.volume:F2} | Pitch: {audioSource.pitch:F2}");
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
            if (showDebugInfo) Debug.Log("Audio Demo: STARTED");
            if (_demoCoroutine != null) StopCoroutine(_demoCoroutine);
            _demoCoroutine = StartCoroutine(DemoSequence());
        }

        private void StopDemo()
        {
            isDemoActive = false;
            if (showDebugInfo) Debug.Log("Audio Demo: STOPPED");
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
                UpdateAudio();
                yield return null;
            }

            // Ensure we hit exactly 100% before holding
            _visualNervesLevel = 100f;
            UpdateAudio();

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
                UpdateAudio();
                yield return null;
            }

            // Ensure we hit exactly 0% and clean up
            _visualNervesLevel = 0f;
            UpdateAudio();

            isDemoActive = false;
            _demoCoroutine = null;
            if (showDebugInfo) Debug.Log("Audio Demo: COMPLETED");
        }

        private void OnGUI()
        {
            if (showDebugInfo)
            {
                GUILayout.BeginArea(new Rect(10, 200, 350, 100));
                GUILayout.Label($"[Audio] Nerves: {_currentNervesLevel:F1}");
                GUILayout.Label($"[Audio] Visual: {_visualNervesLevel:F1}");
                GUILayout.Label($"[Audio] Volume: {(audioSource != null ? audioSource.volume : 0f):F2}");
                GUILayout.Label($"[Audio] Pitch: {(audioSource != null ? audioSource.pitch : 0f):F2}");
                GUILayout.Label($"[Audio] Demo (Press '{debugToggleKeyName}'): {(isDemoActive ? "ACTIVE" : "OFF")}");
                GUILayout.EndArea();
            }
        }
    }
}
