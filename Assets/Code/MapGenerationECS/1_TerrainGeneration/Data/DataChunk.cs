using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KWZTerrainECS
{
    public struct DataChunk : IComponentData
    {
        public int NumQuadPerLine;
        public int QuadsCount;
        public int NumVerticesPerLine;
        public int VerticesCount;
        public int TrianglesCount;
        public int TriangleIndicesCount;
        /*
        public static implicit operator DataChunk(ChunkSettings chunk)
        {
            return new DataChunk
            {
                NumQuadPerLine = chunk.NumQuadPerLine,
                QuadsCount = chunk.QuadsCount,
                NumVerticesPerLine = chunk.NumVertexPerLine,
                VerticesCount = chunk.VerticesCount,
                TrianglesCount = chunk.TrianglesCount,
                TriangleIndicesCount = chunk.TriangleIndicesCount
            };
        }
        */
    }
}