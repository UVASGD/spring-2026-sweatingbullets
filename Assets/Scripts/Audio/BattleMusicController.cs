using System.Collections;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Owns playback of the battle/action music. On first enemy sighting:
    /// fades the ambient track out, plays a transition stinger, and starts
    /// the action track at the stinger's drop point. On combat exit (or
    /// death/win): fades the action track out and optionally resumes ambient.
    /// </summary>
    public class BattleMusicController : MonoBehaviour
    {
        [Header("Action Track")]
        [SerializeField] private AudioSource actionAudioSource;
        [SerializeField] private AudioClip actionTrack;

        private Coroutine _fadeOutCoroutine;

        [Header("Ambient Handoff")]
        [Tooltip("Ambient track to fade out when battle music starts. Optional.")]
        [SerializeField] private AmbientAudio ambientAudio;
        [Tooltip("Seconds to fade ambient out. Should be <= transitionToActionTime so it finishes before action starts.")]
        [SerializeField] private float ambientFadeOutDuration = 1.5f;

        [Header("Transition Stinger")]
        [Tooltip("Optional stinger that bridges ambient -> action. Plays simultaneously with the ambient fade-out.")]
        [SerializeField] private AudioSource transitionAudioSource;
        [SerializeField] private AudioClip transitionStinger;
        [Tooltip("Seconds into the stinger when the action track starts. Set to the stinger's drop point.")]
        [SerializeField] private float transitionToActionTime = 3f;
        [Tooltip("Cut off the stinger when the action track starts. Otherwise it plays out naturally underneath.")]
        [SerializeField] private bool cutTransitionAtActionStart = true;

        [Header("Stop / Exit Behavior")]
        [Tooltip("Seconds to fade the action track out when StopBattleMusic / combat-exit is triggered.")]
        [SerializeField] private float actionFadeOutDuration = 1.0f;

        public void OnCombatEnter()
        {
            if (actionAudioSource == null || actionTrack == null) return;

            // Cancel any in-progress fade-out from a recent combat-exit so the
            // sequence can re-trigger cleanly.
            if (_fadeOutCoroutine != null)
            {
                StopCoroutine(_fadeOutCoroutine);
                _fadeOutCoroutine = null;
                actionAudioSource.Stop();
                actionAudioSource.volume = 1f;
            }

            if (actionAudioSource.isPlaying && actionAudioSource.clip == actionTrack) return;

            if (ambientAudio != null && ambientFadeOutDuration > 0f)
                ambientAudio.FadeOut(ambientFadeOutDuration);

            double now = AudioSettings.dspTime;
            double actionStartDsp = now;

            if (transitionAudioSource != null && transitionStinger != null)
            {
                transitionAudioSource.clip = transitionStinger;
                transitionAudioSource.loop = false;
                transitionAudioSource.Play();
                actionStartDsp = now + transitionToActionTime;
                if (cutTransitionAtActionStart)
                    transitionAudioSource.SetScheduledEndTime(actionStartDsp);
            }
            else if (ambientFadeOutDuration > 0f)
            {
                actionStartDsp = now + ambientFadeOutDuration;
            }

            actionAudioSource.clip = actionTrack;
            actionAudioSource.loop = true;
            if (actionStartDsp > now)
                actionAudioSource.PlayScheduled(actionStartDsp);
            else
                actionAudioSource.Play();
        }

        public void StopBattleMusic()
        {
            if (actionAudioSource == null) return;
            if (!actionAudioSource.isPlaying && actionAudioSource.volume <= 0f) return;
            if (_fadeOutCoroutine != null) StopCoroutine(_fadeOutCoroutine);
            _fadeOutCoroutine = StartCoroutine(FadeOutAndStop(actionAudioSource, actionFadeOutDuration));
        }

        public void OnCombatExit()
        {
            StopBattleMusic();
            if (ambientAudio != null)
                ambientAudio.FadeInFromRandomMidpoint();
        }

        private IEnumerator FadeOutAndStop(AudioSource src, float duration)
        {
            float startVol = src.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }
            src.volume = 0f;
            src.Stop();
            src.volume = 1f;
            _fadeOutCoroutine = null;
        }
    }
}
