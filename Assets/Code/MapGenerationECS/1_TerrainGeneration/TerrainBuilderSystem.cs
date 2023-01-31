using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Entities.SystemAPI;
using static Unity.Jobs.LowLevel.Unsafe.JobsUtility;
using static Unity.Mathematics.math;
using static KWZTerrainECS.Utilities;
using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;

using Mesh = UnityEngine.Mesh;
using Material = UnityEngine.Material;
using MeshCollider = Unity.Physics.MeshCollider;
using int2 = Unity.Mathematics.int2;


namespace KWZTerrainECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TerrainBuilderSystem : SystemBase
    {
        private EntityQuery terrainInitQuery;
        private EntityQuery chunksInitQuery;

        private Entity terrainEntity;
        private TerrainAspectStruct terrainAspect;
        
        protected override void OnCreate()
        {
            terrainInitQuery = QueryBuilder().WithAll<TagTerrain, TagInitialize>().Build();
            chunksInitQuery = QueryBuilder().WithAll<TagChunk, TagInitialize>().Build();
            
            NativeArray<EntityQuery> queries = new (2, Temp);
            queries[0] = terrainInitQuery;
            queries[1] = chunksInitQuery;
            RequireAnyForUpdate(queries);
        }

        protected override void OnStartRunning()
        {
            // Retrieve Terrain
            terrainEntity = GetSingletonEntity<TagTerrain>();
            terrainAspect =  new TerrainAspectStruct(GetAspectRO<TerrainAspect>(terrainEntity));
        }

        protected override void OnUpdate()
        {
            BuildTerrain();
            BuildChunks();
        }
        
        private void BuildTerrain()
        {
            if (terrainInitQuery.IsEmpty) return;
            
            Entities
            .WithoutBurst()
            .WithStructuralChanges()
            .WithStoreEntityQueryInField(ref terrainInitQuery)
            .ForEach((Entity entity, in PrefabChunk prefab, in DataTerrain dataTerrain) =>
            {
                EntityCommandBuffer ecb = new(Temp);
                int numChunks = cmul(dataTerrain.NumChunksXY);
                ecb.SetName(entity, "TerrainSingleton");

                //Create Chunks Entities
                using NativeArray<Entity> chunks = CreateChunks(prefab.Value, numChunks);
                // Register Entity
                SetupAndRegisterChunks(entity, chunks);
                
                ecb.RemoveComponent<TagInitialize>(entity);
                ecb.Playback(EntityManager);
            }).Run();
        }

        //======================================================================================================
        /// <summary>
        /// Register Chunks in buffer and set their name (according to their index)
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="chunkEntities"></param>
        private void SetupAndRegisterChunks(Entity entity, NativeArray<Entity> chunkEntities)
        {
            EntityCommandBuffer ecb = new(Temp);
            
            using NativeArray<float3> positions = 
                GetChunksPosition(terrainAspect.Chunk.NumQuadPerLine, terrainAspect.Terrain.NumChunksXY);
            
            ecb.AddComponent<TagChunk>(chunkEntities);
            ecb.AddComponent<TagInitialize>(chunkEntities);

            BufferLookup<BufferChunk> chunkBuffer = GetBufferLookup<BufferChunk>(false);
            for (int i = 0; i < chunkEntities.Length; i++)
            {
                Entity chunkEntity = chunkEntities[i];
                ecb.SetName(chunkEntity, $"Chunk_{i}");
                ecb.AddComponent(chunkEntity, new DataChunkIndex(){Value = i});
                
                //BROKEN SINCE THE NEW UPDATE: 
                //ecb.AddComponent(chunkEntity, new Parent(){Value = entity});
                //ecb.AddComponent<ParentTransform>(chunkEntity);
                //Debug.Log($"chunk position at : {positions[i]}");
                GetAspectRW<TransformAspect>(chunkEntity).TranslateWorld(positions[i]);
                
                //SystemAPI.GetComponent<WorldTransform>(chunkEntity).Translate(positions[i]);
                //SystemAPI.SetComponent(chunkEntity, new WorldTransform(){Position = positions[i]});
                chunkBuffer[entity].Add(chunkEntities[i]);
            }
            ecb.Playback(EntityManager);
        }
        
        //======================================================================================================
        /// <summary>
        /// Create Chunks
        /// </summary>
        /// <param name="chunkPrefab">Chunk Prefab to instantiate</param>
        /// <param name="numChunks">Number of chunks to create</param>
        /// <returns>chunks entities in a native array</returns>
        private NativeArray<Entity> CreateChunks(Entity chunkPrefab, int numChunks)
        {
            NativeArray<Entity> chunks = EntityManager.Instantiate(chunkPrefab, numChunks, TempJob);
            return chunks;
        }
        
        // =============================================================================================================
        /// <summary>
        /// Reposition chunks according to their index and to the center of the map
        /// </summary>
        /// <param name="numQuadPerLine"></param>
        /// <param name="numChunkXY"></param>
        private NativeArray<float3> GetChunksPosition(int numQuadPerLine, int2 numChunkXY)
        {
            NativeArray<float3> positions = new (cmul(numChunkXY), TempJob, UninitializedMemory);
            JGetChunkPositions.Process(numQuadPerLine, numChunkXY, positions).Complete();
            return positions;
        }
        
        // =============================================================================================================
        // JOBS
        // =============================================================================================================
        [BurstCompile(CompileSynchronously = false)]
        private struct JGetChunkPositions : IJobFor
        {
            [ReadOnly] public int ChunkQuadsPerLine;
            [ReadOnly] public int2 NumChunksAxis;
            [WriteOnly, NativeDisableParallelForRestriction] 
            public NativeArray<float3> Positions;

            public void Execute(int index)
            {
                float halfSizeChunk = ChunkQuadsPerLine / 2f;
                int2 halfNumChunks = NumChunksAxis / 2; //we don't want 0.5!
                int2 coord = GetXY2(index, NumChunksAxis.x) - halfNumChunks;

                float2 positionOffset = mad(coord, ChunkQuadsPerLine, halfSizeChunk);
                //Case the is only 1 chunk halfNumChunks.x/y == 0
                float positionX = select(positionOffset.x, 0, halfNumChunks.x == 0);
                float positionY = select(positionOffset.y, 0, halfNumChunks.y == 0);
                
                Positions[index] = new float3(positionX, 0, positionY);
            }
            
            public static JobHandle Process(
                int chunkQuadsPerLine, int2 numChunkXY, NativeArray<float3> positions, JobHandle dependency = default)
            {
                JGetChunkPositions job = new ()
                {
                    ChunkQuadsPerLine = chunkQuadsPerLine,
                    NumChunksAxis = numChunkXY,
                    Positions = positions
                };
                return job.ScheduleParallel(positions.Length, JobWorkerCount - 1, dependency);
            }
        }
    }
}