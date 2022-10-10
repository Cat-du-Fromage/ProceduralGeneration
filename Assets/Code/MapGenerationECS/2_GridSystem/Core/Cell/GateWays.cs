using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using static KWZTerrainECS.Utilities;

namespace KWZTerrainECS
{
    public readonly struct GateWay
    {
        public readonly int ChunkIndex;
        public readonly Sides Side;
        public readonly int Index;
        public readonly int IndexAdjacent;
        
        public GateWay(int chunkIndex, Sides side, int index = -1, int indexAdj = -1)
        {
            ChunkIndex = chunkIndex;
            Side = side;
            Index = index;
            IndexAdjacent = indexAdj;
        }

        public override string ToString()
        {
            return $"Gate in chunk : {Index}; adjacent: {IndexAdjacent}";
        }
    }
}
