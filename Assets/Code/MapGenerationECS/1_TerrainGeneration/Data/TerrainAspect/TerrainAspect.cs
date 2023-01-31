using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace KWZTerrainECS
{
    public struct TerrainAspectStruct
    {
        public readonly DataTerrain Terrain;
        public readonly DataChunk Chunk;
        public readonly DataNoise Noise;

        public TerrainAspectStruct(TerrainAspect aspect)
        {
            Terrain = aspect.Terrain;
            Chunk = aspect.Chunk;
            Noise = aspect.Noise;
        }

        public readonly int NumChunks => Terrain.NumChunksXY.x * Terrain.NumChunksXY.y;
        public readonly int2 NumChunksXY => Terrain.NumChunksXY;
        public readonly int ChunkNumQuadsPerLine => Chunk.NumQuadPerLine;
        public readonly int ChunkVerticesCount => Chunk.VerticesCount;
        public readonly int ChunkTriangleIndicesCount => Chunk.TriangleIndicesCount;
        
    }
    
    public readonly partial struct TerrainAspect : IAspect
    {
        private readonly RefRO<DataTerrain> DataTerrain;
        private readonly RefRO<DataChunk> DataChunk;
        [Optional]
        private readonly RefRO<DataNoise> DataNoise;
        
        [CreateProperty]
        public DataTerrain Terrain
        {
            get => DataTerrain.ValueRO;
            //set => DataTerrain.ValueRW = value;
        }
        
        [CreateProperty]
        public DataChunk Chunk
        {
            get => DataChunk.ValueRO;
            //set => DataChunk.ValueRW = value;
        }
        
        [CreateProperty]
        public DataNoise Noise
        {
            get => DataNoise.ValueRO;
            //set => DataNoise.ValueRW = value;
        }
        
        /// <summary>Get Total number of chunks in the terrain.</summary>
        public int NumChunks
        {
            get => DataTerrain.ValueRO.NumChunksXY.x * DataTerrain.ValueRO.NumChunksXY.y;
        }

        /// <summary>Get Rapid Access to chunk size.</summary>
        public int ChunkNumQuadPerLine
        {
            get => DataChunk.ValueRO.NumQuadPerLine;
        }

        /// <summary>Get Rapid Access to Number chunks on X/Y.</summary>
        public int2 NumChunkXY
        {
            get => DataTerrain.ValueRO.NumChunksXY;
        }

        public int ChunkNumVertices
        {
            get => DataChunk.ValueRO.VerticesCount;
        }

        public int ChunkNumTriangleIndices
        {
            get => DataChunk.ValueRO.TriangleIndicesCount;
        }
        
    }
}
