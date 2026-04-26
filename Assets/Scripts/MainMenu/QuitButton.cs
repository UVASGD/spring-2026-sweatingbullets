using UnityEngine;

public class QuitButton : MonoBehaviour
{
    [SerializeField] private float wobbleAngle = 8f;
    [SerializeField] private float wobbleSpeed = 12f;

    [Tooltip("Pivot the button swings from, in local pixels relative to the RectTransform's pivot. " +
             "(0, 50) hangs the button from a point 50px above its pivot, like a sign on a hook.")]
    [SerializeField] private Vector2 anchorOffset = new Vector2(0f, 50f);

    private Vector3 _restPosition;
    private Quaternion _restRotation;
    private float _phase;

    private void Awake()
    {
        _restPosition = transform.localPosition;
        _restRotation = transform.localRotation;
        _phase = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        _phase += Time.unscaledDeltaTime * wobbleSpeed;
        float angle = Mathf.Sin(_phase) * wobbleAngle;

        // Snap back to rest each frame, then rotate around the anchor in world space.
        transform.localPosition = _restPosition;
        transform.localRotation = _restRotation;
        transform.RotateAround(transform.TransformPoint(anchorOffset), transform.forward, angle);
    }
}
