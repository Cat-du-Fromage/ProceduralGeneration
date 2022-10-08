using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace KWZTerrainECS
{
    public struct DataNoise : IComponentData
    {
        public uint Seed;
        public int Octaves;
        public float Lacunarity;
        public float Persistence;
        public float Scale;
        public float HeightMultiplier;
        public float2 Offset;
        
        public static implicit operator NoiseSettingsData(DataNoise data)
        {
            return new NoiseSettingsData
            {
                Octaves = data.Octaves,
                Lacunarity = data.Lacunarity,
                Persistence = data.Persistence,
                Scale = data.Scale,
                HeightMultiplier = data.HeightMultiplier,
            };
        }
    }
    
    //USE FOR JOB
    public struct NoiseSettingsData
    {
        public int Octaves;
        public float Lacunarity;
        public float Persistence;
        public float Scale;
        public float HeightMultiplier;
    }
}
