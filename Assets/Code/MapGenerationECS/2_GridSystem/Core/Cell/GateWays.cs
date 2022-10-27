using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using static KWZTerrainECS.Utilities;

namespace KWZTerrainECS
{
    public struct GateWay
    {
        public readonly int ChunkIndex;
        public readonly ESides Side;
        public readonly int ChunkCellIndex;
        public readonly int GridCellIndex;
        public readonly int AdjacentGridCellIndex;
        
        public GateWay(int chunkIndex, ESides side, int chunkCellIndex = -1, int gridCellIndex = -1, int adjacentGridCellIndexAdj = -1)
        {
            ChunkIndex = chunkIndex;
            Side = side;
            ChunkCellIndex = chunkCellIndex;
            GridCellIndex = gridCellIndex;
            AdjacentGridCellIndex = adjacentGridCellIndexAdj;
        }

        public override string ToString()
        {
            return $"Gate in chunk : {GridCellIndex}; adjacent: {AdjacentGridCellIndex}";
        }
    }
}
