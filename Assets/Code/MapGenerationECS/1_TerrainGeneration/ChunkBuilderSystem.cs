using Unity.Entities;
using Unity.Rendering;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine.Rendering;

using static UnityEngine.Mesh;
using static KWZTerrainECS.Utilities;
using static KWZTerrainECS.ChunkMeshBuilderUtils;
using static Unity.Rendering.MaterialMeshInfo;

using static UnityEngine.Rendering.VertexAttribute;
using static UnityEngine.Rendering.VertexAttributeFormat;
using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;

using int2 = Unity.Mathematics.int2;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;
using MeshCollider = Unity.Physics.MeshCollider;

namespace KWZTerrainECS
{
    /// <summary>
    /// CHUNKS BUILDER
    /// </summary>
    public partial class TerrainBuilderSystem : SystemBase
    {
        private void BuildChunks()
        {
            if (chunksInitQuery.IsEmpty) return;
            
            Material material = EntityManager.GetComponentObject<ObjMaterialTerrain>(terrainEntity).Value;
            Mesh[] meshes = GenerateChunksMeshes();

            EntityCommandBuffer ecb = new (Temp);
            Entities
            .WithoutBurst()
            .WithStructuralChanges()
            .WithStoreEntityQueryInField(ref chunksInitQuery)
            .ForEach((Entity chunkEntity, in DataChunkIndex index) =>
            {
                UpdateChunkMeshRenderer(chunkEntity, meshes[index.Value], material);
                UpdateChunkCollider(chunkEntity, meshes[index.Value], terrainAspect.Chunk.TrianglesCount);
                ecb.RemoveComponent<TagInitialize>(chunkEntity);
            }).Run();
            ecb.Playback(EntityManager);
        }

        // =============================================================================================================
        /// <summary>
        /// Generate Meshes according to settings
        /// </summary>
        /// <param name="terrainStruct"></param>
        /// <returns></returns>
        private Mesh[] GenerateChunksMeshes()
        {
            int numChunks = terrainAspect.NumChunks;

            Mesh[] chunkMeshes = new Mesh[numChunks];
            MeshDataArray meshDataArray = AllocateWritableMeshData(numChunks);
            
            using (NativeArray<float> noiseMap = new (terrainAspect.ChunkVerticesCount, TempJob, UninitializedMemory))
            {
                NativeList<JobHandle> jobHandles = new (numChunks, Temp);
                //NativeArray<VertexAttributeDescriptor> vertexAttributes = InitializeVertexAttribute();
                for (int i = 0; i < numChunks; i++)
                {
                    chunkMeshes[i] = new Mesh { name = $"ChunkMesh_{i}" };
                    int2 coordCentered = GetXYOffset(i, terrainAspect.NumChunksXY);
                    MeshData meshData = meshDataArray[i];
                    meshData.InitializeBufferParams(terrainAspect.ChunkVerticesCount, terrainAspect.ChunkTriangleIndicesCount);
                    
                    JobHandle dependency = i == 0 ? default : jobHandles[i - 1];
                    JobHandle meshJobHandle = CreateMesh(meshData, coordCentered, terrainAspect, noiseMap, dependency);
                    
                    jobHandles.Add(meshJobHandle);
                }
                jobHandles[^1].Complete();
                meshDataArray.SetSubMeshes(terrainAspect.ChunkVerticesCount, terrainAspect.ChunkTriangleIndicesCount);
            };
            ApplyAndDisposeWritableMeshData(meshDataArray, chunkMeshes);
            return chunkMeshes;
            
            // -------------------------------------------------------------------------------------------------------
            // INTERNAL METHODS
            // -------------------------------------------------------------------------------------------------------

            JobHandle CreateMesh(MeshData meshData, 
                in int2 coord, 
                in TerrainAspectStruct terrainStruct, 
                NativeArray<float> noiseMap, 
                JobHandle dependency = default)
            {
                JobHandle noiseJh    = SetNoiseJob(terrainStruct.Noise, terrainStruct.Chunk, coord, noiseMap, dependency);
                JobHandle meshJh     = SetMeshJob(terrainStruct.Chunk, meshData, noiseMap, noiseJh);
                JobHandle normalsJh  = SetNormalsJob(terrainStruct.Chunk, meshData, meshJh);
                JobHandle tangentsJh = SetTangentsJob(terrainStruct.Chunk, meshData, normalsJh);

                return tangentsJh;
            }
        }

        // =============================================================================================================
        /// <summary>
        /// Construct Chunk Meshes
        /// </summary>
        /// <param name="chunkEntity"></param>
        /// <param name="chunkMesh"></param>
        /// <param name="material"></param>
        private void UpdateChunkMeshRenderer(Entity chunkEntity, Mesh chunkMesh, Material material)
        {
            chunkMesh.RecalculateBounds();
            
            // Assign Renderer To Chunk
            RenderMeshUtility.AddComponents
            (
                chunkEntity, 
                EntityManager, 
                new RenderMeshDescription(ShadowCastingMode.Off), 
                new RenderMeshArray(new[] { material }, new[] { chunkMesh }),
                FromRenderMeshArrayIndices(0, 0)
            );
        }
        
        // =============================================================================================================
        /// <summary>
        /// Set Collider according to Meshes Data
        /// </summary>
        /// <param name="chunkEntity"></param>
        /// <param name="chunkMesh"></param>
        /// <param name="trianglesCount"></param>
        private void UpdateChunkCollider(Entity chunkEntity, Mesh chunkMesh, int trianglesCount)
        {
            using MeshDataArray meshDataArray = AcquireReadOnlyMeshData(chunkMesh);
            NativeArray<int3> triangleIndices = new (trianglesCount, Temp, UninitializedMemory);

            NativeArray<float3> vertices = meshDataArray[0].GetVertexData<float3>();
            NativeArray<int3> triangles3 = GetMeshTriangles(meshDataArray[0], trianglesCount);
            
            CollisionFilter filter = GetComponent<PhysicsCollider>(chunkEntity).Value.Value.GetCollisionFilter();
            PhysicsCollider physicsCollider = new () { Value = MeshCollider.Create(vertices, triangles3, filter) };
            
            SetComponent(chunkEntity,physicsCollider);

            // -------------------------------------------------------------------------------------------------------
            // INTERNAL METHODS
            // -------------------------------------------------------------------------------------------------------
            NativeArray<int3> GetMeshTriangles(MeshData meshData, int triangleCount)
            {
                NativeArray<ushort> triangles = meshData.GetIndexData<ushort>();
                for (int i = 0; i < triangleCount; i++)
                {
                    triangleIndices[i] = new int3(triangles[i * 3], triangles[i * 3 + 1], triangles[i * 3 + 2]);
                }
                return triangleIndices;
            }
        }
    }
}