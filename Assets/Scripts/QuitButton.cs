using UnityEngine;

public class QuitButton : MonoBehaviour
{
    float accel;
    float vel;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(0, 0, 50 * Time.deltaTime);
    }
}
