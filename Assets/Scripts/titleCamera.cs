using UnityEngine;

public class titleCamera : MonoBehaviour
{

    public Vector3 endPos = new Vector3(284.0f, 284.0f, 77.2f);
    public Vector3 startPos;
    public float camSpeed = 0.5f;
    public float duration = 2.0f;
    public float timeElapsed = 0f;
    public float camAccel = 0.5f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        transform.position = startPos;

    }

    // Update is called once per frame
    void Update()
    {  
       
        //transform.position = Vector3.MoveTowards(transform.position, endPos, (endPos - transform.position).z * camSpeed * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, endPos, camSpeed * Time.deltaTime);
        Debug.Log(transform.position);
        camSpeed += camAccel;
    }
}
