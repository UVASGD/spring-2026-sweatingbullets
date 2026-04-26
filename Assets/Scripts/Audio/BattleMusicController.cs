using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Owns playback of the battle/action music. Fades out the ambient track and
    /// starts the action track the first time an enemy is sighted in a round.
    /// </summary>
    public class BattleMusicController : MonoBehaviour
    {
        [Header("Action Track")]
        [SerializeField] private AudioSource actionAudioSource;
        [SerializeField] private AudioClip actionTrack;

        [Header("Ambient Handoff")]
        [Tooltip("Ambient track to fade out when battle music starts. Optional.")]
        [SerializeField] private AmbientAudio ambientAudio;
        [Tooltip("Seconds to fade ambient out. Action track starts after this delay so the two don't overlap.")]
        [SerializeField] private float ambientFadeOutDuration = 1.5f;

        public void OnFirstEnemySighted()
        {
            if (actionAudioSource == null || actionTrack == null) return;
            if (actionAudioSource.isPlaying && actionAudioSource.clip == actionTrack) return;

            float delay = 0f;
            if (ambientAudio != null && ambientFadeOutDuration > 0f)
            {
                ambientAudio.FadeOut(ambientFadeOutDuration);
                delay = ambientFadeOutDuration;
            }

            actionAudioSource.clip = actionTrack;
            actionAudioSource.loop = true;
            if (delay > 0f) actionAudioSource.PlayDelayed(delay);
            else actionAudioSource.Play();
        }
    }
}
