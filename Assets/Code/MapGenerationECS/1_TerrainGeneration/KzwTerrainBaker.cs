using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Jobs;
using static Unity.Mathematics.math;

using static Unity.Jobs.LowLevel.Unsafe.JobsUtility;
using static KWZTerrainECS.Utilities;
using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;

namespace KWZTerrainECS
{
    public class KzwTerrainBaker : MonoBehaviour
    {
        [field:SerializeField] public Material ChunkMaterial{ get; private set; }
        [field:SerializeField] public TerrainSettings TerrainSettings { get; private set; }
        //[field:SerializeField] public SpawnSettings SpawnSettings { get; private set; }

        private class TerrainAuthoring : Baker<KzwTerrainBaker>
        {
            public override void Bake(KzwTerrainBaker authoring)
            {
                if (authoring.ChunkMaterial == null || authoring.TerrainSettings == null) return;
                DependsOn(authoring.TerrainSettings);
                DependsOn(authoring.ChunkMaterial);
                //DependsOn(authoring.SpawnSettings);
                
                DynamicBuffer<BufferChunk> chunksBuffer = AddBuffer<BufferChunk>();
                chunksBuffer.EnsureCapacity(authoring.TerrainSettings.ChunksCount);

                AddComponent<TagUnInitializeTerrain>();
                AddComponent(new PrefabChunk() { Value = GetEntity(authoring.TerrainSettings.ChunkSettings.Prefab) });
                AddComponentObject(new ObjMaterialTerrain(){Value = authoring.ChunkMaterial});
                
                AddTerrainAspect();
                
                // Internal Methods
                // ===================================================================================================
                void AddTerrainAspect()
                {
                    AddComponent((DataTerrain)authoring.TerrainSettings);
                    AddComponent((DataChunk)authoring.TerrainSettings.ChunkSettings);
                    AddComponent((DataNoise)authoring.TerrainSettings.NoiseSettings);
                }
            }

            
        }
    }
}
