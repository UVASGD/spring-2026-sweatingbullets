

using UnityEngine;

public class titleCamera : MonoBehaviour
{

    public Vector3 endPos = new Vector3(284.0f, 284.0f, 77.2f);
    public Vector3 startPos;
    public float camSpeed = 0.5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        transform.position = startPos;

    }

    // Update is called once per frame
    void Update()
    {  
        transform.position = Vector3.MoveTowards(transform.position, endPos, camSpeed);
        Debug.Log(transform.position);
    }
}
