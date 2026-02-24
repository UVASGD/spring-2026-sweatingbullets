
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void mainStart()
    {
        Debug.Log("Button clicked");
        SceneManager.LoadScene("Main");
    }
}
