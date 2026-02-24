using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    /// <summary>
    /// Controls nerves-based audio effects: breathing and heartbeat that scale with nerves level.
    /// Press 'N' to trigger demo mode (3s ramp up, 2.5s hold, 5s fade out).
    /// </summary>
    public class NervesAudioController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource heartbeatAudioSource;
        [SerializeField] private AudioSource breathingAudioSource;

        [SerializeField] private NervesManager nervesManager;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip heartbeatAudioClip;
        [SerializeField] private AudioClip breathingAudioClip;

        [Header("Heartbeat Audio Settings")]
        [SerializeField] private float maxVolume = 1f;
        [SerializeField] private float minPitch = 0.8f;
        [SerializeField] private float maxPitch = 1.5f;

        [Header("Breathing Audio Settings")]
        [SerializeField] private float breathingMaxVolume = 1f;
        [SerializeField] private float breathingMinPitch = 0.85f;
        [SerializeField] private float breathingMaxPitch = 1.3f;

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

            if (nervesManager != null)
            {
                isDemoActive = false;
                if (_demoCoroutine != null)
                {
                    StopCoroutine(_demoCoroutine);
                    _demoCoroutine = null;
                }
            }

            // Find or create heartbeat AudioSource if not assigned
            if (heartbeatAudioSource == null)
                heartbeatAudioSource = GetComponent<AudioSource>();

            if (heartbeatAudioSource == null)
            {
                heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
                Debug.LogWarning("No AudioSource assigned for heartbeat, created one automatically.");
            }

            // Configure heartbeat AudioSource for looping
            if (heartbeatAudioClip != null)
            {
                heartbeatAudioSource.clip = heartbeatAudioClip;
                heartbeatAudioSource.loop = true;
                heartbeatAudioSource.playOnAwake = false;
            }
            else
            {
                Debug.LogWarning("No heartbeatAudioClip assigned.");
            }

            // Find or create breathing AudioSource if not assigned
            if (breathingAudioSource == null)
            {
                breathingAudioSource = gameObject.AddComponent<AudioSource>();
                Debug.LogWarning("No breathing AudioSource assigned, created one automatically.");
            }

            // Configure breathing AudioSource for looping
            if (breathingAudioClip != null)
            {
                breathingAudioSource.clip = breathingAudioClip;
                breathingAudioSource.loop = true;
                breathingAudioSource.playOnAwake = false;
            }
            else
            {
                Debug.LogWarning("No breathingAudioClip assigned.");
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
            // Convert nerves level (0-100) to normalized (0-1) and apply curve for smooth transitions
            float rawNormalizedNerves = _visualNervesLevel / 100f;
            float normalizedNerves = intensityCurve.Evaluate(rawNormalizedNerves);

            // --- Heartbeat ---
            if (heartbeatAudioSource != null)
            {
                heartbeatAudioSource.volume = normalizedNerves * maxVolume;
                heartbeatAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, normalizedNerves);

                if (normalizedNerves > 0.001f)
                {
                    if (!heartbeatAudioSource.isPlaying && heartbeatAudioClip != null)
                        heartbeatAudioSource.Play();
                }
                else
                {
                    if (heartbeatAudioSource.isPlaying)
                        heartbeatAudioSource.Stop();
                }
            }

            // --- Breathing ---
            if (breathingAudioSource != null)
            {
                breathingAudioSource.volume = normalizedNerves * breathingMaxVolume;
                breathingAudioSource.pitch = Mathf.Lerp(breathingMinPitch, breathingMaxPitch, normalizedNerves);

                if (normalizedNerves > 0.001f)
                {
                    if (!breathingAudioSource.isPlaying && breathingAudioClip != null)
                        breathingAudioSource.Play();
                }
                else
                {
                    if (breathingAudioSource.isPlaying)
                        breathingAudioSource.Stop();
                }
            }

            // Debug logging at extreme states for troubleshooting
            if (showDebugInfo && (normalizedNerves > 0.99f || normalizedNerves < 0.01f))
            {
                string state = normalizedNerves < 0.01f ? "IDLE" : "MAX";
                Debug.Log($"[NervesAudio] {state} | Level: {_visualNervesLevel:F4} | Heartbeat Vol: {(heartbeatAudioSource != null ? heartbeatAudioSource.volume : 0f):F2} | Breathing Vol: {(breathingAudioSource != null ? breathingAudioSource.volume : 0f):F2}");
            }
        }

        private void HandleDebugInput()
        {
            if (nervesManager != null) return;
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
                GUILayout.BeginArea(new Rect(10, 200, 350, 130));
                GUILayout.Label($"[Audio] Nerves: {_currentNervesLevel:F1}");
                GUILayout.Label($"[Audio] Visual: {_visualNervesLevel:F1}");
                GUILayout.Label($"[Audio] Heartbeat Vol: {(heartbeatAudioSource != null ? heartbeatAudioSource.volume : 0f):F2} | Pitch: {(heartbeatAudioSource != null ? heartbeatAudioSource.pitch : 0f):F2}");
                GUILayout.Label($"[Audio] Breathing  Vol: {(breathingAudioSource != null ? breathingAudioSource.volume : 0f):F2} | Pitch: {(breathingAudioSource != null ? breathingAudioSource.pitch : 0f):F2}");
                GUILayout.Label($"[Audio] Demo (Press '{debugToggleKeyName}'): {(isDemoActive ? "ACTIVE" : "OFF")}");
                GUILayout.EndArea();
            }
        }
    }
}
