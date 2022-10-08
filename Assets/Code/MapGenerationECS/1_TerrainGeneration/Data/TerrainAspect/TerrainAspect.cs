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
    }
    
    public readonly partial struct TerrainAspect : IAspect
    {
        private readonly RefRO<DataTerrain> DataTerrain;
        private readonly RefRO<DataChunk> DataChunk;
        [Optional]
        private readonly RefRO<DataNoise> DataNoise;
        
        [CreateProperty]
        public readonly DataTerrain Terrain
        {
            get => DataTerrain.ValueRO;
            //set => DataTerrain.ValueRW = value;
        }
        
        [CreateProperty]
        public readonly DataChunk Chunk
        {
            get => DataChunk.ValueRO;
            //set => DataChunk.ValueRW = value;
        }
        
        [CreateProperty]
        public readonly DataNoise Noise
        {
            get => DataNoise.ValueRO;
            //set => DataNoise.ValueRW = value;
        }
    }
}
