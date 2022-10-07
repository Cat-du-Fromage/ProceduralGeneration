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

using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;
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
            terrainQuery = GetEntityQuery(typeof(TagUnintitializeTerrain));
        }

        protected override void OnStartRunning()
        {
            Entity terrainEntity = terrainQuery.GetSingletonEntity();
            DataChunk chunkData = GetComponent<DataChunk>(terrainEntity);
            DataTerrain terrainData = GetComponent<DataTerrain>(terrainEntity);
            int numChunks = cmul(terrainData.NumChunksXY);
            Debug.Log( $"y : {terrainData.NumChunksXY.y}; X: {terrainData.NumChunksXY.x}");
            using NativeArray<float3> positions = GetChunkPositions(chunkData.NumQuadPerLine, terrainData.NumChunksXY);

            NativeArray<Entity> chunkArray = new(numChunks, TempJob, UninitializedMemory);
            EntityManager.Instantiate(GetComponent<PrefabChunk>(terrainEntity).Value, chunkArray);

            //Add RenderMesh
            Material chunkMaterial = EntityManager.GetComponentObject<ObjMaterialTerrain>(terrainEntity).Value;
            RenderMesh renderMesh = new RenderMesh { material = chunkMaterial, mesh = new Mesh() };
            
            EntityManager.AddSharedComponentManaged(chunkArray, renderMesh);
            
            for (int i = 0; i < chunkArray.Length; i++)
            {
                EntityManager.SetName(chunkArray[i], $"Chunk_{i}");
                SetComponent(chunkArray[i], new Translation(){Value = positions[i]});
                int2 coord = GetXY2(i, terrainData.NumChunksXY.x) - terrainData.NumChunksXY / 2;
                CreateChunkAt(terrainEntity, chunkArray[i], i, coord);
            }
            chunkArray.Dispose();
        }

        protected override void OnUpdate() { return; }

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
        
        private void CreateChunkAt(Entity terrainEntity, Entity chunkEntity, int index, int2 coord)
        {
            DataChunk chunkData = GetComponent<DataChunk>(terrainEntity);
            DataNoise noiseData = GetComponent<DataNoise>(terrainEntity);
            
            Mesh chunkMesh = SetupMesh();
            AssignRenderMesh(chunkMesh);
            SetChunkCollider();
            
            //Internal Methods
            //=======================================================================================

            Mesh SetupMesh()
            {
                Mesh mesh = BuildMesh(chunkData, noiseData, coord.x, coord.y);
                mesh.name = $"ChunkMesh_{index}";
                return mesh;
            }
/*
            RenderMesh SetupMeshRenderer(Mesh mesh)
            {
                RenderMesh renderer = EntityManager.GetSharedComponentManaged<RenderMesh>(chunkEntity);
                renderer.mesh = mesh;
                EntityManager.SetSharedComponentManaged(chunkEntity, renderer);
                return renderer;
            }
            */
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

            void SetChunkCollider()
            {
                MeshDataArray meshData = AcquireReadOnlyMeshData(chunkMesh);

                NativeArray<Vector3> vertices = new (chunkData.VerticesCount, Temp);
                meshData[0].GetVertices(vertices);
                NativeArray<int> triangles = new (chunkData.TriangleIndicesCount, Temp);
                meshData[0].GetIndices(triangles, 0);

                NativeArray<int3> tri3 = new (chunkData.TrianglesCount, Temp);
                for (int i = 0; i < chunkData.TrianglesCount; i++)
                {
                    tri3[i] = new int3(triangles[i * 3], triangles[i * 3 + 1], triangles[i * 3 + 2]);
                }

                CollisionFilter filter = GetComponent<PhysicsCollider>(chunkEntity).Value.Value.GetCollisionFilter();
                PhysicsCollider physicsCollider = new ()
                {
                    Value = MeshCollider.Create(vertices.Reinterpret<float3>(), tri3, filter)
                };
                EntityManager.SetComponentData(chunkEntity,physicsCollider);
            }
            
        }
        
        private Mesh BuildMesh(DataChunk terrainSettings, DataNoise noiseData, int x = 0, int y  = 0)
        {
            Mesh terrainMesh = GenerateChunk(terrainSettings, noiseData, x, y);
            terrainMesh.RecalculateBounds();
            return terrainMesh;
        }
        
        //Here : need to construct according to X,Y position of the chunk
        private Mesh GenerateChunk(DataChunk chunkData, DataNoise noiseData, int x = 0, int y  = 0)
        {
            int triIndicesCount = chunkData.TriangleIndicesCount;
            int verticesCount = chunkData.VerticesCount;
            
            MeshDataArray meshDataArray = AllocateWritableMeshData(1);
            MeshData meshData = meshDataArray[0];
            meshData.subMeshCount = 1;

            NativeArray<VertexAttributeDescriptor> vertexAttributes = InitializeVertexAttribute();
            meshData.SetVertexBufferParams(verticesCount, vertexAttributes);
            meshData.SetIndexBufferParams(triIndicesCount, IndexFormat.UInt16);

            NativeArray<float> noiseMap = new (verticesCount, TempJob, UninitializedMemory);
            JobHandle noiseJh    = SetNoiseJob(noiseData, chunkData,new int2(x,y), noiseMap);
            JobHandle meshJh     = SetMeshJob(chunkData, meshData, noiseMap, noiseJh);
            JobHandle normalsJh  = SetNormalsJob(chunkData, meshData, meshJh);
            JobHandle tangentsJh = SetTangentsJob(chunkData, meshData, normalsJh);
            tangentsJh.Complete();
            
            SubMeshDescriptor descriptor = new(0, triIndicesCount) { vertexCount = verticesCount };
            meshData.SetSubMesh(0, descriptor, MeshUpdateFlags.DontRecalculateBounds);

            Mesh terrainMesh = new Mesh { name = "ProceduralTerrainMesh" };
            ApplyAndDisposeWritableMeshData(meshDataArray, terrainMesh);
            return terrainMesh;
        }
        
        private NativeArray<VertexAttributeDescriptor> InitializeVertexAttribute()
        {
            NativeArray<VertexAttributeDescriptor> vertexAttributes = new(4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            vertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, dimension: 3, stream: 0);
            vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, dimension: 3, stream: 1);
            vertexAttributes[2] = new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float16, dimension: 4, stream: 2);
            vertexAttributes[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, dimension: 2, stream: 3);
            return vertexAttributes;
        }
        
        //JOBS
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
