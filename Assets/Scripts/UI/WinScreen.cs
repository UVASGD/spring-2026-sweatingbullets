using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;

public class WinScreen : MonoBehaviour
{
    public void ShowWinScreen()
    {
        Debug.Log("ShowWinScreen called");
        gameObject.SetActive(true);
        StartCoroutine(InitAfterFrame());
    }

    private IEnumerator InitAfterFrame()
    {
        Debug.Log("InitAfterFrame started");
        yield return null;
        yield return null;
        yield return null;

        Debug.Log("InitAfterFrame after frames");
        var root = GetComponent<UIDocument>().rootVisualElement;
        var overlay = root.Q("Root");
        Debug.Log("Overlay found: " + (overlay != null));

        var restartButton = root.Q<Button>("RestartButton");
        var quitButton    = root.Q<Button>("QuitButton");

        if (restartButton != null) restartButton.clicked += RestartGame;
        if (quitButton != null)    quitButton.clicked += QuitGame;

        if (overlay == null) { yield break; }

        float duration = 0.8f;
        float elapsed = 0f;
        overlay.style.opacity = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlay.style.opacity = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        overlay.style.opacity = 1f;
        Time.timeScale = 0f;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}