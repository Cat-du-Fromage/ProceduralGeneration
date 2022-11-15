/*
using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Authoring;
using Unity.Rendering;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine.Rendering;
using static Unity.Jobs.LowLevel.Unsafe.JobsUtility;
using static Unity.Mathematics.math;
using static UnityEngine.Mesh;
using static KWZTerrainECS.Utilities;
using static KWZTerrainECS.ChunkMeshBuilderUtils;
using static Unity.Rendering.MaterialMeshInfo;

using static UnityEngine.Rendering.VertexAttribute;
using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;
using int2 = Unity.Mathematics.int2;
using Material = UnityEngine.Material;
using MeshCollider = Unity.Physics.MeshCollider;

namespace KWZTerrainECS
{
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TerrainInitializationSystem : SystemBase
    {
        private EntityQuery UnInitializeTerrainQuery;
        private Entity terrainEntity;
        
        protected override void OnCreate()
        {
            UnInitializeTerrainQuery = GetEntityQuery(typeof(TagUnInitializeTerrain));
        }

        protected override void OnStartRunning()
        {
            terrainEntity = UnInitializeTerrainQuery.GetSingletonEntity();
        }

        protected override void OnUpdate()
        {
            EntityManager.SetName(terrainEntity, "TerrainSingleton");
            StepTerrainGeneration();
            EntityManager.RemoveComponent<TagUnInitializeTerrain>(terrainEntity);
        }
        


        // ==========================================================================================================
        // STEP 1 : Terrain Generation
        // ==========================================================================================================
        private void StepTerrainGeneration()
        {
            //EntityManager.SetName(terrainEntity, "TerrainSingleton");
            
            TerrainAspectStruct terrainStruct = new (EntityManager.GetAspectRO<TerrainAspect>(terrainEntity));
            BuildTerrain(terrainStruct);
            
            //EntityManager.RemoveComponent<TagUnInitializeTerrain>(terrainEntity);
        }
        
        private void RegisterChunksEntities(NativeArray<Entity> chunkArray)
        {
            if (!HasBuffer<LinkedEntityGroup>(terrainEntity))
            {
                EntityManager.AddBuffer<LinkedEntityGroup>(terrainEntity);
            }
            GetBuffer<LinkedEntityGroup>(terrainEntity).EnsureCapacity(chunkArray.Length);
            DynamicBuffer<LinkedEntityGroup> bufferLinked = GetBuffer<LinkedEntityGroup>(terrainEntity);
            bufferLinked.AddRange(chunkArray.Reinterpret<LinkedEntityGroup>());
        }

        private void BuildTerrain(in TerrainAspectStruct terrainStruct)
        {
            DataTerrain terrainData = terrainStruct.Terrain;
            DataChunk chunkData = terrainStruct.Chunk;

            using NativeArray<Entity> chunkArray = CreateChunkEntities(terrainData.NumChunksXY);
            EntityManager.AddComponent<TagChunk>(chunkArray);
            //RegisterChunksEntities(chunkArray);
            RegisterAndNameChunks(chunkArray);
            SetChunkPosition(chunkArray, chunkData.NumQuadPerLine, terrainData.NumChunksXY);
            
            Mesh[] chunkMeshes = GenerateChunksMeshes(terrainStruct);
            UpdateChunkMeshRenderer(chunkArray, chunkMeshes);
            UpdateChunkCollider(chunkArray, chunkMeshes, chunkData.TrianglesCount);
        }

        /// <summary>
        /// Create Chunks
        /// </summary>
        /// <param name="numChunkXY"></param>
        /// <returns></returns>
        private NativeArray<Entity> CreateChunkEntities(int2 numChunkXY)
        {
            Entity chunkPrefab = GetComponent<PrefabChunk>(terrainEntity).Value;
            NativeArray<Entity> chunks = EntityManager.Instantiate(chunkPrefab, cmul(numChunkXY), TempJob);
            return chunks;
        }

        /// <summary>
        /// Register Chunks in buffer and set their name (according to their index)
        /// </summary>
        /// <param name="chunkEntities"></param>
        private void RegisterAndNameChunks(NativeArray<Entity> chunkEntities)
        {
            DynamicBuffer<BufferChunk> chunksBuffer = GetBuffer<BufferChunk>(terrainEntity);
            for (int i = 0; i < chunkEntities.Length; i++)
            {
                Entity chunkEntity = chunkEntities[i];
                chunksBuffer.Add(chunkEntity);
                EntityManager.SetName(chunkEntity, $"Chunk_{i}");
            }
        }
        
        /// <summary>
        /// Reposition chunks according to their index and to the center of the map
        /// </summary>
        /// <param name="chunkEntities"></param>
        /// <param name="numQuadPerLine"></param>
        /// <param name="numChunkXY"></param>
        private void SetChunkPosition(NativeArray<Entity> chunkEntities, int numQuadPerLine, int2 numChunkXY)
        {
            using NativeArray<float3> positions = new (cmul(numChunkXY), TempJob, UninitializedMemory);
            JGetChunkPositions.ScheduleParallel(numQuadPerLine, numChunkXY, positions).Complete();
            
            for (int i = 0; i < chunkEntities.Length; i++)
            {
                Entity chunkEntity = chunkEntities[i];
                SetComponent(chunkEntity, new Translation(){Value = positions[i]});
            }
        }
        
        /// <summary>
        /// Construct Chunk Meshes
        /// </summary>
        /// <param name="chunkEntities"></param>
        /// <param name="chunkMeshes"></param>
        private void UpdateChunkMeshRenderer(NativeArray<Entity> chunkEntities, Mesh[] chunkMeshes)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Temp);
            for (int i = 0; i < chunkMeshes.Length; i++)
            {
                Entity chunkEntity = chunkEntities[i];
                chunkMeshes[i].RecalculateBounds();
                AssignRendererToChunk(chunkMeshes[i], chunkEntity);
            }
            ecb.Playback(EntityManager);
            
            // -------------------------------------------------------------------------------------------------------
            // INTERNAL METHODS
            // -------------------------------------------------------------------------------------------------------
            void AssignRendererToChunk(Mesh chunkMesh, Entity chunkEntity)
            {
                //Material material = EntityManager.GetSharedComponentManaged<RenderMeshArray>(chunkEntity).Materials[1];
                Material material = EntityManager.GetComponentObject<ObjMaterialTerrain>(UnInitializeTerrainQuery.GetSingletonEntity()).Value;
                RenderMeshDescription desc = new(shadowCastingMode: ShadowCastingMode.Off, receiveShadows: false);
                RenderMeshArray renderMeshArray = new(new[] { material }, new[] { chunkMesh });
                RenderMeshUtility.AddComponents
                (
                    chunkEntity, 
                    EntityManager, 
                    desc, 
                    renderMeshArray,
        FromRenderMeshArrayIndices(0, 0)
                );
            }
        }
        
        /// <summary>
        /// Set Collider according to Meshes Data
        /// </summary>
        /// <param name="chunkEntities"></param>
        /// <param name="chunkMeshes"></param>
        /// <param name="trianglesCount"></param>
        private void UpdateChunkCollider(NativeArray<Entity> chunkEntities, Mesh[] chunkMeshes, int trianglesCount)
        {
            using MeshDataArray meshDataArray = AcquireReadOnlyMeshData(chunkMeshes);
            NativeArray<int3> tri3 = new (trianglesCount, Temp, UninitializedMemory);
            
            for (int chunkIndex = 0; chunkIndex < chunkMeshes.Length; chunkIndex++)
            {
                Entity chunkEntity = chunkEntities[chunkIndex];
                NativeArray<float3> vertices = meshDataArray[chunkIndex].GetVertexData<float3>();
                NativeArray<int3> triangles3 = GetMeshTriangles(meshDataArray[chunkIndex], trianglesCount);
                
                CollisionFilter filter = GetComponent<PhysicsCollider>(chunkEntity).Value.Value.GetCollisionFilter();
                PhysicsCollider physicsCollider = new () { Value = MeshCollider.Create(vertices, triangles3, filter) };
                
                SetComponent(chunkEntity,physicsCollider);
            }
            
            // -------------------------------------------------------------------------------------------------------
            // INTERNAL METHODS
            // -------------------------------------------------------------------------------------------------------
            NativeArray<int3> GetMeshTriangles(MeshData meshData, int triangleCount)
            {
                NativeArray<ushort> triangles = meshData.GetIndexData<ushort>();
                for (int i = 0; i < triangleCount; i++)
                {
                    tri3[i] = new int3(triangles[i * 3], triangles[i * 3 + 1], triangles[i * 3 + 2]);
                }
                return tri3;
            }
        }

        /// <summary>
        /// Generate Meshes according to settings
        /// </summary>
        /// <param name="terrainStruct"></param>
        /// <returns></returns>
        private Mesh[] GenerateChunksMeshes(in TerrainAspectStruct terrainStruct)
        {
            int verticesCount = terrainStruct.Chunk.VerticesCount;
            int triIndicesCount = terrainStruct.Chunk.TriangleIndicesCount;
            
            int2 numChunksXY = terrainStruct.Terrain.NumChunksXY;
            int numChunks = cmul(numChunksXY);
            
            Mesh[] chunkMeshes = new Mesh[numChunks];
            MeshDataArray meshDataArray = AllocateWritableMeshData(numChunks);
            using (NativeArray<float> noiseMap = new(verticesCount, TempJob, UninitializedMemory))
            {
                NativeList<JobHandle> jobHandles = new (numChunks, Temp);
                NativeArray<VertexAttributeDescriptor> vertexAttributes = InitializeVertexAttribute();
                for (int i = 0; i < numChunks; i++)
                {
                    chunkMeshes[i] = new Mesh { name = $"ChunkMesh_{i}" };
                    int2 coordCentered = GetXY2(i, numChunksXY.x) - numChunksXY / 2;
                    MeshData meshData = InitMeshDataAt(i, vertexAttributes);
                    
                    JobHandle dependency = i == 0 ? default : jobHandles[i - 1];
                    JobHandle meshJobHandle = CreateMesh(meshData, coordCentered, terrainStruct, noiseMap, dependency);
                    
                    jobHandles.Add(meshJobHandle);
                }
                jobHandles[^1].Complete();
                SetSubMeshes();
            };
            ApplyAndDisposeWritableMeshData(meshDataArray, chunkMeshes);
            return chunkMeshes;
            
            // -------------------------------------------------------------------------------------------------------
            // INTERNAL METHODS
            // -------------------------------------------------------------------------------------------------------

            void SetSubMeshes()
            {
                SubMeshDescriptor descriptor = new(0, triIndicesCount) { vertexCount = verticesCount };
                for (int i = 0; i < numChunks; i++)
                    meshDataArray[i].SetSubMesh(0, descriptor, MeshUpdateFlags.DontRecalculateBounds);
            }
            
            JobHandle CreateMesh(MeshData meshData, 
                in int2 coord, 
                in TerrainAspectStruct terrainStruct, 
                NativeArray<float> noiseMap, 
                JobHandle dependency)
            {
                JobHandle noiseJh    = SetNoiseJob(terrainStruct.Noise, terrainStruct.Chunk, coord, noiseMap, dependency);
                JobHandle meshJh     = SetMeshJob(terrainStruct.Chunk, meshData, noiseMap, noiseJh);
                JobHandle normalsJh  = SetNormalsJob(terrainStruct.Chunk, meshData, meshJh);
                JobHandle tangentsJh = SetTangentsJob(terrainStruct.Chunk, meshData, normalsJh);

                return tangentsJh;
            }
            
            MeshData InitMeshDataAt(int index, NativeArray<VertexAttributeDescriptor> vertexAttributes)
            {
                MeshData meshData = meshDataArray[index];
                meshData.subMeshCount = 1;
                meshData.SetVertexBufferParams(verticesCount, vertexAttributes);
                meshData.SetIndexBufferParams(triIndicesCount, IndexFormat.UInt16);
                return meshData;
            }
            
            NativeArray<VertexAttributeDescriptor> InitializeVertexAttribute()
            {
                NativeArray<VertexAttributeDescriptor> vertexAttributes = new(4, Temp, UninitializedMemory);
                vertexAttributes[0] = new VertexAttributeDescriptor(Position, VertexAttributeFormat.Float32, dimension: 3, stream: 0);
                vertexAttributes[1] = new VertexAttributeDescriptor(Normal, VertexAttributeFormat.Float32, dimension: 3, stream: 1);
                vertexAttributes[2] = new VertexAttributeDescriptor(Tangent, VertexAttributeFormat.Float16, dimension: 4, stream: 2);
                vertexAttributes[3] = new VertexAttributeDescriptor(TexCoord0, VertexAttributeFormat.Float16, dimension: 2, stream: 3);
                return vertexAttributes;
            }
        }
        
        // =============================================================================================================
        // JOBS
        // =============================================================================================================
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
            
            public static JobHandle ScheduleParallel(
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
        
        // ==========================================================================================================
        // STEP 2 : GridSystem
        // ==========================================================================================================
    }
}
*/