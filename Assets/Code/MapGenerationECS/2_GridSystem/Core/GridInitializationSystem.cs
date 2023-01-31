using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Jobs.LowLevel.Unsafe.JobsUtility;
using static Unity.Mathematics.math;
using static UnityEngine.Mesh;
using static KWZTerrainECS.Utilities;

using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;

namespace KWZTerrainECS
{
    //[RequireMatchingQueriesForUpdate] CAREFULL equivalent of require ANY!
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(TerrainBuilderSystem))]
    public partial class GridInitializationSystem : SystemBase
    {
        //private EntityQuery initTerrainQuery;
        private EntityQuery initGridTerrainQuery;

        protected override void OnCreate()
        {
            /*
            initTerrainQuery = new EntityQueryBuilder(Temp)
            .WithAll<TagTerrain>()
            .WithNone<TagInitialize>()
            .Build(this);
            */
            initGridTerrainQuery = new EntityQueryBuilder(Temp)
            .WithAll<TagUnInitializeGrid>()
            .Build(this);
            
            //RequireForUpdate(unInitializeGridTerrainQuery);
            RequireForUpdate(initGridTerrainQuery);
        }

        protected override void OnUpdate()
        {
            UnityEngine.Debug.Log($"Pass {initGridTerrainQuery.IsEmpty}");
            Entity terrain = GetSingletonEntity<TagTerrain>();
            TerrainAspectStruct terrainStruct = new (EntityManager.GetAspectRO<TerrainAspect>(terrain));
            
            ref BlobArray<Cell> cells = ref GenerateGridTerrain(terrain, terrainStruct);
            EntityManager.RemoveComponent<TagUnInitializeGrid>(terrain);
            
#if UNITY_EDITOR
            //Test2(ref cells);
#endif
            
        }

        private ref BlobArray<Cell> GenerateGridTerrain(
            Entity terrainEntity, 
            in TerrainAspectStruct terrainStruct)
        {
            DynamicBuffer<Entity> chunkEntities = EntityManager.GetBuffer<BufferChunk>(terrainEntity, true).Reinterpret<Entity>();
            using MeshDataArray meshDataArray = EntityManager.GetEntitiesMeshDataArray(chunkEntities.AsNativeArray());
            
            BlobAssetReference<GridCells> blob = CreateGridCells(meshDataArray, terrainStruct, chunkEntities.AsNativeArray());
            EntityManager.AddComponentData(terrainEntity, new BlobCells() { Blob = blob });

            DynamicBuffer<ChunkNodeGrid> buffer = EntityManager.AddBuffer<ChunkNodeGrid>(terrainEntity);
            buffer.BuildGrid(terrainStruct.Chunk.NumQuadPerLine, terrainStruct.Terrain.NumChunksXY);
            return ref blob.Value.Cells;
        }
        
        private BlobAssetReference<GridCells> CreateGridCells(
            MeshDataArray meshDataArray,
            in TerrainAspectStruct terrainStruct,
            NativeArray<Entity> chunks)
        {
            const int numVerticesPerQuad = 4;
            const int verticesWidth = numVerticesPerQuad / 2;
            
            using BlobBuilder builder = new (Temp);
            
            ref GridCells gridCells = ref builder.ConstructRoot<GridCells>();
            gridCells.ChunkSize = terrainStruct.Chunk.NumQuadPerLine;
            gridCells.NumChunkX = terrainStruct.Terrain.NumChunksXY.x;
            
            BlobBuilderArray<Cell> arrayBuilder = ConstructGridArray(ref gridCells, terrainStruct);
            return builder.CreateBlobAssetReference<GridCells>(Persistent);

            // -------------------------------------------------------------------------------------------------------
            // INNER METHODS : Construct Grid nodes, Nodes are not ordered by chunk!
            // -------------------------------------------------------------------------------------------------------
            BlobBuilderArray<Cell> ConstructGridArray(
                ref GridCells gridCells, 
                in TerrainAspectStruct terrainStruct)
            {
                int numVerticesX = terrainStruct.Terrain.NumVerticesXY.x;
                int2 terrainQuadsXY = terrainStruct.Terrain.NumQuadsXY;
                
                BlobBuilderArray<Cell> arrayBuilder = builder.Allocate(ref gridCells.Cells, cmul(terrainQuadsXY));
                using NativeArray<float3> verticesNtv = GetOrderedVertices(chunks, meshDataArray, terrainStruct);
                NativeArray<float3> cellVertices = new (4, Temp, UninitializedMemory);
                
                for (int cellIndex = 0; cellIndex < arrayBuilder.Length; cellIndex++)
                {
                    int2 cellCoord = GetXY2(cellIndex, terrainQuadsXY.x);
                    for (int vertexIndex = 0; vertexIndex < numVerticesPerQuad; vertexIndex++)
                    {
                        int2 vertexCoord = GetXY2(vertexIndex, verticesWidth);
                        int index = mad(cellCoord.y + vertexCoord.y, numVerticesX,cellCoord.x + vertexCoord.x);
                        cellVertices[vertexIndex] = verticesNtv[index];
                    }
                    arrayBuilder[cellIndex] = new Cell(terrainQuadsXY, cellCoord, cellVertices);
                }
                return arrayBuilder;
            }
        }
        
        private NativeArray<float3> GetOrderedVertices(
            NativeArray<Entity> chunkEntities,
            MeshDataArray meshDataArray, 
            in TerrainAspectStruct terrainStruct)
        {
            int numTerrainVertices = cmul(terrainStruct.Terrain.NumVerticesXY);
            int numChunkVertices = terrainStruct.Chunk.NumVerticesPerLine * terrainStruct.Chunk.NumVerticesPerLine;
            
            NativeArray<float3> verticesNtv = new(numTerrainVertices, TempJob, UninitializedMemory);
            NativeArray<JobHandle> jobHandles = new(chunkEntities.Length, Temp, UninitializedMemory);

            for (int chunkIndex = 0; chunkIndex < chunkEntities.Length; chunkIndex++)
            {
                int2 chunkCoord = GetXY2(chunkIndex, terrainStruct.Terrain.NumChunksXY.x);

                jobHandles[chunkIndex] = new JReorderMeshVertices()
                {
                    TerrainNumVertexPerLine = terrainStruct.Terrain.NumVerticesXY.x,
                    ChunkNumVertexPerLine = terrainStruct.Chunk.NumVerticesPerLine,
                    ChunkCoord = chunkCoord,
                    ChunkPosition = SystemAPI.GetAspectRO<TransformAspect>(chunkEntities[chunkIndex]).WorldPosition,
                    //ChunkPosition = SystemAPI.GetComponent<WorldTransform>(chunkEntities[chunkIndex]).Position,
                    MeshVertices = meshDataArray[chunkIndex].GetVertexData<float3>(stream: 0),
                    OrderedVertices = verticesNtv
                }.ScheduleParallel(numChunkVertices,JobWorkerCount - 1,default);
            }
            JobHandle.CompleteAll(jobHandles);
            //Test(verticesNtv);
            return verticesNtv;
        }

        [BurstCompile(CompileSynchronously = false)]
        private partial struct JReorderMeshVertices : IJobFor
        {
            [ReadOnly] public int TerrainNumVertexPerLine;
            [ReadOnly] public int ChunkNumVertexPerLine;
            [ReadOnly] public int2 ChunkCoord;
            [ReadOnly] public float3 ChunkPosition;

            [ReadOnly, NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction]
            public NativeArray<float3> MeshVertices;
        
            [WriteOnly, NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction]
            public NativeArray<float3> OrderedVertices;
        
            public void Execute(int index)
            {
                int2 cellCoord = GetXY2(index, ChunkNumVertexPerLine);
            
                bool2 skipDuplicate = new (ChunkCoord.x > 0 && cellCoord.x == 0, ChunkCoord.y > 0 && cellCoord.y == 0);
                if (any(skipDuplicate)) return;

                int chunkNumQuadPerLine = ChunkNumVertexPerLine - 1;
                int2 offset = ChunkCoord * chunkNumQuadPerLine;
                int2 fullTerrainCoord = cellCoord + offset;
                
                // fullTerrainCoord.y * TerrainNumVertexPerLine + fullTerrainCoord.x;
                int fullMapIndex = GetIndex(fullTerrainCoord, TerrainNumVertexPerLine);
                
                OrderedVertices[fullMapIndex] = ChunkPosition + MeshVertices[index];
            }
        }
    }
}
