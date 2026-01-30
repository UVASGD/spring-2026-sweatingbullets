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

        public EdgeType north;
        public EdgeType east;
        public EdgeType south;
        public EdgeType west;


    }

}
