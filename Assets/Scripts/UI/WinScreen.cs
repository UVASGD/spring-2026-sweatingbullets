using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;

public class WinScreen : MonoBehaviour
{
    [SerializeField] private Audio.BattleMusicController battleMusic;

    public void ShowWinScreen(int round)
    {
        Debug.Log("ShowWinScreen called");
        if (battleMusic != null)
            battleMusic.StopBattleMusic();
        gameObject.SetActive(true);
        StartCoroutine(InitAfterFrame(round));
    }

    private IEnumerator InitAfterFrame(int round)
    {
        Debug.Log("InitAfterFrame started");
        yield return null;
        yield return null;
        yield return null;

        Debug.Log("InitAfterFrame after frames");
        var root = GetComponent<UIDocument>().rootVisualElement;
        var overlay = root.Q("Root");
        Debug.Log("Overlay found: " + (overlay != null));

        var restartButton   = root.Q<Button>("RestartButton");
        var nextRoundButton = root.Q<Button>("NextRoundButton");
        var quitButton      = root.Q<Button>("QuitButton");
        // Difficulty deprecated.
        // var difficultyLabel = root.Q<Label>("DifficultyLabel");

        if (restartButton != null)   restartButton.clicked   += RestartGame;
        if (nextRoundButton != null) nextRoundButton.clicked += NextRound;
        if (quitButton != null)      quitButton.clicked      += QuitGame;
        // Difficulty deprecated.
        // if (difficultyLabel != null) difficultyLabel.text = $"Difficulty {round}";

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

    private void NextRound()
    {
        Time.timeScale = 1f;
        GameManager.RoundCount++;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        GameManager.RoundCount = 1;
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