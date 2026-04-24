using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using Enemy;

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

        struct Segment
        {
            public Vector2Int pos;
            public Vector2Int dir;
            public Vector2Int widthOff; // Vector2Int.zero for 1-wide (alley) segments
            public int remaining;

            public Segment(Vector2Int p, Vector2Int d, Vector2Int w, int r)
            {
                pos = p; dir = d; widthOff = w; remaining = r;
            }
        }

        // ---- Grid config ----
        public int gridSizeX = 10;
        public int gridSizeY = 10;
        public int tileSize = 5;

        // ---- Prefabs / materials ----
        [Tooltip("Floor tile spawned under every path/filler cell")]
        public GameObject floorTilePrefab;
        [Tooltip("Impassable tile spawned outside the grid as scenery")]
        public GameObject outsideTilePrefab;
        public int outsideRadius = 3;
        [Tooltip("Material using Custom/SandBlend shader — grid bounds set automatically")]
        public Material sandBlendMaterial;

        // ---- Map features (spawn tile, etc.) ----
        public List<ManualPlacement> mapFeatures;
        
        // ---- To spawn the player in the spawn location ----
        public GameObject playerPrefab;

        // ---- Tile references ----
        [Header("Tile References")]
        public TileDefinition pathTileDefinition;
        public TileDefinition spawnTileDefinition;
        public List<TileDefinition> edgeWallDefinitions;
        public List<TileDefinition> cornerWallDefinitions;
        public List<TileDefinition> fillerTileDefinitions;

        // ---- Main roads ----
        [Header("Main Roads (2-wide)")]
        [Tooltip("Number of independent main road origins scattered across the map.")]
        public int mainRoadCount = 3;
        [Tooltip("Minimum Manhattan distance between main road origin cells.")]
        public int mainRoadOriginSpacing = 5;
        public int mainRoadSegmentLengthMin = 3;
        public int mainRoadSegmentLengthMax = 7;
        [Tooltip("Chance at end of each segment to turn 90°. Lower = straighter roads that reach further.")]
        [Range(0f, 1f)] public float mainTurnProbability = 0.2f;
        [Tooltip("Chance at end of each segment to dead-end. Lower = longer road lifetimes.")]
        [Range(0f, 1f)] public float mainDeadEndProbability = 0.1f;
        [Tooltip("Chance at end of each segment to fork: continue straight AND spawn a perpendicular branch. Creates T/Y intersections.")]
        [Range(0f, 1f)] public float mainForkProbability = 0.15f;
        public int maxMainRoadCells = 60;

        // ---- Alleys ----
        [Header("Alleys (1-wide)")]
        public int alleySegmentLengthMin = 1;
        public int alleySegmentLengthMax = 4;
        [Range(0f, 1f)] public float alleyTurnProbability = 0.6f;
        [Range(0f, 1f)] public float alleyBranchProbability = 0.15f;
        [Range(0f, 1f)] public float alleyDeadEndProbability = 0.3f;
        public int maxAlleyCells = 40;
        [Range(0f, 1f)] public float alleyFromMainRoadChance = 0.2f;

        // ---- Buildings ----
        [Header("Buildings")]
        public int buildingSeedCount = 5;
        public Vector2Int buildingRectMin = new Vector2Int(2, 1);
        public Vector2Int buildingRectMax = new Vector2Int(4, 2);
        [Range(0f, 1f)] public float buildingAdjacencyPreference = 0.75f;

        // ---- Debug ----
        [Header("Debug")]
        [Tooltip("When true, main road cells are left completely empty (no path tile, no floor) so their shapes are visible as holes.")]
        public bool debugEmptyMainRoads = true;
        [Tooltip("When true, alley cells are left completely empty (no path tile, no floor) so their shapes are visible as holes.")]
        public bool debugEmptyAlleys = true;

        // ---- Runtime wiring ----
        public NavMeshSurface navMeshSurface;
        public GameObject enemy;
        public List<Transform> spawnPoints;

        // ---- Internal state ----
        private readonly HashSet<Vector2Int> mainRoadCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> alleyCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> buildingCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> reservedMapFeatureCells = new HashSet<Vector2Int>();

        static readonly Vector2Int[] Cardinals =
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        void Start()
        {
            StartCoroutine(RunWFC());
        }

        IEnumerator RunWFC()
        {
            InitializeSets();
            PlaceMainRoads();
            PlaceBuildings();
            PlaceAlleys();
            PlaceFillers();
            InstantiateMapFeatures();
            SpawnOutsideTiles();
            UpdateSandBlendBounds();

            yield return null;

            if (navMeshSurface != null) navMeshSurface.BuildNavMesh();
            
            GameObject spawnTile = GameObject.FindWithTag("Spawn");
            Instantiate(playerPrefab, spawnTile.transform.position + Vector3.up * 5, spawnTile.transform.rotation);
            
            foreach (var t in spawnPoints)
            {
                GameObject clone = Instantiate(enemy, t.position, t.rotation);
                clone.GetComponent<EnemyAI>().Init(GameObject.FindWithTag("Player"));
            }
        }

        void InitializeSets()
        {
            mainRoadCells.Clear();
            alleyCells.Clear();
            buildingCells.Clear();
            reservedMapFeatureCells.Clear();
            foreach (var f in mapFeatures)
                reservedMapFeatureCells.Add(f.position);
        }

        // =====================================================================
        // Phase 1 — Main roads: 2-wide winding L-shapes originating from scattered
        // origin cells (not from the spawn tile).
        // =====================================================================
        void PlaceMainRoads()
        {
            Queue<Segment> frontier = new Queue<Segment>();

            // Partition the grid into ~mainRoadCount regions and pick one origin per region.
            // cols/rows chosen so partitions are roughly square given the grid's aspect.
            List<Vector2Int> origins = PickPartitionedOrigins(mainRoadCount);

            // Seed a segment at each origin, heading in a random cardinal direction.
            foreach (var origin in origins)
            {
                var dirs = ShuffleCardinals();
                foreach (var dir in dirs)
                {
                    Vector2Int start = origin;
                    if (!InGrid(start)) continue;
                    Vector2Int widthOff = RandomPerpendicular(dir);
                    if (!InGrid(start + widthOff)) widthOff = -widthOff;
                    if (!InGrid(start + widthOff)) continue;
                    frontier.Enqueue(new Segment(start, dir, widthOff,
                        RandomLen(mainRoadSegmentLengthMin, mainRoadSegmentLengthMax)));
                    break;
                }
            }

            int placed = 0;
            int safetyBudget = gridSizeX * gridSizeY * 4;
            while (frontier.Count > 0 && placed < maxMainRoadCells && safetyBudget-- > 0)
            {
                var s = frontier.Dequeue();
                Vector2Int cellA = s.pos;
                Vector2Int cellB = s.pos + s.widthOff;

                if (!InGrid(cellA) || !InGrid(cellB)) continue;
                if (reservedMapFeatureCells.Contains(cellA)
                    || reservedMapFeatureCells.Contains(cellB)) continue;

                bool aIsNew = !mainRoadCells.Contains(cellA);
                bool bIsNew = !mainRoadCells.Contains(cellB);
                if (!aIsNew && !bIsNew) continue; // merged back into existing road

                if (aIsNew)
                {
                    if (!debugEmptyMainRoads) PlacePathTileAt(cellA);
                    mainRoadCells.Add(cellA);
                    placed++;
                }
                if (bIsNew)
                {
                    if (!debugEmptyMainRoads) PlacePathTileAt(cellB);
                    mainRoadCells.Add(cellB);
                    placed++;
                }

                if (s.remaining > 1)
                {
                    frontier.Enqueue(new Segment(s.pos + s.dir, s.dir, s.widthOff, s.remaining - 1));
                }
                else
                {
                    float r = Random.value;
                    float threshDead = mainDeadEndProbability;
                    float threshTurn = threshDead + mainTurnProbability;
                    float threshFork = threshTurn + mainForkProbability;
                    if (r < threshDead)
                    {
                        // dead end — no enqueue
                    }
                    else if (r < threshTurn)
                    {
                        // 90° turn — reuse the current cell as the elbow corner,
                        // with the old travel direction as the new width offset.
                        // Pick the perpendicular with more empty cells ahead so the road
                        // doesn't fold back into already-placed territory.
                        Vector2Int newDir = BestTurnDirection(s.pos, s.dir);
                        Vector2Int newWidthOff = s.dir;
                        frontier.Enqueue(new Segment(s.pos, newDir, newWidthOff,
                            RandomLen(mainRoadSegmentLengthMin, mainRoadSegmentLengthMax)));
                    }
                    else if (r < threshFork)
                    {
                        // FORK — continue straight AND spawn a perpendicular branch from the current cell.
                        frontier.Enqueue(new Segment(s.pos + s.dir, s.dir, s.widthOff,
                            RandomLen(mainRoadSegmentLengthMin, mainRoadSegmentLengthMax)));
                        Vector2Int branchDir = BestTurnDirection(s.pos, s.dir);
                        Vector2Int branchWidthOff = s.dir;
                        frontier.Enqueue(new Segment(s.pos, branchDir, branchWidthOff,
                            RandomLen(mainRoadSegmentLengthMin, mainRoadSegmentLengthMax)));
                    }
                    else
                    {
                        frontier.Enqueue(new Segment(s.pos + s.dir, s.dir, s.widthOff,
                            RandomLen(mainRoadSegmentLengthMin, mainRoadSegmentLengthMax)));
                    }
                }
            }

            ConnectMainRoadClusters();
        }

        // Partition the grid into a rows × cols grid of rectangular regions and pick one
        // random origin cell inside each region. Partition count is the smallest rows × cols
        // grid that accommodates `n`, chosen so partition cells are roughly square.
        List<Vector2Int> PickPartitionedOrigins(int n)
        {
            var result = new List<Vector2Int>();
            if (n <= 0) return result;

            float aspect = gridSizeY > 0 ? (float)gridSizeX / gridSizeY : 1f;
            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n * aspect)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));

            // Enumerate partition (row, col) pairs and shuffle so we don't always favor top-left.
            var partitions = new List<Vector2Int>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    partitions.Add(new Vector2Int(c, r));
            for (int i = partitions.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (partitions[i], partitions[j]) = (partitions[j], partitions[i]);
            }

            foreach (var p in partitions)
            {
                if (result.Count >= n) break;
                int x0 = p.x * gridSizeX / cols;
                int x1 = (p.x + 1) * gridSizeX / cols;
                int y0 = p.y * gridSizeY / rows;
                int y1 = (p.y + 1) * gridSizeY / rows;
                if (x1 <= x0 || y1 <= y0) continue;

                Vector2Int? chosen = null;
                int tries = 12;
                while (tries-- > 0)
                {
                    Vector2Int cand = new Vector2Int(Random.Range(x0, x1), Random.Range(y0, y1));
                    if (reservedMapFeatureCells.Contains(cand)) continue;
                    chosen = cand;
                    break;
                }
                if (chosen.HasValue) result.Add(chosen.Value);
            }
            return result;
        }

        // Flood-fill mainRoadCells into connected components and draw 2-wide L-connectors
        // between the closest pairs until only one component remains.
        void ConnectMainRoadClusters()
        {
            int safety = Mathf.Max(4, mainRoadCount * 10);
            while (safety-- > 0)
            {
                var components = FloodFillMainRoadComponents();
                if (components.Count <= 1) return;

                // Find the closest pair of cells across any two components.
                int bestDist = int.MaxValue;
                Vector2Int bestA = components[0][0], bestB = components[1][0];
                for (int i = 0; i < components.Count; i++)
                {
                    for (int j = i + 1; j < components.Count; j++)
                    {
                        foreach (var ca in components[i])
                        {
                            foreach (var cb in components[j])
                            {
                                int d = Mathf.Abs(ca.x - cb.x) + Mathf.Abs(ca.y - cb.y);
                                if (d < bestDist)
                                {
                                    bestDist = d; bestA = ca; bestB = cb;
                                }
                            }
                        }
                    }
                }

                int before = mainRoadCells.Count;
                DrawLConnector(bestA, bestB);
                if (mainRoadCells.Count == before)
                {
                    Debug.LogWarning("WFCManager: could not reduce main-road component count; leaving some roads disconnected");
                    return;
                }
            }
        }

        List<List<Vector2Int>> FloodFillMainRoadComponents()
        {
            var result = new List<List<Vector2Int>>();
            var visited = new HashSet<Vector2Int>();
            foreach (var seed in mainRoadCells)
            {
                if (!visited.Add(seed)) continue;
                var component = new List<Vector2Int>();
                var stack = new Stack<Vector2Int>();
                stack.Push(seed);
                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    component.Add(cur);
                    foreach (var d in Cardinals)
                    {
                        var n = cur + d;
                        if (!mainRoadCells.Contains(n)) continue;
                        if (!visited.Add(n)) continue;
                        stack.Push(n);
                    }
                }
                result.Add(component);
            }
            return result;
        }

        // 2-wide L-shaped connector from `from` to `to`. Walks horizontally then vertically
        // (or vice versa, chosen randomly), placing each cell + one perpendicular companion.
        // Skips cells that are buildings or off-grid — connector can end up 1-wide in spots
        // rather than failing outright.
        void DrawLConnector(Vector2Int from, Vector2Int to)
        {
            bool horizFirst = Random.value < 0.5f;
            Vector2Int bend = horizFirst ? new Vector2Int(to.x, from.y) : new Vector2Int(from.x, to.y);

            WalkAndPlace(from, bend);
            WalkAndPlace(bend, to);
        }

        void WalkAndPlace(Vector2Int a, Vector2Int b)
        {
            if (a == b)
            {
                PlaceConnectorCell(a);
                return;
            }
            Vector2Int step = new Vector2Int(
                a.x == b.x ? 0 : (b.x > a.x ? 1 : -1),
                a.y == b.y ? 0 : (b.y > a.y ? 1 : -1));
            Vector2Int companion = step.x != 0 ? Vector2Int.up : Vector2Int.right;

            Vector2Int cur = a;
            PlaceConnectorCell(cur);
            PlaceConnectorCell(cur + companion);
            while (cur != b)
            {
                cur += step;
                PlaceConnectorCell(cur);
                PlaceConnectorCell(cur + companion);
            }
        }

        void PlaceConnectorCell(Vector2Int pos)
        {
            if (!InGrid(pos)) return;
            if (buildingCells.Contains(pos)) return;
            if (reservedMapFeatureCells.Contains(pos)) return;
            if (mainRoadCells.Contains(pos)) return;
            if (!debugEmptyMainRoads) PlacePathTileAt(pos);
            mainRoadCells.Add(pos);
        }

        // =====================================================================
        // Phase 2 — Buildings: deterministic wall layout, prefer main-road adjacency.
        // Adjacency mode picks a halo cell (cardinally adjacent to a main road) and
        // builds the rect outward from it so the rect is guaranteed to touch a road.
        // =====================================================================
        void PlaceBuildings()
        {
            List<(Vector2Int cell, Vector2Int towardRoad)> halo = BuildRoadHalo();

            int placed = 0;
            int safetyBudget = gridSizeX * gridSizeY * 4;
            while (placed < buildingSeedCount && safetyBudget-- > 0)
            {
                int w = Random.Range(buildingRectMin.x, buildingRectMax.x + 1);
                int h = Random.Range(buildingRectMin.y, buildingRectMax.y + 1);
                if (w > gridSizeX || h > gridSizeY) continue;

                int originX, originY;
                bool useAdjacency = halo.Count > 0 && Random.value < buildingAdjacencyPreference;

                if (useAdjacency)
                {
                    var pick = halo[Random.Range(0, halo.Count)];
                    if (!TryRectOriginFromHalo(pick.cell, pick.towardRoad, w, h, out originX, out originY))
                        continue;
                }
                else
                {
                    originX = Random.Range(0, gridSizeX - w + 1);
                    originY = Random.Range(0, gridSizeY - h + 1);
                }

                if (originX < 0 || originY < 0
                    || originX + w > gridSizeX || originY + h > gridSizeY) continue;

                List<Vector2Int> rectCells = new List<Vector2Int>();
                bool blocked = false;
                for (int dx = 0; dx < w && !blocked; dx++)
                {
                    for (int dy = 0; dy < h && !blocked; dy++)
                    {
                        Vector2Int p = new Vector2Int(originX + dx, originY + dy);
                        if (IsUsed(p) || reservedMapFeatureCells.Contains(p)) { blocked = true; break; }
                        //if (AdjacentToExistingBuilding(p)) { blocked = true; break; }
                        rectCells.Add(p);
                    }
                }
                if (blocked) continue;

                foreach (var p in rectCells)
                {
                    int dx = p.x - originX;
                    int dy = p.y - originY;
                    PlaceBuildingCell(p, dx, dy, w, h);
                    buildingCells.Add(p);
                    if (floorTilePrefab != null)
                    {
                        Vector3 fp = GridToWorld(p);
                        Instantiate(floorTilePrefab, fp + Vector3.up * 0.01f,
                            floorTilePrefab.transform.rotation, transform);
                    }
                }
                placed++;
            }
        }

        // Cells cardinally adjacent to a main road cell (and not themselves road).
        // Each entry also records which direction points back toward the road,
        // so we can orient building rects with their road-facing edge on the halo cell.
        List<(Vector2Int, Vector2Int)> BuildRoadHalo()
        {
            var result = new List<(Vector2Int, Vector2Int)>();
            var seen = new HashSet<Vector2Int>();
            foreach (var road in mainRoadCells)
            {
                foreach (var d in Cardinals)
                {
                    Vector2Int neighbor = road + d;
                    if (!InGrid(neighbor)) continue;
                    if (mainRoadCells.Contains(neighbor)) continue;
                    if (reservedMapFeatureCells.Contains(neighbor)) continue;
                    // towardRoad from neighbor's perspective is -d
                    if (seen.Add(neighbor))
                        result.Add((neighbor, -d));
                }
            }
            return result;
        }

        // Given a halo cell and the direction from it toward the road, compute rect origin so
        // that the halo cell sits on the rect's road-facing edge, with a random offset along
        // that edge. Returns false if the resulting rect would go out of bounds.
        bool TryRectOriginFromHalo(Vector2Int halo, Vector2Int towardRoad, int w, int h,
            out int originX, out int originY)
        {
            if (towardRoad == Vector2Int.up)
            {
                // Road is north of halo → rect's top (posZ) edge hosts the halo cell; extend south.
                int offsetX = Random.Range(0, w);
                originX = halo.x - offsetX;
                originY = halo.y - h + 1;
            }
            else if (towardRoad == Vector2Int.down)
            {
                int offsetX = Random.Range(0, w);
                originX = halo.x - offsetX;
                originY = halo.y;
            }
            else if (towardRoad == Vector2Int.right)
            {
                int offsetY = Random.Range(0, h);
                originX = halo.x - w + 1;
                originY = halo.y - offsetY;
            }
            else // Vector2Int.left
            {
                int offsetY = Random.Range(0, h);
                originX = halo.x;
                originY = halo.y - offsetY;
            }
            return originX >= 0 && originY >= 0
                && originX + w <= gridSizeX && originY + h <= gridSizeY;
        }

        bool AdjacentToExistingBuilding(Vector2Int pos)
        {
            foreach (var d in Cardinals)
                if (buildingCells.Contains(pos + d)) return true;
            return false;
        }

        void PlaceBuildingCell(Vector2Int pos, int dx, int dy, int w, int h)
        {
            // Which cardinal directions point outside the rect (including off-grid)?
            // Indices: 0 = posZ, 1 = posX, 2 = negZ, 3 = negX
            List<int> outsideDirs = new List<int>();
            if (dy == h - 1) outsideDirs.Add(0);
            if (dx == w - 1) outsideDirs.Add(1);
            if (dy == 0) outsideDirs.Add(2);
            if (dx == 0) outsideDirs.Add(3);

            TileDefinition def;
            int rotation;

            switch (outsideDirs.Count)
            {
                case 1:
                {
                    
                    def = RandomFrom(edgeWallDefinitions);
                    int baseClosed = FindBaseClosedDirection(def);
                    rotation = (outsideDirs[0] - baseClosed + 4) % 4;
                    break;
                }
                case 2:
                {
                    int a = outsideDirs[0], b = outsideDirs[1];
                    bool aNextB = (a + 1) % 4 == b;
                    bool bNextA = (b + 1) % 4 == a;
                    if (aNextB || bNextA)
                    {
                        def = RandomFrom(cornerWallDefinitions);
                        int targetCorner = aNextB ? a : b;
                        int baseCorner = FindBaseClosedCornerIndex(def);
                        rotation = (targetCorner - baseCorner + 4) % 4;
                    }
                    else
                    {
                        // Opposite-axis outside pair (thin middle of 1×N). No tile fits; leave empty.
                        return;
                    }
                    break;
                }
                // 0 outside = interior cell, intentionally empty.
                // 3/4 outside = end-cap / isolated 1×1, no suitable tile — leave empty.
                default:
                    return;
            }

            if (def == null || def.prefab == null) return;

            Vector3 worldPos = GridToWorld(pos);
            Quaternion rot = Quaternion.Euler(0, rotation * 90, 0) * def.prefab.transform.rotation;
            Instantiate(def.prefab, worldPos, rot, transform);
        }

        // =====================================================================
        // Phase 3 — Alleys: 1-wide branches off main roads + 1-cell gap fills.
        // =====================================================================
        void PlaceAlleys()
        {
            Queue<Segment> frontier = new Queue<Segment>();

            // Source A: branches off main roads.
            foreach (var cell in new List<Vector2Int>(mainRoadCells))
            {
                if (Random.value > alleyFromMainRoadChance) continue;
                foreach (var d in ShuffleCardinals())
                {
                    Vector2Int next = cell + d;
                    if (!InGrid(next) || IsUsed(next) || reservedMapFeatureCells.Contains(next)) continue;
                    frontier.Enqueue(new Segment(next, d, Vector2Int.zero,
                        RandomLen(alleySegmentLengthMin, alleySegmentLengthMax)));
                    break;
                }
            }

            // Source B: 1-cell gaps between adjacent buildings.
            for (int x = 0; x < gridSizeX; x++)
            {
                for (int y = 0; y < gridSizeY; y++)
                {
                    Vector2Int p = new Vector2Int(x, y);
                    if (IsUsed(p) || reservedMapFeatureCells.Contains(p)) continue;

                    bool horizGap = buildingCells.Contains(p + Vector2Int.left)
                                    && buildingCells.Contains(p + Vector2Int.right);
                    bool vertGap = buildingCells.Contains(p + Vector2Int.up)
                                   && buildingCells.Contains(p + Vector2Int.down);

                    if (horizGap)
                        frontier.Enqueue(new Segment(p, Vector2Int.up, Vector2Int.zero,
                            RandomLen(alleySegmentLengthMin, alleySegmentLengthMax)));
                    else if (vertGap)
                        frontier.Enqueue(new Segment(p, Vector2Int.right, Vector2Int.zero,
                            RandomLen(alleySegmentLengthMin, alleySegmentLengthMax)));
                }
            }

            int placed = 0;
            int safetyBudget = gridSizeX * gridSizeY * 4;
            while (frontier.Count > 0 && placed < maxAlleyCells && safetyBudget-- > 0)
            {
                var s = frontier.Dequeue();
                if (!InGrid(s.pos)) continue;
                if (IsUsed(s.pos) || reservedMapFeatureCells.Contains(s.pos)) continue;

                if (!debugEmptyAlleys) PlacePathTileAt(s.pos);
                alleyCells.Add(s.pos);
                placed++;

                if (s.remaining > 1)
                {
                    frontier.Enqueue(new Segment(s.pos + s.dir, s.dir, Vector2Int.zero, s.remaining - 1));
                }
                else
                {
                    float r = Random.value;
                    if (r < alleyDeadEndProbability)
                    {
                        // dead end
                    }
                    else if (r < alleyDeadEndProbability + alleyBranchProbability)
                    {
                        var (p1, p2) = Perpendiculars(s.dir);
                        frontier.Enqueue(new Segment(s.pos + p1, p1, Vector2Int.zero,
                            RandomLen(alleySegmentLengthMin, alleySegmentLengthMax)));
                        frontier.Enqueue(new Segment(s.pos + p2, p2, Vector2Int.zero,
                            RandomLen(alleySegmentLengthMin, alleySegmentLengthMax)));
                    }
                    else
                    {
                        Vector2Int perp = RandomPerpendicular(s.dir);
                        frontier.Enqueue(new Segment(s.pos + perp, perp, Vector2Int.zero,
                            RandomLen(alleySegmentLengthMin, alleySegmentLengthMax)));
                    }
                }
            }
        }

        // =====================================================================
        // Phase 4 — Fillers: prop tiles in every leftover cell.
        // =====================================================================
        void PlaceFillers()
        {
            if (fillerTileDefinitions == null || fillerTileDefinitions.Count == 0) return;
            int totalWeight = fillerTileDefinitions.Sum(t => t.spawnWeight);
            if (totalWeight <= 0) return;

            for (int x = 0; x < gridSizeX; x++)
            {
                for (int y = 0; y < gridSizeY; y++)
                {
                    Vector2Int p = new Vector2Int(x, y);
                    if (IsUsed(p) || reservedMapFeatureCells.Contains(p)) continue;

                    int rand = Random.Range(0, totalWeight);
                    int cur = 0;
                    TileDefinition chosen = fillerTileDefinitions[0];
                    foreach (var t in fillerTileDefinitions)
                    {
                        cur += t.spawnWeight;
                        if (rand < cur) { chosen = t; break; }
                    }

                    int rot = Random.Range(0, 4);
                    Vector3 worldPos = GridToWorld(p);
                    Quaternion quat = Quaternion.Euler(0, rot * 90, 0) * chosen.prefab.transform.rotation;
                    Instantiate(chosen.prefab, worldPos, quat, transform);
                    if (floorTilePrefab != null)
                        Instantiate(floorTilePrefab, worldPos + Vector3.up * 0.01f,
                            floorTilePrefab.transform.rotation, transform);
                }
            }
        }

        // =====================================================================
        // Map features (spawn tile and any other manual placements).
        // =====================================================================
        void InstantiateMapFeatures()
        {
            foreach (var f in mapFeatures)
            {
                if (f.tile == null || f.tile.prefab == null) continue;
                Vector3 worldPos = GridToWorld(f.position);
                Quaternion quat = Quaternion.Euler(0, f.rotation * 90, 0) * f.tile.prefab.transform.rotation;
                Instantiate(f.tile.prefab, worldPos, quat, transform);
                if (floorTilePrefab != null)
                    Instantiate(floorTilePrefab, worldPos + Vector3.up * 0.01f,
                        floorTilePrefab.transform.rotation, transform);
            }
        }

        // =====================================================================
        // Environment (unchanged from previous pipeline).
        // =====================================================================
        void UpdateSandBlendBounds()
        {
            if (sandBlendMaterial == null) return;
            float halfTile = tileSize * 0.5f;
            sandBlendMaterial.SetFloat("_GridMinX", -halfTile);
            sandBlendMaterial.SetFloat("_GridMinZ", -halfTile);
            sandBlendMaterial.SetFloat("_GridMaxX", (gridSizeX - 1) * tileSize + halfTile);
            sandBlendMaterial.SetFloat("_GridMaxZ", (gridSizeY - 1) * tileSize + halfTile);
        }

        void SpawnOutsideTiles()
        {
            if (outsideTilePrefab == null) return;
            for (int x = -outsideRadius; x < gridSizeX + outsideRadius; x++)
            {
                for (int y = -outsideRadius; y < gridSizeY + outsideRadius; y++)
                {
                    if (x >= 0 && x < gridSizeX && y >= 0 && y < gridSizeY) continue;
                    Instantiate(outsideTilePrefab,
                        new Vector3(x * tileSize, 0, y * tileSize),
                        outsideTilePrefab.transform.rotation, transform);
                }
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        void PlacePathTileAt(Vector2Int pos)
        {
            if (pathTileDefinition == null || pathTileDefinition.prefab == null) return;
            Vector3 worldPos = GridToWorld(pos);
            Instantiate(pathTileDefinition.prefab, worldPos,
                pathTileDefinition.prefab.transform.rotation, transform);
            if (floorTilePrefab != null)
                Instantiate(floorTilePrefab, worldPos + Vector3.up * 0.01f,
                    floorTilePrefab.transform.rotation, transform);
        }

        Vector3 GridToWorld(Vector2Int p) => new Vector3(p.x * tileSize, 0, p.y * tileSize);

        bool InGrid(Vector2Int p) => p.x >= 0 && p.x < gridSizeX && p.y >= 0 && p.y < gridSizeY;

        bool IsUsed(Vector2Int p)
            => mainRoadCells.Contains(p) || alleyCells.Contains(p) || buildingCells.Contains(p);

        Vector2Int RandomPerpendicular(Vector2Int d)
        {
            if (d.x != 0) return Random.value < 0.5f ? Vector2Int.up : Vector2Int.down;
            return Random.value < 0.5f ? Vector2Int.right : Vector2Int.left;
        }

        // At a main-road turn, pick the perpendicular direction with more empty cells ahead
        // so the road doesn't fold back into itself.
        Vector2Int BestTurnDirection(Vector2Int fromPos, Vector2Int currentDir)
        {
            var (perp1, perp2) = Perpendiculars(currentDir);
            int score1 = EmptyCellsAhead(fromPos, perp1, 4);
            int score2 = EmptyCellsAhead(fromPos, perp2, 4);
            if (score1 > score2) return perp1;
            if (score2 > score1) return perp2;
            return Random.value < 0.5f ? perp1 : perp2;
        }

        int EmptyCellsAhead(Vector2Int fromPos, Vector2Int dir, int lookahead)
        {
            int count = 0;
            for (int i = 1; i <= lookahead; i++)
            {
                Vector2Int c = fromPos + dir * i;
                if (!InGrid(c)) break;
                if (mainRoadCells.Contains(c)) break;
                count++;
            }
            return count;
        }

        (Vector2Int, Vector2Int) Perpendiculars(Vector2Int d)
        {
            if (d.x != 0) return (Vector2Int.up, Vector2Int.down);
            return (Vector2Int.right, Vector2Int.left);
        }

        List<Vector2Int> ShuffleCardinals()
        {
            var list = new List<Vector2Int>(Cardinals);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
            return list;
        }

        int RandomLen(int min, int max) => Random.Range(min, max + 1);

        T RandomFrom<T>(List<T> list) => list[Random.Range(0, list.Count)];

        // For an edge-wall tile (exactly one Closed edge at rotation 0), return its direction index
        // (0 = posZ, 1 = posX, 2 = negZ, 3 = negX). If multiple closed edges, returns the first found.
        static int FindBaseClosedDirection(TileDefinition def)
        {
            if (def.posZ == TileDefinition.EdgeType.Closed) return 0;
            if (def.posX == TileDefinition.EdgeType.Closed) return 1;
            if (def.negZ == TileDefinition.EdgeType.Closed) return 2;
            if (def.negX == TileDefinition.EdgeType.Closed) return 3;
            return 0;
        }

        // For a corner-wall tile (two perpendicular Closed edges at rotation 0), return the "a" index
        // such that the closed pair is {a, (a + 1) % 4}:
        //   {posZ, posX} NE → 0
        //   {posX, negZ} SE → 1
        //   {negZ, negX} SW → 2
        //   {negX, posZ} NW → 3
        static int FindBaseClosedCornerIndex(TileDefinition def)
        {
            bool pZ = def.posZ == TileDefinition.EdgeType.Closed;
            bool pX = def.posX == TileDefinition.EdgeType.Closed;
            bool nZ = def.negZ == TileDefinition.EdgeType.Closed;
            bool nX = def.negX == TileDefinition.EdgeType.Closed;

            if (pZ && pX) return 0;
            if (pX && nZ) return 1;
            if (nZ && nX) return 2;
            if (nX && pZ) return 3;
            return 0;
        }
    }
}
