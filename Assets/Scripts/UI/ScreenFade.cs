using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using System.Collections;

public class UIToolkitScreenFade : MonoBehaviour
{
    private VisualElement fadeScreen;

    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private UnityEvent onFadeInComplete;

    void Awake()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        fadeScreen = root.Q<VisualElement>("fade-screen");

        // Start fully black to hide the scene while it loads
        fadeScreen.style.opacity = 1f;
        StartFadeIn();
    }

    // Add this helper to trigger the fade from other scripts easily
    public void StartFadeIn()
    {
        StartCoroutine(FadeIn());
    }

    public IEnumerator FadeOut()
    {
        float alpha = fadeScreen.style.opacity.value;

        while (alpha < 1)
        {
            alpha += Time.deltaTime * fadeSpeed;
            fadeScreen.style.opacity = alpha;
            yield return null;
        }
    }

    public IEnumerator FadeIn()
    {
        yield return new WaitForSeconds(1.0f);
        float alpha = fadeScreen.style.opacity.value;

        while (alpha > 0)
        {
            alpha -= Time.deltaTime * fadeSpeed;
            fadeScreen.style.opacity = alpha;
            yield return null;
        }

        onFadeInComplete?.Invoke();
    }
}
