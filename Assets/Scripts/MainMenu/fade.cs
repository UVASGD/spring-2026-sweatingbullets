using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class fade : MonoBehaviour
{
    public float transparency = 0.0f;

    public bool IntroFadeComplete { get; private set; }

    void Start()
    {
        SetAlpha(1f);
        StartCoroutine(IntroFade());
    }

    IEnumerator IntroFade()
    {
        yield return StartCoroutine(FadeRoutine(1.0f, 0.0f));
        IntroFadeComplete = true;
    }

    void SetAlpha(float alpha)
    {
        Image renderer = GetComponent<Image>();

        if (renderer != null)
        {
            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }



    public void fadeout()
    {
        StartCoroutine(FadeRoutine(1.0f, 0.0f));

    }

    public void fadein()
    {
        StartCoroutine(FadeRoutine(0.0f, 1.0f));
    }

    public void fadeStart()
    {
        StartCoroutine(inAndOut());
    }

    public IEnumerator inAndOut()
    {
        // yield return tells the script: "Wait until this coroutine finishes before moving on"
        yield return StartCoroutine(FadeRoutine(0.0f, 1.0f));

        yield return new WaitForSeconds(1f);

        yield return StartCoroutine(FadeRoutine(1.0f, 0.0f));
    }
    //public
    //IEnumerator Sequence()
    //{
    //    yield return StartCoroutine(fadeout()); // wait until fade finishes
    //    fadein();
    //}

    IEnumerator FadeRoutine(float start, float end)
    {
        float duration = 1.0f;
        float time = 0;

        while (time < duration)
        {
            float alpha = Mathf.Lerp(start, end, time / duration);
            SetAlpha(alpha);
            time += Time.deltaTime;
            yield return null;
        }

        SetAlpha(end);
    }
}
