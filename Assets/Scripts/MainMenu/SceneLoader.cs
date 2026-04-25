
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public void mainStart()
    {
        Debug.Log("Button clicked - starting countdown");
        // Start the coroutine that handles the waiting AND the loading
        StartCoroutine(WaitAndLoad(1f));
    }

    // This is now one sequence: Wait -> Then Load
    public IEnumerator WaitAndLoad(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);

        Debug.Log("Time up! Loading scene...");
        SceneManager.LoadScene("Main");
    }

    public void quitGame()
    {
        Application.Quit();
        Debug.Log("Game has been quitted");
    }
}