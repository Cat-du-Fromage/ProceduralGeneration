using System;
using UnityEngine;
using Unity.Entities;

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

                AddComponent<TagTerrain>();
                AddComponent<TagUnInitializeTerrain>();
                AddComponent(new PrefabChunk() { Value = GetEntity(authoring.TerrainSettings.ChunkSettings.Prefab) });
                AddComponentObject(new ObjMaterialTerrain(){Value = authoring.ChunkMaterial});
                
                AddTerrainAspect();
                
                // -------------------------------------------------------------------------------------------------------
                // INTERNAL METHODS
                // -------------------------------------------------------------------------------------------------------
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
