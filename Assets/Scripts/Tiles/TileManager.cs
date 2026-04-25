using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace Tiles
{
    public class TileManager : MonoBehaviour
    {
        // TODO: PLace wall tiles. 
        private Dictionary<Vector2Int, Tile> _tiles;

        // Track where we might want to place the next tile
        private HashSet<Vector2Int> _availableSpaces;

        public List<TileDefinition> allTileDefinitions;
        public TileDefinition wallTileDefinition;
        public int tileSize = 5;

        [SerializeField] private int maxTiles = 15; // Stop after this many tiles
        [SerializeField] private int gridSizeX = 5; // Hard bounds
        [SerializeField] private int gridSizeY = 5;

        public enum Direction
        {
            PosZ,
            PosX,
            NegZ,
            NegX
        }

        void Start()
        {
            GenerateGrid();
            // PlaceWalls();
        }

        void GenerateGrid()
        {
            _tiles = new Dictionary<Vector2Int, Tile>();
            _availableSpaces = new HashSet<Vector2Int>();

            // 1. Place the first tile at (0,0)
            Vector2Int startPos = Vector2Int.zero;
            // Ideally, have a specific "Start Room" definition, but we'll just pick a random valid one
            PlaceTile(PickTileForPosition(startPos), startPos);

            // 2. Loop until we hit max tiles or run out of space
            int iterations = 0;
            while (_tiles.Count < maxTiles && _availableSpaces.Count > 0 && iterations < 1000)
            {
                // Pick a random position from the frontier
                Vector2Int posToBuild = _availableSpaces.ElementAt(Random.Range(0, _availableSpaces.Count));

                // Check boundaries (optional)
                if (posToBuild.x >= 0 && posToBuild.x < gridSizeX && posToBuild.y >= 0 && posToBuild.y < gridSizeY)
                {
                    TileDefinition bestTile = PickTileForPosition(posToBuild);

                    if (bestTile != null)
                    {
                        PlaceTile(bestTile, posToBuild);
                    }
                    else
                    {
                        // No tile fits here (cornered ourselves), remove from frontier
                        _availableSpaces.Remove(posToBuild);
                    }
                }
                else
                {
                    _availableSpaces.Remove(posToBuild);
                }

                iterations++;
            }
        }

        TileDefinition PickTileForPosition(Vector2Int cell)
        {
            // 1. Find all hard-compatible tiles
            var candidates = allTileDefinitions
                .Where(t => MatchesAllNeighbors(t, cell))
                .ToList();

            if (candidates.Count == 0) return null;

            // 2. Weighted Random Selection
            int totalWeight = candidates.Sum(t => t.spawnWeight);
            int randomValue = Random.Range(0, totalWeight);
            int currentSum = 0;

            foreach (var tile in candidates)
            {
                currentSum += tile.spawnWeight;
                if (randomValue < currentSum)
                {
                    return tile;
                }
            }

            return candidates[0]; // Fallback
        }

        void PlaceTile(TileDefinition def, Vector2Int cell)
        {
            GameObject tileObj = Instantiate(def.prefab, new Vector3(cell.x * tileSize, 0, cell.y * tileSize),
                def.prefab.transform.rotation);
            Tile tileComponent = tileObj.GetComponent<Tile>();
            tileComponent.definition = def;
            tileComponent.gridPosition = cell;

            _tiles.Add(cell, tileComponent);

            // Remove this cell from available spaces since it's now occupied
            _availableSpaces.Remove(cell);

            // Add Neighbors to the Frontier
            UpdateFrontier(cell + Vector2Int.up);
            UpdateFrontier(cell + Vector2Int.down);
            UpdateFrontier(cell + Vector2Int.right);
            UpdateFrontier(cell + Vector2Int.left);

            // Link neighbors (your existing logic, slightly compressed)
            LinkNeighbor(tileComponent, cell + Vector2Int.up, Direction.PosZ);
            LinkNeighbor(tileComponent, cell + Vector2Int.down, Direction.NegZ);
            LinkNeighbor(tileComponent, cell + Vector2Int.right, Direction.PosX);
            LinkNeighbor(tileComponent, cell + Vector2Int.left, Direction.NegX);
        }

        void UpdateFrontier(Vector2Int pos)
        {
            if (!_tiles.ContainsKey(pos))
            {
                _availableSpaces.Add(pos);
            }
        }

        void LinkNeighbor(Tile tile, Vector2Int neighborPos, Direction dir)
        {
            if (_tiles.TryGetValue(neighborPos, out Tile neighbor))
            {
                switch (dir)
                {
                    case Direction.PosZ:
                        tile.posZ = neighbor;
                        neighbor.negZ = tile;
                        break;
                    case Direction.NegZ:
                        tile.negZ = neighbor;
                        neighbor.posZ = tile;
                        break;
                    case Direction.PosX:
                        tile.posX = neighbor;
                        neighbor.negX = tile;
                        break;
                    case Direction.NegX:
                        tile.negX = neighbor;
                        neighbor.posX = tile;
                        break;
                }
            }
        }

        bool MatchesAllNeighbors(TileDefinition def, Vector2Int cell)
        {
            int score = 0;
            // Check +Z
            if (_tiles.TryGetValue(cell + Vector2Int.up, out Tile posZTile) &&
                !HardCompatible(def, posZTile.definition, Direction.PosZ)) score++;

            // Check -Z
            if (_tiles.TryGetValue(cell + Vector2Int.down, out Tile negZTile) &&
                !HardCompatible(def, negZTile.definition, Direction.NegZ)) score++;

            // Check +X
            if (_tiles.TryGetValue(cell + Vector2Int.right, out Tile posXTile) &&
                !HardCompatible(def, posXTile.definition, Direction.PosX)) score++;

            // Check -X
            if (_tiles.TryGetValue(cell + Vector2Int.left, out Tile negXTile) &&
                !HardCompatible(def, negXTile.definition, Direction.NegX)) score++;

            return score <= 1;
        }

        bool HardCompatible(TileDefinition a, TileDefinition b, Direction dir)
        {
            // Edge on `a` side facing `dir` must match the opposite edge on `b`.
            if (dir == Direction.PosX) return a.posX == b.negX;
            if (dir == Direction.NegZ) return a.negZ == b.posZ;
            if (dir == Direction.NegX) return a.negX == b.posX;
            if (dir == Direction.PosZ) return a.posZ == b.negZ;
            return false;
        }

        void PlaceWalls()
        {
            // Iterate through every tile we have placed
            foreach (var kvp in _tiles)
            {
                Vector2Int gridPos = kvp.Key;
                Tile tile = kvp.Value;

                Vector3 tileWorldPos = new Vector3(gridPos.x * tileSize, 0, gridPos.y * tileSize);

                // Check +Z
                if (ShouldPlaceWall(gridPos + Vector2Int.up, tile.definition.posZ))
                {
                    SpawnWall(tileWorldPos, Vector3.forward);
                }

                // Check -Z
                if (ShouldPlaceWall(gridPos + Vector2Int.down, tile.definition.negZ))
                {
                    SpawnWall(tileWorldPos, Vector3.back);
                }

                // Check +X
                if (ShouldPlaceWall(gridPos + Vector2Int.right, tile.definition.posX))
                {
                    SpawnWall(tileWorldPos, Vector3.right);
                }

                // Check -X
                if (ShouldPlaceWall(gridPos + Vector2Int.left, tile.definition.negX))
                {
                    SpawnWall(tileWorldPos, Vector3.left);
                }
            }
        }

        bool ShouldPlaceWall(Vector2Int neighborPos, TileDefinition.EdgeType edgeType)
        {
            // 1. If the edge is already "Closed" in the definition, we usually don't need a wall (the mesh likely has a wall there).
            if (edgeType == TileDefinition.EdgeType.Closed) return false;

            // 2. Only place a wall if there is NO tile at the neighbor position
            if (_tiles.ContainsKey(neighborPos)) return false;

            // 3. It is Open and leads to Void -> Place Wall
            return true;
        }

        void SpawnWall(Vector3 tileCenter, Vector3 direction)
        {
            Vector3 wallPos = tileCenter + (direction * tileSize);
            GameObject wall = Instantiate(wallTileDefinition.prefab, wallPos, Quaternion.identity);
        }

    }
}