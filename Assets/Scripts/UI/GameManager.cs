using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private WinScreen winScreen;

    void Awake()
    {
        Instance = this;
        winScreen = FindObjectOfType<WinScreen>(true);
    }

    public static bool IsGameOver { get; private set; }
    public void ShowWinScreen()
    {
        IsGameOver = true;
        winScreen.ShowWinScreen(); 
    }
}