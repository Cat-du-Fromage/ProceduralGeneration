using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KWZTerrainECS
{
    public struct DataTerrain : IComponentData
    {
        public int2 NumChunksXY;
        public int2 NumQuadsXY;
        public int2 NumVerticesXY;
    }
}