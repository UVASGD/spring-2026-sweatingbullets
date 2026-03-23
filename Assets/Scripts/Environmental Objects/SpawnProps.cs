using UnityEngine;

public class SpawnProps : MonoBehaviour
{
    public PropInfo[] propInfos;

    [System.Serializable]
    public class PropInfo {
        public float spawnChance = 0.15f;
        public GameObject propPrefab;
    }

    void Start(){
        ActuallySpawnProps();
    }

    private void ActuallySpawnProps(){
        foreach(var info in propInfos){
            if(UnityEngine.Random.Range(0f, 1f) < info.spawnChance){
                SpawnAProp(info.propPrefab);
            }
        }
    }

    void SpawnAProp(GameObject prefabToSpawn){
        int attempts = 0;
        while(attempts < 100f){
            Vector3 randomOffset = new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
            Collider[] hits = Physics.OverlapBox(transform.position + randomOffset, Vector3.one * 1f, Quaternion.identity);
            bool somethingInSpace = false;
            foreach(var hit in hits){
                if(hit.transform.tag != "Ground")
                    somethingInSpace = true;
            }
            if (somethingInSpace == false){
                Instantiate(prefabToSpawn, transform.position + randomOffset, Quaternion.identity);
                break;
            }
            attempts += 1;
        }
    }
}
