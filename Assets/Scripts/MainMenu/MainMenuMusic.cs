using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MainMenuMusic : MonoBehaviour
{
    [SerializeField] private AudioClip themeMusic;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    private AudioSource _source;
    private Coroutine _fadeRoutine;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.clip = themeMusic;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.volume = volume;
        if (themeMusic != null) _source.Play();
    }

    public void FadeOut()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(fadeOutDuration));
    }

    private IEnumerator FadeRoutine(float duration)
    {
        float startVolume = _source.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _source.volume = Mathf.Lerp(startVolume, 0f, t / duration);
            yield return null;
        }
        _source.volume = 0f;
        _source.Stop();
        _fadeRoutine = null;
    }
}
