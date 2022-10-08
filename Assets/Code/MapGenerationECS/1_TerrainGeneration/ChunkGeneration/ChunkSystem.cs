/*
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

using static KWZTerrainECS.Utilities;
using static Unity.Jobs.LowLevel.Unsafe.JobsUtility;

using static UnityEngine.Mesh;
using static Unity.Mathematics.math;
using static KWZTerrainECS.ChunkMeshBuilderUtils;

using static UnityEngine.Rendering.VertexAttribute;
using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;

namespace KWZTerrainECS
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ChunkSystem : SystemBase
    {
        private EntityQuery chunksQuery;

        protected override void OnCreate()
        {
            chunksQuery = GetEntityQuery(typeof(TagUnInitializeChunk));
        }
        
        protected override void OnStartRunning()
        {
            Entity terrainEntity = GetSingletonEntity<TagTerrain>();
            Test(terrainEntity);
        }
        
        protected override void OnUpdate() { return; }

        
        private void Test(Entity terrainEntity)
        {
            TerrainAspectStruct terrainStruct = new (SystemAPI.GetAspectRO<TerrainAspect>(terrainEntity));
            DataTerrain terrainData = terrainStruct.Terrain;
            DataChunk chunkData = terrainStruct.Chunk;
            
            Mesh[] chunkMeshes = GenerateChunks(terrainStruct);
            
            NativeArray<float3> positions = new (cmul(terrainData.NumChunksXY), TempJob, UninitializedMemory);
            JobHandle positionJh = JGetChunkPositions.ScheduleParallel(chunkData.NumQuadPerLine, terrainData.NumChunksXY, positions);
            positionJh.Complete();
            
            InitializeChunkData(chunkMeshes, positions);
        }

        private void InitializeChunkData(Mesh[] chunkMeshes, NativeArray<float3> positions)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Temp);
            NativeArray<Entity> chunkEntities = chunksQuery.ToEntityArray(Temp);
            for (int i = 0; i < chunkMeshes.Length; i++)
            {
                Entity chunkEntity = chunkEntities[i];
                SetComponent(chunkEntity, new Translation(){Value = positions[i]});
                AssignRenderMeshToChunk(ecb, chunkMeshes[i], chunkEntity);
                ecb.RemoveComponent<TagUnInitializeChunk>(chunkEntity);
            }
            ecb.Playback(EntityManager);
        }

        private void AssignRenderMeshToChunk(EntityCommandBuffer ecb, Mesh chunkMesh, Entity chunkEntity)
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
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
            );
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
                    int2 coordCentered = GetXY2(i, numChunksXY.x) - numChunksXY / 2;
                    MeshData meshData = InitMeshDataAt(i, vertexAttributes);
                    
                    JobHandle dependency = i == 0 ? default : jobHandles[i - 1];
                    JobHandle meshJobHandle = CreateMesh(meshData, coordCentered, terrainStruct, noiseMap, dependency);
                    
                    jobHandles.Add(meshJobHandle);
                }
                jobHandles[^1].Complete();
                
                for (int i = 0; i < numChunks; i++)
                {
                    SubMeshDescriptor descriptor = new(0, triIndicesCount) { vertexCount = verticesCount };
                    meshDataArray[i].SetSubMesh(i, descriptor, MeshUpdateFlags.DontRecalculateBounds);
                }
            };
            ApplyAndDisposeWritableMeshData(meshDataArray, chunkMeshes);
            return chunkMeshes;

            // INTERNAL METHODS
            // ========================================================================================================
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

        // ===========================================================================================================
        // JOBS
        // ===========================================================================================================

        /// <summary>
        /// Calculate chunks positions in order to get the whole map centered at 0,0,0
        /// </summary>
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
*/