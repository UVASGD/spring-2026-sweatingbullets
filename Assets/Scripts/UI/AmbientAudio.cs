using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AmbientAudio : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float targetVolume = 0.7f;
    [SerializeField] private float fadeInDuration = 1f;

    private AudioSource _source;
    private Coroutine _fadeRoutine;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.volume = 0f;
        _source.playOnAwake = false;
    }

    public void FadeIn()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        if (!_source.isPlaying) _source.Play();
        _fadeRoutine = StartCoroutine(FadeRoutine(fadeInDuration, targetVolume));
    }

    public void FadeOut(float duration, bool stopWhenDone = true)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutRoutine(duration, stopWhenDone));
    }

    private IEnumerator FadeOutRoutine(float duration, bool stopWhenDone)
    {
        yield return FadeRoutine(duration, 0f);
        if (stopWhenDone) _source.Stop();
    }

    private IEnumerator FadeRoutine(float duration, float endVolume)
    {
        float startVolume = _source.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _source.volume = Mathf.Lerp(startVolume, endVolume, t / duration);
            yield return null;
        }
        _source.volume = endVolume;
        _fadeRoutine = null;
    }
}
