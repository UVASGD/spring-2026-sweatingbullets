using UnityEngine;

public class BillboardSprite : MonoBehaviour
{
    public Camera _cam; // drag your camera in here via Inspector
    
    void LateUpdate()
    {
        if (_cam == null) return; // safety check
        
        transform.LookAt(transform.position + _cam.transform.rotation * Vector3.forward,
                         _cam.transform.rotation * Vector3.up);

        float elevation = Vector3.Dot(transform.position.normalized, Vector3.up);
        float alpha = Mathf.Clamp01((elevation + 0.1f) / 0.3f);
        GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, alpha);
    }
}