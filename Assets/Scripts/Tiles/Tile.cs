using UnityEngine;

namespace Tiles
{
    public class Tile : MonoBehaviour
    {
        public Vector2Int gridPosition;
        public TileDefinition definition;

        public Tile north;
        public Tile east;
        public Tile south;
        public Tile west;
    }

    [System.Serializable]
    public struct OrientedTile
    {
        public TileDefinition definition;
        public int rotationIndex; // 0 = 0, 1 = 90, 2 = 180, 3 = 270

        public OrientedTile(TileDefinition def, int rot)
        {
            definition = def;
            rotationIndex = rot;
        }

        // This helper gets the edge type considering the rotation
        public TileDefinition.EdgeType GetEdge(int directionIndex)
        {
            // 0=North, 1=East, 2=South, 3=West
            // We rotate the requested direction "backwards" by our current rotation to find the original edge
            // E.g. If rotated 90 (index 1), and we ask for North (0), we actually need the original West (3) side.
            int originalSideIndex = (directionIndex - rotationIndex + 4) % 4;

            switch (originalSideIndex)
            {
                case 0: return definition.north;
                case 1: return definition.east;
                case 2: return definition.south;
                case 3: return definition.west;
                default: return TileDefinition.EdgeType.Closed;
            }
        }
    }
}