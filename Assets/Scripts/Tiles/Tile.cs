using UnityEngine;

namespace Tiles
{
    public class Tile : MonoBehaviour
    {
        public Vector2Int gridPosition;
        public TileDefinition definition;

        public Tile posZ;
        public Tile posX;
        public Tile negZ;
        public Tile negX;
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
            // 0 = posZ (forward), 1 = posX (right), 2 = negZ (back), 3 = negX (left)
            // We rotate the requested direction "backwards" by our current rotation to find the original edge.
            // E.g. if rotated 90 (index 1) and we ask for posZ (0), the original negX (3) side now faces posZ.
            int originalSideIndex = (directionIndex - rotationIndex + 4) % 4;

            switch (originalSideIndex)
            {
                case 0: return definition.posZ;
                case 1: return definition.posX;
                case 2: return definition.negZ;
                case 3: return definition.negX;
                default: return TileDefinition.EdgeType.Closed;
            }
        }
    }
}