using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private WinScreen winScreen;

    public static int RoundCount { get; set; } = 1;

    public static bool IsGameOver { get; private set; }

    // Difficulty deprecated — kept for reference, no longer used.
    // public static int Difficulty => Mathf.Clamp(RoundCount, 1, 10);

    public static float RoundElapsedSeconds { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        winScreen = FindObjectOfType<WinScreen>(true);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsGameOver = false;
        RoundElapsedSeconds = 0f;
        winScreen = FindObjectOfType<WinScreen>(true);
    }

    void Update()
    {
        if (IsGameOver) return;
        RoundElapsedSeconds += Time.deltaTime;
    }

    public void ShowWinScreen()
    {
        IsGameOver = true;
        winScreen.ShowWinScreen(RoundCount);
    }
}