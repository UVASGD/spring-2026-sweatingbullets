using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework.Constraints;
using Unity.AI.Navigation;
using UnityEngine;
using Enemy;
using Unity.VisualScripting;

namespace Tiles
{
    public class WFCManager : MonoBehaviour
    {
        [System.Serializable]
        public struct ManualPlacement
        {
            public Vector2Int position;
            public TileDefinition tile;
            public int rotation; // 0-3
        }

        public List<TileDefinition> allTileDefinitions;

        public int gridSizeX = 10;
        public int gridSizeY = 10;

        public int tileSize = 5;

        private Dictionary<Vector2Int, WFCcell> grid;

        private bool isDone = false;

        public TileDefinition wallTileDefinition;

        public List<ManualPlacement> mapFeatures;
        
        public NavMeshSurface navMeshSurface;

        public GameObject enemy;
        public List<Transform> spawnPoints;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake()
        {
            
        }
        void Start()
        {
            StartCoroutine(RunWFC());
        }

        // Update is called once per frame
        void Update()
        {

        }

        IEnumerator RunWFC()
        {
            InitializeGrid();

            // place macro features first and propagate them once
            foreach (var feature in mapFeatures)
            {
                if (grid.TryGetValue(feature.position, out WFCcell cell))
                {
                    // Force the cell to this specific oriented tile
                    OrientedTile forcedTile = new OrientedTile(feature.tile, feature.rotation);
                    cell.possibleTiles = new List<OrientedTile> { forcedTile };
                    cell.isCollapsed = true;

                    // Ripple the constraints out from this feature
                    Propagate(cell);
                }
            }

            // prevent infinite loops
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
                //yield return null;
                //yield return new WaitForSeconds(0.05f); // set to null if I want to be instant
            }

            yield return null;

            print("WFC grid planning finished. Instantiating...");
            InstantiateGrid();
            SurroundWithWalls();
            
            navMeshSurface.BuildNavMesh();
            
            foreach (var t in spawnPoints)
            {
                GameObject clone = Instantiate(enemy, t.position, t.rotation);
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
                        Instantiate(wallTileDefinition.prefab,
                            new Vector3(i * tileSize, 0, j * tileSize),
                            Quaternion.identity, transform);
                }
            }
        }

        void InitializeGrid()
        {
            grid = new Dictionary<Vector2Int, WFCcell>();
            for (int x = 0; x < gridSizeX; x++)
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                grid.Add(pos, new WFCcell(pos, allTileDefinitions));
            }
        }

        // Step 1: Find lowest entropy
        WFCcell GetLowestEntropyCell()
        {
            var uncollapsed = grid.Values.Where(c => !c.isCollapsed).ToList();
            if (uncollapsed.Count == 0) return null;

            uncollapsed.Sort((a, b) => a.possibleTiles.Count.CompareTo(b.possibleTiles.Count));

            int lowestCount = uncollapsed[0].possibleTiles.Count;
            var bestCandidates = uncollapsed.Where(c => c.possibleTiles.Count == lowestCount).ToList();
            return bestCandidates[Random.Range(0, bestCandidates.Count)];
        }

        // Step 2: Collapse
        void CollapseCell(WFCcell cell)
        {
            if (cell.possibleTiles.Count == 0)
            {
                Debug.Log("No tiles fit at " + cell.position);
                return;
            }

            // Weighted random. Use spawn weight here
            OrientedTile selected = cell.possibleTiles[0];

            int totalWeight = cell.possibleTiles.Sum(t => t.definition.spawnWeight);
            int rand = Random.Range(0, totalWeight);
            int current = 0;
            foreach (var t in cell.possibleTiles)
            {
                current += t.definition.spawnWeight;
                if (rand < current)
                {
                    selected = t;
                    break;
                }
            }

            // selected a tile! yay!
            cell.possibleTiles.Clear();
            cell.possibleTiles.Add(selected);
            cell.isCollapsed = true;
        }

        // step 3: Propagate
        void Propagate(WFCcell startCell)
        {
            Stack<WFCcell> stack = new Stack<WFCcell>();
            stack.Push(startCell);

            while (stack.Count > 0)
            {
                WFCcell current = stack.Pop();
                Vector2Int currentPosition = current.position;

                // checking all four neighbors
                foreach (var dir in new Vector2Int[]
                             { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    Vector2Int neighborPosition = currentPosition + dir;
                    if (!grid.ContainsKey(neighborPosition))
                        continue;

                    WFCcell neighbor = grid[neighborPosition];
                    if (neighbor.isCollapsed)
                        continue;

                    // restrict the neighbor based on current's options
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

            foreach (var neighborOption in neighbor.possibleTiles)
            {
                bool isCompatibleWithAny = false;

                // Does this neighborOption match AT LEAST ONE of the source's current possibilities?
                foreach (var sourceOption in source.possibleTiles)
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

            foreach (var remove in toRemove)
            {
                neighbor.possibleTiles.Remove(remove);
            }

            return neighborChanged;

        }

        bool CanConnect(OrientedTile a, OrientedTile b, Vector2Int dir)
        {
            int dirIndex = GetDirIndex(dir); // direction from a to b
            int oppositeDirIndex = (dirIndex + 2) % 4; // direction vice versa

            // get the sides that create the edge that defines the connection
            var edgeA = a.GetEdge(dirIndex);
            var edgeB = b.GetEdge(oppositeDirIndex);

            return edgeA == edgeB;
        }

        void InstantiateGrid()
        {
            foreach (var kvp in grid)
            {
                if (kvp.Value.possibleTiles.Count == 1)
                {
                    var tile = kvp.Value.possibleTiles[0];
                    Instantiate(tile.definition.prefab,
                        new Vector3(kvp.Key.x * tileSize, 0, kvp.Key.y * tileSize),
                        Quaternion.Euler(0, tile.rotationIndex * 90, 0), transform);
                }
            }
        }

        int GetDirIndex(Vector2Int dir) // 0 = north, 1 = east, 2 = south, 3 = west
        {
            if (dir == Vector2Int.up) return 0;
            if (dir == Vector2Int.right) return 1;
            if (dir == Vector2Int.down) return 2;
            if (dir == Vector2Int.left) return 3;
            return -1;
        }
    }
}