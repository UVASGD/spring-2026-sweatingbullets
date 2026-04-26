using UnityEngine;

public class titleCamera : MonoBehaviour
{

    public Vector3 endPos = new Vector3(284.0f, 284.0f, 77.2f);
    public Vector3 startPos;
    public float camSpeed;
    public float camAccel;
    [SerializeField] private fade fadeController;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        transform.position = startPos;

    }

    // Update is called once per frame
    void Update()
    {
        if (fadeController != null && !fadeController.IntroFadeComplete)
            return;

        //transform.position = Vector3.MoveTowards(transform.position, endPos, (endPos - transform.position).z * camSpeed * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, endPos, camSpeed * Time.deltaTime);
        Debug.Log(transform.position);
        if (transform.position != endPos)
            camSpeed += camAccel;
    }
}
