using UnityEngine;

public class QuitButton : MonoBehaviour
{
    public Transform to;
    public Transform from;
    int rotateSpeed = 50;

    private float timeCount = 0.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //transform.Rotate(0, 0, Mathf.Sin(rotateSpeed * Time.deltaTime));
        // transform.rotate = Quaternion.Slerp(from.rotation, to.rotation, timeCount);
        // timeCount = timeCount + Time.deltaTime;
    }
}
