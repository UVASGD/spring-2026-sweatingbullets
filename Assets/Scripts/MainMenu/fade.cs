using UnityEngine;
using System.Collections;

public class fade : MonoBehaviour
{
    public float transparency = 0.0f;

    void Start()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        Color color = renderer.color;
        color.a = 0f;
        renderer.color = color;
    }

    void SetAlpha(float alpha)
    {
        // Access the MeshRenderer and its material
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();

        if (renderer != null)
        {


            // Get the current color
            Color color = renderer.color;

            // Set new alpha (value between 0.0f and 1.0f)
            color.a = alpha;

            // Apply the color back to the material
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

        yield return new WaitForSeconds(5f);

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