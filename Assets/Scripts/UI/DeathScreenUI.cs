using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DeathScreenUI : MonoBehaviour
{
    private VisualElement deathContainer;
    private Button restartButton;
    private Button quitButton;

    [SerializeField] private MonoBehaviour pauseMenuScript;

    void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        deathContainer = root.Q<VisualElement>("DeathContainer");
        restartButton = root.Q<Button>("RestartButton");
        quitButton = root.Q<Button>("QuitButton");

        // Safety checks
        if (deathContainer == null) Debug.LogError("DeathContainer not found");
        if (restartButton == null) Debug.LogError("RestartButton not found");
        if (quitButton == null) Debug.LogError("QuitButton not found");

        restartButton.clicked += RestartGame;
        quitButton.clicked += QuitGame;

        // Hide screen initially
        deathContainer.style.display = DisplayStyle.None;
    }

    public void Show()
    {
        deathContainer.style.display = DisplayStyle.Flex;

        // Unlock cursor
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        if (pauseMenuScript)
            pauseMenuScript.enabled = false;

        // Time.timeScale = 0f;
    }

    void RestartGame()
    {
       // Time.timeScale = 1f;

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    void QuitGame()
    {
#if UNITY_EDITOR
    // Stop play mode in the editor
    UnityEditor.EditorApplication.isPlaying = false;
#else
    // Quit the built application
    Application.Quit();
#endif
    }
}