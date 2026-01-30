using System.Collections.Generic;
using UnityEngine;

namespace Tiles
{
    public class WFCcell
    {
        public Vector2Int position;

        //public List<TileDefinition> possibleTiles; // "Superposition"
        public List<OrientedTile> possibleTiles;
        public bool isCollapsed = false;

        public WFCcell(Vector2Int pos, List<TileDefinition> allTiles)
        {
            position = pos;
            // Start with every tile being possible
            possibleTiles = new List<OrientedTile>();

            foreach (var def in allTiles)
            {
                for (int i = 0; i < 4; i++) // creating four rotated variants for every tile definition
                {
                    possibleTiles.Add(new OrientedTile(def, i));
                }
            }
        }
    }
}