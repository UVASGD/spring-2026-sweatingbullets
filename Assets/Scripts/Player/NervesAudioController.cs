using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class NervesAudioController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioLowPassFilter lowPassFilter;

        [Header("Audio")]
        [SerializeField] private AudioClip loopClip;
        [SerializeField] private bool playOnStart = true;

        [Header("Timing")]
        [SerializeField] private float transitionSpeed = 2f;

        [Header("Intensity Curves")]
        [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Volume")]
        [SerializeField] private float minVolume = 0f;
        [SerializeField] private float maxVolume = 1f;

        [Header("Onset")]
        [SerializeField] private bool silenceAtZero = true;
        [SerializeField] private float startNervesThreshold = 0.5f;
        [SerializeField] private float startVolume = 0.08f;

        [Header("Pitch")]
        [SerializeField] private bool affectPitch = true;
        [SerializeField] private float minPitch = 1f;
        [SerializeField] private float maxPitch = 1.2f;

        [Header("Low Pass")]
        [SerializeField] private bool affectLowPass = false;
        [SerializeField] private float minCutoff = 500f;
        [SerializeField] private float maxCutoff = 22000f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private string debugToggleKeyName = "n";
        [SerializeField] private bool isDemoActive = false;
        [SerializeField] private float demoIntensifyDuration = 3f;
        [SerializeField] private float demoFadeDuration = 5f;

        private float _currentNervesLevel = 0f;
        private float _audioNervesLevel = 0f;
        private Coroutine _demoCoroutine;

        private void Start()
        {
            if (intensityCurve.length == 2 && intensityCurve[0].value == 0 && intensityCurve[1].value == 1)
                intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                Debug.LogError("No AudioSource assigned.");
                enabled = false;
                return;
            }

            audioSource.loop = true;

            if (loopClip != null)
                audioSource.clip = loopClip;

            ApplyAudio(true);

            if (playOnStart && audioSource.clip != null && !audioSource.isPlaying)
                audioSource.Play();

            if (playOnStart && audioSource.clip == null)
                Debug.LogWarning("No loop clip assigned.");
        }

        private void Update()
        {
            HandleDebugInput();

            if (!isDemoActive)
                SmoothAudio();
        }

        public void SetNervesLevel(float level)
        {
            if (isDemoActive) return;
            _currentNervesLevel = Mathf.Clamp(level, 0f, 100f);

            if (!audioSource.isPlaying && audioSource.clip != null)
                audioSource.Play();
        }

        private void SmoothAudio()
        {
            if (Mathf.Approximately(_audioNervesLevel, _currentNervesLevel)) return;

            _audioNervesLevel = Mathf.MoveTowards(
                _audioNervesLevel,
                _currentNervesLevel,
                transitionSpeed * 100f * Time.deltaTime
            );

            ApplyAudio();
        }

        private void ApplyAudio(bool force = false)
        {
            float rawNormalized = _audioNervesLevel / 100f;
            float targetVolume;
            float intensity;

            if (silenceAtZero && _audioNervesLevel <= 0.0001f)
            {
                targetVolume = 0f;
                intensity = 0f;
            }
            else if (rawNormalized <= (startNervesThreshold / 100f))
            {
                targetVolume = 0f;
                intensity = 0f;
            }
            else
            {
                float thresholdNormalized = startNervesThreshold / 100f;
                float remapped = Mathf.InverseLerp(thresholdNormalized, 1f, rawNormalized);
                float shaped = intensityCurve.Evaluate(remapped);
                float minOnVolume = Mathf.Max(minVolume, startVolume);
                targetVolume = Mathf.Lerp(minOnVolume, maxVolume, shaped);
                intensity = shaped;
            }

            audioSource.volume = targetVolume;

            if (!audioSource.isPlaying && audioSource.clip != null && targetVolume > 0.0001f)
                audioSource.Play();

            if (audioSource.isPlaying && targetVolume <= 0.0001f)
                audioSource.Stop();

            if (affectPitch)
                audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, intensity);

            if (affectLowPass)
            {
                if (lowPassFilter == null)
                    lowPassFilter = GetComponent<AudioLowPassFilter>();

                if (lowPassFilter != null)
                    lowPassFilter.cutoffFrequency = Mathf.Lerp(minCutoff, maxCutoff, intensity);
            }

            if (showDebugInfo && (intensity > 0.99f || rawNormalized < 0.01f))
            {
                string state = rawNormalized < 0.01f ? "IDLE" : "MAX";
                Debug.Log($"[NervesAudio] {state} | Level: {_audioNervesLevel:F4} | Intensity: {intensity:F3} | Vol: {audioSource.volume:F3} | Pitch: {audioSource.pitch:F3}");
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
            float elapsed = 0f;
            float startLevel = _audioNervesLevel;

            while (elapsed < demoIntensifyDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / demoIntensifyDuration;
                float smoothT = t * t;
                _audioNervesLevel = Mathf.Lerp(startLevel, 100f, smoothT);
                ApplyAudio();
                yield return null;
            }

            _audioNervesLevel = 100f;
            ApplyAudio();

            yield return new WaitForSeconds(2.5f);

            elapsed = 0f;
            startLevel = 100f;

            while (elapsed < demoFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / demoFadeDuration;
                float smoothT = 1f - (1f - t) * (1f - t);
                _audioNervesLevel = Mathf.Lerp(startLevel, 0f, smoothT);
                ApplyAudio();
                yield return null;
            }

            _audioNervesLevel = 0f;
            ApplyAudio();

            isDemoActive = false;
            _demoCoroutine = null;
            if (showDebugInfo) Debug.Log("Demo: COMPLETED");
        }

        public void SetLoopClip(AudioClip clip, bool restartIfPlaying = true)
        {
            loopClip = clip;

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null) return;

            audioSource.clip = loopClip;

            if (restartIfPlaying && audioSource.isPlaying)
            {
                audioSource.Stop();
                if (audioSource.clip != null)
                    audioSource.Play();
            }
        }
    }
}
