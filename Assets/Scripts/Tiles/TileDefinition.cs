using UnityEngine;

namespace Tiles
{
    [CreateAssetMenu(menuName = "ScriptableObjects/TileDefinition")]

    public class TileDefinition : ScriptableObject
    {
        public GameObject prefab;

        [Tooltip("Higher number = more likely to spawn")]
        public int spawnWeight = 10;

        public enum EdgeType
        {
            Open,
            Closed
        }

        // Direction index mapping (used by OrientedTile.GetEdge and WFCManager.GetDirIndex):
        //   0 = posZ  (Vector2Int.up    / world +Z / forward)
        //   1 = posX  (Vector2Int.right / world +X / right)
        //   2 = negZ  (Vector2Int.down  / world -Z / back)
        //   3 = negX  (Vector2Int.left  / world -X / left)
        public EdgeType posZ;
        public EdgeType posX;
        public EdgeType negZ;
        public EdgeType negX;

        public enum TileCategory
        {
            Open,
            Building,
            Transition
        }

        [Tooltip("Cluster group. WFC biases tiles of the same category to group together.")]
        public TileCategory category = TileCategory.Open;
    }

}
