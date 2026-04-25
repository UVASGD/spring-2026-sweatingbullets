using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.InputSystem; 

[RequireComponent(typeof(UIDocument))]
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private string menuRootName = "Root";

    private VisualElement menuRoot;
    private Button resumeButton;
    private Button restartButton;
    private Button quitButton;

    private bool isPaused;

    private void Awake()
    {
        var doc = GetComponent<UIDocument>();
        var docRoot = doc.rootVisualElement;

        menuRoot = docRoot.Q<VisualElement>(menuRootName);
        if (menuRoot == null)
        {
            Debug.LogError($"PauseMenuController: Could not find VisualElement named '{menuRootName}' in UXML.");
            enabled = false;
            return;
        }

        resumeButton  = menuRoot.Q<Button>("ResumeButton");
        restartButton = menuRoot.Q<Button>("RestartButton");
        quitButton    = menuRoot.Q<Button>("QuitButton");

        if (resumeButton != null)  resumeButton.clicked += ResumeGame;
        if (restartButton != null) restartButton.clicked += RestartGame;
        if (quitButton != null)    quitButton.clicked += QuitGame;

        isPaused = false;
        Time.timeScale = 1f;
        SetMenuVisible(false);
    }

    private void Update()
    {
        if (GameManager.IsGameOver) return; // ← block pause menu after win

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    private void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        SetMenuVisible(true);
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        resumeButton?.Focus();
    }

    private void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        SetMenuVisible(false);
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetMenuVisible(bool visible)
    {
        menuRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
