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