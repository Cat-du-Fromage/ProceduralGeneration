using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;
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
        [field:SerializeField] public SpawnSettings SpawnSettings { get; private set; }
        
        //public GameObject[] chunks { get; private set; }

        private class CameraAuthoring : Baker<KzwTerrainBaker>
        {
            public override void Bake(KzwTerrainBaker authoring)
            {
                DependsOn(authoring.TerrainSettings);
                DependsOn(authoring.SpawnSettings);
                
                DynamicBuffer<BufferChunk> chunksBuffer = AddBuffer<BufferChunk>();
                chunksBuffer.EnsureCapacity(authoring.TerrainSettings.ChunksCount);
                
                AddComponent<TagUnintitializeTerrain>();
                AddComponent((DataTerrain)authoring.TerrainSettings);
                AddComponent((DataChunk)authoring.TerrainSettings.ChunkSettings);
                
                AddComponent(new PrefabChunk()
                {
                    Value = GetEntity(authoring.TerrainSettings.ChunkSettings.Prefab)
                });

                DependsOn(authoring.ChunkMaterial);
                AddComponentObject(new ObjMaterialTerrain(){Value = authoring.ChunkMaterial});
            }
        }
        
        private GameObject[] Build(TerrainSettings terrain, GameObject chunkPrefab)
        {
            GameObject[] chunkArray = new GameObject[terrain.ChunksCount];

            using NativeArray<float3> positions = new (chunkArray.Length, TempJob, UninitializedMemory);
            JGetChunkPositions job = new ()
            {
                ChunkQuadsPerLine = terrain.ChunkQuadsPerLine,
                NumChunksAxis = terrain.NumChunkXY,
                Positions = positions
            };
            job.ScheduleParallel(positions.Length,JobWorkerCount - 1, default).Complete();
            
            
            for (int i = 0; i < chunkArray.Length; i++)
            {
                GameObject chunk = Instantiate(chunkPrefab, positions[i], Quaternion.identity, transform);
                chunk.name = $"Chunk_{i}";
                chunkArray[i] = chunk;
            }
            return chunkArray;
        }
        
        [BurstCompile(CompileSynchronously = true)]
        private struct JGetChunkPositions : IJobFor
        {
            [ReadOnly] public int ChunkQuadsPerLine;
            [ReadOnly] public int2 NumChunksAxis;
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float3> Positions;

            public void Execute(int index)
            {
                float halfSizeChunk = ChunkQuadsPerLine / 2f;
                int2 halfNumChunks = NumChunksAxis / 2; //we don't want 0.5!
                int2 coord = GetXY2(index, NumChunksAxis.x) - halfNumChunks;

                float2 positionOffset = mad(coord, ChunkQuadsPerLine, halfSizeChunk);
                float positionX = select(positionOffset.x, 0, halfNumChunks.x == 0);
                float positionY = select(positionOffset.y, 0, halfNumChunks.y == 0);
                
                Positions[index] = new float3(positionX, 0, positionY);
            }
        }
    }
}
