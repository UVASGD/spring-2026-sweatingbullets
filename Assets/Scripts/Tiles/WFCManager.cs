using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Enemy;
using Player;
using Unity.AI.Navigation;
using UnityEngine;

namespace Tiles
{
    public class WFCManager : MonoBehaviour
    {
        [System.Serializable]
        public struct ManualPlacement
        {
            public Vector2Int position;
            public TileDefinition tile;
            public int rotation;
        }

        public List<TileDefinition> allTileDefinitions;
        public int gridSizeX = 10;
        public int gridSizeY = 10;
        public int tileSize = 5;
        public TileDefinition wallTileDefinition;
        public List<ManualPlacement> mapFeatures;
        public NavMeshSurface navMeshSurface;
        public GameObject enemy;
        public List<Transform> spawnPoints;

        [Header("Round Pickups")]
        public GameObject gunPickupPrefab;
        public GameObject ammoPickupPrefab;
        public int ammoPickupCount = 3;
        public int bulletsPerAmmoPickup = 1;
        public float minDistanceFromPlayer = 7f;
        public float minDistanceFromEnemySpawn = 6f;
        public float pickupGroundProbeHeight = 10f;
        public float pickupSpawnYOffset = 0.25f;

        private Dictionary<Vector2Int, WFCcell> grid;
        private bool isDone = false;

        void Start()
        {
            StartCoroutine(RunWFC());
        }

        IEnumerator RunWFC()
        {
            InitializeGrid();

            foreach (var feature in mapFeatures)
            {
                if (grid.TryGetValue(feature.position, out WFCcell cell))
                {
                    OrientedTile forcedTile = new OrientedTile(feature.tile, feature.rotation);
                    cell.possibleTiles = new List<OrientedTile> { forcedTile };
                    cell.isCollapsed = true;
                    Propagate(cell);
                }
            }

            int attempts = 0;
            while (!isDone && attempts < gridSizeX * gridSizeY * 7)
            {
                WFCcell cellToCollapse = GetLowestEntropyCell();
                if (cellToCollapse == null)
                {
                    isDone = true;
                    break;
                }

                CollapseCell(cellToCollapse);
                Propagate(cellToCollapse);
                attempts++;
            }

            yield return null;

            print("WFC grid planning finished. Instantiating...");
            InstantiateGrid();
            SurroundWithWalls();

            navMeshSurface.BuildNavMesh();
            SpawnRoundPickups();

            foreach (Transform spawnPoint in spawnPoints)
            {
                GameObject clone = Instantiate(enemy, spawnPoint.position, spawnPoint.rotation);
                clone.GetComponent<EnemyAI>().Init(GameObject.FindWithTag("Player"));
            }
        }

        void SurroundWithWalls()
        {
            for (int i = -1; i <= gridSizeX; i++)
            {
                for (int j = -1; j <= gridSizeY; j++)
                {
                    if (i < 0 || i >= gridSizeX || j < 0 || j >= gridSizeY)
                    {
                        Instantiate(
                            wallTileDefinition.prefab,
                            new Vector3(i * tileSize, 0, j * tileSize),
                            Quaternion.identity,
                            transform);
                    }
                }
            }
        }

        void InitializeGrid()
        {
            grid = new Dictionary<Vector2Int, WFCcell>();
            for (int x = 0; x < gridSizeX; x++)
            {
                for (int y = 0; y < gridSizeY; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    grid.Add(pos, new WFCcell(pos, allTileDefinitions));
                }
            }
        }

        WFCcell GetLowestEntropyCell()
        {
            List<WFCcell> uncollapsed = grid.Values.Where(c => !c.isCollapsed).ToList();
            if (uncollapsed.Count == 0)
            {
                return null;
            }

            uncollapsed.Sort((a, b) => a.possibleTiles.Count.CompareTo(b.possibleTiles.Count));

            int lowestCount = uncollapsed[0].possibleTiles.Count;
            List<WFCcell> bestCandidates = uncollapsed.Where(c => c.possibleTiles.Count == lowestCount).ToList();
            return bestCandidates[Random.Range(0, bestCandidates.Count)];
        }

        void CollapseCell(WFCcell cell)
        {
            if (cell.possibleTiles.Count == 0)
            {
                Debug.Log("No tiles fit at " + cell.position);
                return;
            }

            OrientedTile selected = cell.possibleTiles[0];
            int totalWeight = cell.possibleTiles.Sum(t => t.definition.spawnWeight);
            int rand = Random.Range(0, totalWeight);
            int current = 0;

            foreach (var tile in cell.possibleTiles)
            {
                current += tile.definition.spawnWeight;
                if (rand < current)
                {
                    selected = tile;
                    break;
                }
            }

            cell.possibleTiles.Clear();
            cell.possibleTiles.Add(selected);
            cell.isCollapsed = true;
        }

        void Propagate(WFCcell startCell)
        {
            Stack<WFCcell> stack = new Stack<WFCcell>();
            stack.Push(startCell);

            while (stack.Count > 0)
            {
                WFCcell current = stack.Pop();
                Vector2Int currentPosition = current.position;

                foreach (Vector2Int dir in new[]
                         { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    Vector2Int neighborPosition = currentPosition + dir;
                    if (!grid.ContainsKey(neighborPosition))
                    {
                        continue;
                    }

                    WFCcell neighbor = grid[neighborPosition];
                    if (neighbor.isCollapsed)
                    {
                        continue;
                    }

                    if (ConstrainNeighbor(current, neighbor, dir))
                    {
                        stack.Push(neighbor);
                    }
                }
            }
        }

        bool ConstrainNeighbor(WFCcell source, WFCcell neighbor, Vector2Int dirToNeighbor)
        {
            bool neighborChanged = false;
            List<OrientedTile> toRemove = new List<OrientedTile>();

            foreach (OrientedTile neighborOption in neighbor.possibleTiles)
            {
                bool isCompatibleWithAny = false;

                foreach (OrientedTile sourceOption in source.possibleTiles)
                {
                    if (CanConnect(sourceOption, neighborOption, dirToNeighbor))
                    {
                        isCompatibleWithAny = true;
                        break;
                    }
                }

                if (!isCompatibleWithAny)
                {
                    toRemove.Add(neighborOption);
                    neighborChanged = true;
                }
            }

            foreach (OrientedTile remove in toRemove)
            {
                neighbor.possibleTiles.Remove(remove);
            }

            return neighborChanged;
        }

        bool CanConnect(OrientedTile a, OrientedTile b, Vector2Int dir)
        {
            int dirIndex = GetDirIndex(dir);
            int oppositeDirIndex = (dirIndex + 2) % 4;
            TileDefinition.EdgeType edgeA = a.GetEdge(dirIndex);
            TileDefinition.EdgeType edgeB = b.GetEdge(oppositeDirIndex);
            return edgeA == edgeB;
        }

        void InstantiateGrid()
        {
            foreach (var kvp in grid)
            {
                if (kvp.Value.possibleTiles.Count == 1)
                {
                    OrientedTile tile = kvp.Value.possibleTiles[0];
                    Instantiate(
                        tile.definition.prefab,
                        new Vector3(kvp.Key.x * tileSize, 0, kvp.Key.y * tileSize),
                        Quaternion.Euler(0, tile.rotationIndex * 90, 0),
                        transform);
                }
            }
        }

        int GetDirIndex(Vector2Int dir)
        {
            if (dir == Vector2Int.up) return 0;
            if (dir == Vector2Int.right) return 1;
            if (dir == Vector2Int.down) return 2;
            if (dir == Vector2Int.left) return 3;
            return -1;
        }

        void SpawnRoundPickups()
        {
            List<Vector3> candidatePositions = BuildPickupCandidates();
            if (candidatePositions.Count == 0)
            {
                Debug.LogWarning("WFCManager could not find any valid pickup candidates.");
                return;
            }

            Shuffle(candidatePositions);

            GameObject playerObject = GameObject.FindWithTag("Player");
            int gunSpawnedCount = 0;
            int ammoSpawnedCount = 0;

            foreach (Vector3 candidatePosition in candidatePositions)
            {
                if (!IsFarEnoughFromPlayer(candidatePosition, playerObject) ||
                    !IsFarEnoughFromEnemySpawns(candidatePosition) ||
                    !TryGetGroundedPickupPosition(candidatePosition, out Vector3 groundedPosition))
                {
                    continue;
                }

                if (gunSpawnedCount == 0)
                {
                    SpawnGunPickup(groundedPosition);
                    gunSpawnedCount++;
                    continue;
                }

                if (ammoSpawnedCount < ammoPickupCount)
                {
                    SpawnAmmoPickup(groundedPosition);
                    ammoSpawnedCount++;
                }

                if (gunSpawnedCount == 1 && ammoSpawnedCount >= ammoPickupCount)
                {
                    break;
                }
            }

            if (gunSpawnedCount != 1 || ammoSpawnedCount != ammoPickupCount)
            {
                Debug.LogWarning(
                    $"WFCManager spawned {gunSpawnedCount} gun pickups and {ammoSpawnedCount}/{ammoPickupCount} ammo pickups.");
            }
        }

        List<Vector3> BuildPickupCandidates()
        {
            List<Vector3> candidates = new List<Vector3>();

            foreach (var kvp in grid)
            {
                if (kvp.Value.possibleTiles.Count != 1)
                {
                    continue;
                }

                candidates.Add(new Vector3(kvp.Key.x * tileSize, 0f, kvp.Key.y * tileSize));
            }

            return candidates;
        }

        bool IsFarEnoughFromPlayer(Vector3 position, GameObject playerObject)
        {
            if (playerObject == null)
            {
                return true;
            }

            return PlanarDistance(position, playerObject.transform.position) >= minDistanceFromPlayer;
        }

        bool IsFarEnoughFromEnemySpawns(Vector3 position)
        {
            foreach (Transform spawnPoint in spawnPoints)
            {
                if (spawnPoint == null)
                {
                    continue;
                }

                if (PlanarDistance(position, spawnPoint.position) < minDistanceFromEnemySpawn)
                {
                    return false;
                }
            }

            return true;
        }

        bool TryGetGroundedPickupPosition(Vector3 candidatePosition, out Vector3 groundedPosition)
        {
            Vector3 rayOrigin = candidatePosition + Vector3.up * pickupGroundProbeHeight;
            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    pickupGroundProbeHeight * 2f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                groundedPosition = hit.point + Vector3.up * pickupSpawnYOffset;
                return true;
            }

            groundedPosition = default;
            return false;
        }

        void SpawnGunPickup(Vector3 position)
        {
            GameObject pickupObject = gunPickupPrefab != null
                ? Instantiate(gunPickupPrefab, position, Quaternion.identity)
                : CreateFallbackGunPickup(position);

            if (pickupObject.GetComponent<GunPickup>() == null)
            {
                pickupObject.AddComponent<GunPickup>();
            }
        }

        void SpawnAmmoPickup(Vector3 position)
        {
            GameObject pickupObject = ammoPickupPrefab != null
                ? Instantiate(ammoPickupPrefab, position, Quaternion.identity)
                : CreateFallbackAmmoPickup(position);

            AmmoPickup ammoPickup = pickupObject.GetComponent<AmmoPickup>();
            if (ammoPickup == null)
            {
                ammoPickup = pickupObject.AddComponent<AmmoPickup>();
            }

            ammoPickup.SetAmmoAmount(bulletsPerAmmoPickup);
        }

        GameObject CreateFallbackGunPickup(Vector3 position)
        {
            GameObject pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pickupObject.name = "GunPickup";
            pickupObject.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
            pickupObject.transform.localScale = new Vector3(0.18f, 0.45f, 0.18f);
            ApplyFallbackMaterialColor(pickupObject, new Color(0.2f, 0.2f, 0.2f));
            return pickupObject;
        }

        GameObject CreateFallbackAmmoPickup(Vector3 position)
        {
            GameObject pickupObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pickupObject.name = "AmmoPickup";
            pickupObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            pickupObject.transform.localScale = new Vector3(0.2f, 0.35f, 0.2f);
            ApplyFallbackMaterialColor(pickupObject, new Color(0.76f, 0.62f, 0.18f));
            return pickupObject;
        }

        void ApplyFallbackMaterialColor(GameObject pickupObject, Color color)
        {
            Renderer renderer = pickupObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }

        float PlanarDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                T temp = items[i];
                items[i] = items[swapIndex];
                items[swapIndex] = temp;
            }
        }
    }
}
