using UnityEngine;

public class BirdSpawner : MonoBehaviour
{
    public GameObject birdPrefab;
    public int birdCount = 5;

    public Vector3 spawnArea = new Vector3(50, 20, 50);

    public float minRadius = 5f;
    public float maxRadius = 20f;

    public float minSpeed = 0.5f;
    public float maxSpeed = 2f;

    public float minHeight = 10f;
    public float maxHeight = 25f;

    void Start()
    {
        for (int i = 0; i < birdCount; i++)
        {
            SpawnBird();
        }
    }

    void SpawnBird()
    {
        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnArea.x / 2, spawnArea.x / 2),
            Random.Range(minHeight, maxHeight),
            Random.Range(-spawnArea.z / 2, spawnArea.z / 2)
        );

        Vector3 spawnPos = transform.position + randomOffset ;

        GameObject bird = Instantiate(birdPrefab, spawnPos, Quaternion.identity);

        BirdCircler circler = bird.GetComponent<BirdCircler>();

        circler.centerPoint = spawnPos;
        circler.radius = Random.Range(minRadius, maxRadius);
        circler.speed = Random.Range(minSpeed, maxSpeed);
        circler.heightOffset = spawnPos.y - transform.position.y;
        circler.boundsSize = spawnArea;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, spawnArea);
    }
}