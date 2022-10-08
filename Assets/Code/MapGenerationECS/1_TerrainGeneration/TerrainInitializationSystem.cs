using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Authoring;
using Unity.Rendering;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Aspects;
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
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TerrainInitializationSystem : SystemBase
    {
        private EntityQuery terrainQuery;
        
        protected override void OnCreate()
        {
            terrainQuery = GetEntityQuery(typeof(TagUnInitializeTerrain));
        }

        protected override void OnStartRunning()
        {
            Entity terrainEntity = terrainQuery.GetSingletonEntity();
            EntityManager.SetName(terrainEntity, $"TerrainSingleton");
            
            TerrainAspectStruct terrainStruct = new (EntityManager.GetAspectRO<TerrainAspect>(terrainEntity));
            InitializeTerrain(terrainEntity, terrainStruct);
            
            EntityManager.RemoveComponent<TagUnInitializeTerrain>(terrainEntity);
        }

        protected override void OnUpdate() { return; }

        private void InitializeTerrain(Entity terrainEntity, in TerrainAspectStruct terrainStruct)
        {
            DataTerrain terrainData = terrainStruct.Terrain;
            DataChunk chunkData = terrainStruct.Chunk;
            
            using NativeArray<Entity> chunkArray = CreateChunkEntities(terrainData.NumChunksXY);
            RegisterAndNameChunks(terrainEntity, chunkArray);
            SetChunkPosition(chunkArray, chunkData.NumQuadPerLine, terrainData.NumChunksXY);
            
            Mesh[] chunkMeshes = GenerateChunks(terrainStruct);
            InitializeChunkMesh(chunkArray, chunkMeshes);
            SetChunkCollider(chunkArray, chunkMeshes, chunkData.TrianglesCount);
            
            // INTERNAL METHODS
            // =========================================================================================================
            NativeArray<Entity> CreateChunkEntities(int2 numChunkXY)
            {
                Entity chunkPrefab = GetComponent<PrefabChunk>(terrainEntity).Value;
                NativeArray<Entity> chunks = EntityManager.Instantiate(chunkPrefab, cmul(numChunkXY), TempJob);

                //Add RenderMesh
                Material chunkMaterial = EntityManager.GetComponentObject<ObjMaterialTerrain>(terrainEntity).Value;
                RenderMesh renderMesh = new RenderMesh { material = chunkMaterial, mesh = new Mesh() };
                EntityManager.AddSharedComponentManaged(chunks, renderMesh);

                return chunks;
            }
        }

        private void RegisterAndNameChunks(Entity terrainEntity, NativeArray<Entity> chunkEntities)
        {
            DynamicBuffer<BufferChunk> chunksBuffer = GetBuffer<BufferChunk>(terrainEntity);
            
            for (int i = 0; i < chunkEntities.Length; i++)
            {
                Entity chunkEntity = chunkEntities[i];
                chunksBuffer.Add(chunkEntity);
                EntityManager.SetName(chunkEntity, $"Chunk_{i}");
            }
        }
        
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
        
        private void InitializeChunkMesh(NativeArray<Entity> chunkEntities, Mesh[] chunkMeshes)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Temp);
            for (int i = 0; i < chunkMeshes.Length; i++)
            {
                Entity chunkEntity = chunkEntities[i];
                chunkMeshes[i].RecalculateBounds();
                AssignRenderMeshToChunk(chunkMeshes[i], chunkEntity);
            }
            ecb.Playback(EntityManager);
            
            // INTERNAL METHODS
            // ========================================================================================================
            void AssignRenderMeshToChunk(Mesh chunkMesh, Entity chunkEntity)
            {
                RenderMesh renderer = EntityManager.GetSharedComponentManaged<RenderMesh>(chunkEntity);
                renderer.mesh = chunkMesh;
                ecb.SetSharedComponentManaged(chunkEntity, renderer);

                RenderMeshDescription desc = new(shadowCastingMode: ShadowCastingMode.Off, receiveShadows: false);
                RenderMeshArray renderMeshArray = new(new[] { renderer.material }, new[] { chunkMesh });
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
        
        private void SetChunkCollider(NativeArray<Entity> chunkEntities, Mesh[] chunkMeshes, int trianglesCount)
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
            
            // INTERNAL METHODS
            // =======================================================================================================
            NativeArray<int3> GetMeshTriangles(MeshData meshData, int triangleCount)
            {
                using NativeArray<ushort> triangles = meshData.GetIndexData<ushort>();
                for (int i = 0; i < triangleCount; i++)
                {
                    tri3[i] = new int3(triangles[i * 3], triangles[i * 3 + 1], triangles[i * 3 + 2]);
                }
                return tri3;
            }
        }

        private Mesh[] GenerateChunks(in TerrainAspectStruct terrainStruct)
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

            // INTERNAL METHODS
            // ========================================================================================================

            void SetSubMeshes()
            {
                SubMeshDescriptor descriptor = new(0, triIndicesCount) { vertexCount = verticesCount };
                for (int i = 0; i < numChunks; i++)
                    meshDataArray[i].SetSubMesh(0, descriptor, MeshUpdateFlags.DontRecalculateBounds);
            }
            
            JobHandle CreateMesh(
                MeshData meshData, in int2 coord, in TerrainAspectStruct terrainStruct, NativeArray<float> noiseMap, JobHandle dependency)
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
        
        // JOBS
        // ==============================================================
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
                JGetChunkPositions job = new JGetChunkPositions
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

/*
        private NativeArray<float3> GetChunkPositions(int chunkQuadsPerLine, int2 numChunksXY)
        {
            NativeArray<float3> positions = new (cmul(numChunksXY), TempJob, UninitializedMemory);
            JGetChunkPositions job = new ()
            {
                ChunkQuadsPerLine = chunkQuadsPerLine,
                NumChunksAxis = numChunksXY,
                Positions = positions
            };
            job.ScheduleParallel(positions.Length,JobWorkerCount - 1, default).Complete();
            return positions;
        }
        
        private void CreateChunkAt(in TerrainAspectStruct terrainStruct, Entity chunkEntity, int2 coord)
        {
            Mesh chunkMesh = BuildMesh(terrainStruct, coord);
            AssignRenderMesh(chunkMesh);
            SetChunkCollider(terrainStruct);
            
            //Internal Methods
            //=======================================================================================
            
            void AssignRenderMesh(Mesh mesh)
            {
                RenderMesh renderer = EntityManager.GetSharedComponentManaged<RenderMesh>(chunkEntity);
                renderer.mesh = mesh;
                EntityManager.SetSharedComponentManaged(chunkEntity, renderer);
                
                RenderMeshDescription desc = new(shadowCastingMode: ShadowCastingMode.Off, receiveShadows: false);
                RenderMeshArray renderMeshArray = new(new[] { renderer.material }, new[] { chunkMesh });
                RenderMeshUtility.AddComponents
                (
                    chunkEntity,
                    EntityManager, 
                    desc,
                    renderMeshArray,
                    FromRenderMeshArrayIndices(0, 0)
                );
            }

            void SetChunkCollider(in TerrainAspectStruct terrainStruct)
            {
                using MeshDataArray meshData = AcquireReadOnlyMeshData(chunkMesh);
                
                using NativeArray<float3> vertices = meshData[0].GetVertexData<float3>();
                using NativeArray<ushort> triangles = meshData[0].GetIndexData<ushort>();

                NativeArray<int3> tri3 = new (terrainStruct.Chunk.TrianglesCount, Temp);
                for (int i = 0; i < terrainStruct.Chunk.TrianglesCount; i++)
                {
                    tri3[i] = new int3(triangles[i * 3], triangles[i * 3 + 1], triangles[i * 3 + 2]);
                }

                CollisionFilter filter = GetComponent<PhysicsCollider>(chunkEntity).Value.Value.GetCollisionFilter();
                PhysicsCollider physicsCollider = new () { Value = MeshCollider.Create(vertices, tri3, filter) };
                
                SetComponent(chunkEntity,physicsCollider);
            }
        }
        
        private Mesh BuildMesh(in TerrainAspectStruct terrainStruct, int2 coord)
        {
            Mesh terrainMesh = GenerateChunk(terrainStruct, coord);
            terrainMesh.RecalculateBounds();
            
            int chunkIndex = coord.y * terrainStruct.Chunk.NumQuadPerLine + coord.x;
            terrainMesh.name = $"ChunkMesh_{chunkIndex}";
            
            return terrainMesh;
        }

        //Here : need to construct according to X,Y position of the chunk
        private Mesh GenerateChunk(in TerrainAspectStruct terrainStruct, int2 coord)
        {
            int triIndicesCount = terrainStruct.Chunk.TriangleIndicesCount;
            int verticesCount = terrainStruct.Chunk.VerticesCount;
            int2 coordCentered = coord - terrainStruct.Terrain.NumChunksXY / 2;
            
            MeshDataArray meshDataArray = AllocateWritableMeshData(1);
            MeshData meshData = meshDataArray[0];
            meshData.subMeshCount = 1;

            NativeArray<VertexAttributeDescriptor> vertexAttributes = InitializeVertexAttribute();
            meshData.SetVertexBufferParams(verticesCount, vertexAttributes);
            meshData.SetIndexBufferParams(triIndicesCount, IndexFormat.UInt16);

            using NativeArray<float> noiseMap = new (verticesCount, TempJob, UninitializedMemory);
            JobHandle noiseJh    = SetNoiseJob(terrainStruct.Noise, terrainStruct.Chunk, coordCentered, noiseMap);
            JobHandle meshJh     = SetMeshJob(terrainStruct.Chunk, meshData, noiseMap, noiseJh);
            JobHandle normalsJh  = SetNormalsJob(terrainStruct.Chunk, meshData, meshJh);
            JobHandle tangentsJh = SetTangentsJob(terrainStruct.Chunk, meshData, normalsJh);
            tangentsJh.Complete();
            
            SubMeshDescriptor descriptor = new(0, triIndicesCount) { vertexCount = verticesCount };
            meshData.SetSubMesh(0, descriptor, MeshUpdateFlags.DontRecalculateBounds);

            Mesh terrainMesh = new Mesh();
            ApplyAndDisposeWritableMeshData(meshDataArray, terrainMesh);
            return terrainMesh;
        }
        
        private NativeArray<VertexAttributeDescriptor> InitializeVertexAttribute()
        {
            NativeArray<VertexAttributeDescriptor> vertexAttributes = new(4, Temp, UninitializedMemory);
            vertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, dimension: 3, stream: 0);
            vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, dimension: 3, stream: 1);
            vertexAttributes[2] = new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float16, dimension: 4, stream: 2);
            vertexAttributes[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, dimension: 2, stream: 3);
            return vertexAttributes;
        }
*/