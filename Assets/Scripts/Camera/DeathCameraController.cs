using UnityEngine;
using System.Collections;

public class DeathCameraController : MonoBehaviour
{
    [SerializeField] private Camera mainCam;
    [SerializeField] private Camera deathCam;
    [SerializeField] private MonoBehaviour mouseLookScript;
    [SerializeField] private Transform player;
    [SerializeField] private UIToolkitScreenFade screenFade;
    [SerializeField] private DeathScreenUI deathScreen;

    private DeathCameraFollow deathFollow;

    void Start()
    {
        // Auto-find fade if reference was lost after scene reload
        if (screenFade == null)
            screenFade = FindObjectOfType<UIToolkitScreenFade>();

        // Auto-find death screen if missing
        if (deathScreen == null)
            deathScreen = FindObjectOfType<DeathScreenUI>();

        deathFollow = deathCam.GetComponent<DeathCameraFollow>();

        // Ensure the death camera always knows the player
        if (deathFollow != null && player != null)
            deathFollow.SetTarget(player);

        // Death camera should not render at start
        if (deathCam != null)
            deathCam.enabled = false;
    }

    public void ActivateDeathCam()
    {
        StartCoroutine(DeathTransition());
    }

    private IEnumerator DeathTransition()
    {
        // Fade screen to black
        if (screenFade != null)
        {
            yield return StartCoroutine(screenFade.FadeOut());
        }

        // Stop player camera control
        if (mouseLookScript != null)
            mouseLookScript.enabled = false;

        // Freeze the death camera so it stops following
        if (deathFollow != null)
            deathFollow.FreezeCamera();

        // Switch cameras
        if (mainCam != null) mainCam.enabled = false;
        if (deathCam != null) deathCam.enabled = true;

        // Small delay so camera settles
        yield return new WaitForSeconds(0.2f);

        // Fade back in to the death camera
        if (screenFade != null)
        {
            yield return StartCoroutine(screenFade.FadeIn());
        }

        // Show death screen UI
        if (deathScreen != null)
        {
            deathScreen.Show();
        }
    }
}