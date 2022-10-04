using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Authoring;
using Unity.Rendering;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
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

            using NativeArray<float3> positions = GetChunkPositions(chunkData.NumQuadPerLine, terrainData.NumChunksXY);

            NativeArray<Entity> chunkArray = new(numChunks, TempJob, UninitializedMemory);
            EntityManager.Instantiate(GetComponent<PrefabChunk>(terrainEntity).Value, chunkArray);

            RenderMesh renderMesh = new RenderMesh();
            renderMesh.mesh = new Mesh();
            EntityManager.AddSharedComponentManaged(chunkArray, renderMesh);
            
            for (int i = 0; i < chunkArray.Length; i++)
            {
                
                EntityManager.SetName(chunkArray[i], $"Chunk_{i}");
                SetComponent(chunkArray[i], new Translation(){Value = positions[i]});
                CreateChunkAt(terrainEntity, chunkArray[i], i, terrainData.NumChunksXY / 2);
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
        
        private void CreateChunkAt(Entity terrainEntity, Entity chunkEntity, int index, int2 coordOffset)
        {
            DataChunk chunkData = GetComponent<DataChunk>(terrainEntity);
            Mesh chunkMesh = BuildMesh(chunkData, coordOffset.x, coordOffset.y);
            chunkMesh.name = $"ChunkMesh_{index}";

            Material material = EntityManager.GetComponentObject<ObjMaterialTerrain>(terrainEntity).Value;
            //RenderMesh renderer = EntityManager.GetSharedComponentManaged<RenderMesh>(chunkEntity);
            //renderer.material = material;
            //renderer.mesh = chunkMesh;

            RenderMeshDescription desc = new RenderMeshDescription
            (
                shadowCastingMode: ShadowCastingMode.Off,
                receiveShadows: false
            );

            Debug.Log($"mesh: {chunkMesh}; Material: {material}");
            
            RenderMeshArray renderMeshArray = new RenderMeshArray
            (
                new Material[] { material },
                new Mesh[] { chunkMesh }
            );
            RenderMeshUtility.AddComponents
            (
                chunkEntity,
                EntityManager, 
                desc,
                renderMeshArray,
                FromRenderMeshArrayIndices(0, 0)
            );
        }
        
        private Mesh BuildMesh(DataChunk terrainSettings, int x = 0, int y  = 0)
        {
            Mesh terrainMesh = GenerateChunk(terrainSettings, x, y);
            terrainMesh.RecalculateBounds();
            return terrainMesh;
        }
        
        //Here : need to construct according to X,Y position of the chunk
        private Mesh GenerateChunk(DataChunk chunkData, int x = 0, int y  = 0)
        {
            int triIndicesCount = chunkData.TriangleIndicesCount;
            int verticesCount = chunkData.VerticesCount;
            
            MeshDataArray meshDataArray = AllocateWritableMeshData(1);
            MeshData meshData = meshDataArray[0];
            meshData.subMeshCount = 1;

            NativeArray<VertexAttributeDescriptor> vertexAttributes = InitializeVertexAttribute();
            meshData.SetVertexBufferParams(verticesCount, vertexAttributes);
            meshData.SetIndexBufferParams(triIndicesCount, IndexFormat.UInt16);

            NativeArray<float> noiseMap = new (verticesCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            //JobHandle noiseJh    = SetNoiseJob(terrain,new int2(x,y), noiseMap);
            JobHandle meshJh     = SetMeshJob(chunkData, meshData, noiseMap/*, noiseJh*/);
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
