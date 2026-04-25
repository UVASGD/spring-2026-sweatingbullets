using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class UIToolkitScreenFade : MonoBehaviour
{
    private VisualElement fadeScreen;

    [SerializeField] private float fadeSpeed = 2f;

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
        float alpha = fadeScreen.style.opacity.value;

        while (alpha > 0)
        {
            alpha -= Time.deltaTime * fadeSpeed;
            fadeScreen.style.opacity = alpha;
            yield return null;
        }
    }
}