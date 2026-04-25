

using UnityEngine;

public class creditScroll : MonoBehaviour
{

    Vector3 endPos = new Vector3(278, 500, 150);
    bool moving = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {   
        transform.position = new Vector3(278, -10, 150);
    }

    // Update is called once per frame
    void Update()
    {
        if (moving)
        {
            
            transform.position = Vector3.MoveTowards(transform.position, endPos, 50.0f * Time.deltaTime);
        }
    }

    public void scroll()
    {
        transform.position = new Vector3(278, -10, 150);
        moving = true;
    }
}
