using UnityEngine;

public class DeathCameraController : MonoBehaviour
{
    [SerializeField] private Camera mainCam;
    [SerializeField] private Camera deathCam;
    [SerializeField] private MonoBehaviour mouseLookScript;

    public void ActivateDeathCam()
    {
        Debug.Log("ActivateDeathCam called");
        foreach (var c in Camera.allCameras)
        {
            Debug.Log($"CAM: {c.name} enabled={c.enabled} active={c.gameObject.activeInHierarchy} depth={c.depth} tag={c.tag}");
        }
        if (!mainCam || !deathCam)
        {
            Debug.LogError($"Missing camera reference. mainCam={(mainCam?mainCam.name:"NULL")}, deathCam={(deathCam?deathCam.name:"NULL")}");
            return;
        }

        // If the death camera GameObject was disabled, enabling the component won't help.
        deathCam.gameObject.SetActive(true);
        mainCam.gameObject.SetActive(true); // in case something else disabled it

        // Disable player camera rotation
        if (mouseLookScript) mouseLookScript.enabled = false;

        // Switch cameras
        mainCam.enabled = false;
        deathCam.enabled = true;

        // Make deathCam win even if another camera is also enabled (depth tie-breaker)
        deathCam.depth = 100;
        mainCam.depth = 0;

        // Cursor optional
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log($"Switched. mainCam.enabled={mainCam.enabled}, deathCam.enabled={deathCam.enabled}, deathCam.activeInHierarchy={deathCam.gameObject.activeInHierarchy}");
    }
}