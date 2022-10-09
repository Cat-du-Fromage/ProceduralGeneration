using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

using static Unity.Entities.ComponentType;

using static Unity.Jobs.LowLevel.Unsafe.JobsUtility;
using static Unity.Mathematics.math;
using static UnityEngine.Mesh;
using static KWZTerrainECS.Utilities;

using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;

namespace KWZTerrainECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(TerrainInitializationSystem))]
    public partial class GridInitializationSystem : SystemBase
    {
        private EntityQuery unInitializeGridTerrainQuery;
        private EntityQuery chunksQuery;

        protected override void OnCreate()
        {
            unInitializeGridTerrainQuery = new EntityQueryBuilder(Temp)
            .WithAll<TagUnInitializeGrid>()
            .WithNone<TagUnInitializeTerrain>()
            .Build(this);
            
            chunksQuery = new EntityQueryBuilder(Temp)
            .WithAll<TagChunk, Translation>()
            .Build(this);
            
            RequireForUpdate(unInitializeGridTerrainQuery);
        }

        protected override void OnUpdate()
        {
            Entity terrain = GetSingletonEntity<TagTerrain>();
            TerrainAspectStruct terrainStruct = new (EntityManager.GetAspectRO<TerrainAspect>(terrain));
            GenerateGridTerrain(terrain, terrainStruct);
            EntityManager.RemoveComponent<TagUnInitializeGrid>(terrain);
        }

        private void GenerateGridTerrain(Entity terrainEntity, in TerrainAspectStruct terrainStruct)
        {
            using NativeArray<Entity> chunkEntities = chunksQuery.ToEntityArray(TempJob);
            using MeshDataArray meshDataArray = GetChunksMeshDataArray(chunkEntities);
            
            BlobAssetReference<GridCells> blob = CreateGridCells(meshDataArray, terrainStruct, chunkEntities);
            EntityManager.AddComponentData(terrainEntity, new BlobCells() { Blob = blob });

            DynamicBuffer<ChunkNodeGrid> buffer = EntityManager.AddBuffer<ChunkNodeGrid>(terrainEntity);
            buffer.BuildGrid(terrainStruct.Chunk.NumQuadPerLine, terrainStruct.Terrain.NumChunksXY);
            
            // -------------------------------------------------------------------------------------------------------
            // internal methods
            // -------------------------------------------------------------------------------------------------------
            MeshDataArray GetChunksMeshDataArray(NativeArray<Entity> entities)
            {
                Mesh[] meshes = new Mesh[entities.Length];
                for (int i = 0; i < entities.Length; i++)
                {
                    meshes[i] = EntityManager.GetSharedComponentManaged<RenderMeshArray>(entities[i]).Meshes[0];
                }
                return AcquireReadOnlyMeshData(meshes);
            }
        }
        
        private BlobAssetReference<GridCells> CreateGridCells(
            MeshDataArray meshDataArray,
            in TerrainAspectStruct terrainStruct,
            NativeArray<Entity> chunkEntities)
        {
            BlobBuilder builder = new BlobBuilder(Allocator.Temp);
            
            ref GridCells gridCells = ref builder.ConstructRoot<GridCells>();
            BlobBuilderArray<Cell> arrayBuilder = ConstructGridArray(ref gridCells, terrainStruct);
            
            BlobAssetReference<GridCells> result = builder.CreateBlobAssetReference<GridCells>(Persistent);
            builder.Dispose();
            return result; 
            
            // -------------------------------------------------------------------------------------------------------
            // INNER METHODS : GET CHUNK POSITIONS
            // -------------------------------------------------------------------------------------------------------
            BlobBuilderArray<Cell> ConstructGridArray(ref GridCells gridCells, in TerrainAspectStruct terrainStruct)
            {
                int numVerticesX = terrainStruct.Terrain.NumVerticesXY.x;
                int2 terrainQuadsXY = terrainStruct.Terrain.NumQuadsXY;
                
                BlobBuilderArray<Cell> arrayBuilder = builder.Allocate(ref gridCells.Cells, cmul(terrainQuadsXY));
                using NativeArray<float3> verticesNtv = GetOrderedVertices(chunkEntities, meshDataArray, terrainStruct);
                
                NativeArray<float3> cellVertices = new (4, Temp, UninitializedMemory);
                for (int cellIndex = 0; cellIndex < arrayBuilder.Length; cellIndex++)
                {
                    (int x, int y) = GetXY(cellIndex, terrainQuadsXY.x);
                    for (int vertexIndex = 0; vertexIndex < 4; vertexIndex++)
                    {
                        (int xV, int yV) = GetXY(vertexIndex, 2);
                        int index = mad(y + yV, numVerticesX, x + xV);
                        cellVertices[vertexIndex] = verticesNtv[index];
                    }
                    arrayBuilder[cellIndex] = new Cell(terrainQuadsXY, x, y, cellVertices);
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
            NativeArray<float3> chunkPositions = chunksQuery.ToComponentDataArray<Translation>(TempJob).Reinterpret<float3>();
            //NativeArray<float3> chunkPosition = GetChunkPosition(chunkEntities.Length);
            
            NativeArray<JobHandle> jobHandles = new(chunkEntities.Length, Temp, UninitializedMemory);
            for (int chunkIndex = 0; chunkIndex < chunkEntities.Length; chunkIndex++)
            {
                int2 chunkCoord = GetXY2(chunkIndex, terrainStruct.Terrain.NumChunksXY.x);
                
                jobHandles[chunkIndex] = new JReorderMeshVertices()
                {
                    ChunkIndex = chunkIndex,
                    ChunkCoord = chunkCoord,
                    TerrainNumVertexPerLine = terrainStruct.Terrain.NumVerticesXY.x,
                    ChunkNumVertexPerLine = terrainStruct.Chunk.NumVerticesPerLine,
                    ChunkPositions = chunkPositions,
                    MeshVertices = meshDataArray[chunkIndex].GetVertexData<float3>(stream: 0),
                    OrderedVertices = verticesNtv
                }.ScheduleParallel(numChunkVertices,JobWorkerCount - 1,default);
            }
            JobHandle.CompleteAll(jobHandles);
            chunkPositions.Dispose();
            return verticesNtv;
            /*
            // --------------------------------------------------------------------------------------------------------
            // INNER METHODS : GET CHUNK POSITIONS
            // --------------------------------------------------------------------------------------------------------
            NativeArray<float3> GetChunkPosition(int length)
            {
                ComponentLookup<Translation> translations = SystemAPI.GetComponentLookup<Translation>(true);
                chunksQuery.ToComponentDataArray<Translation>(TempJob);
                NativeArray<float3> positions = new(length, TempJob, UninitializedMemory);
                for (int i = 0; i < chunkEntities.Length; i++)
                {
                    Entity chunk = chunkEntities[i];
                    positions[i] = translations[chunk].Value;
                }
                return positions;
            }
            */
        }


        [BurstCompile]
        private struct JReorderMeshVertices : IJobFor
        {
            [ReadOnly] public int ChunkIndex;
            [ReadOnly] public int2 ChunkCoord;
        
            [ReadOnly] public int TerrainNumVertexPerLine;
            [ReadOnly] public int ChunkNumVertexPerLine;
        
            [ReadOnly, NativeDisableParallelForRestriction] 
            public NativeArray<float3> ChunkPositions;
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
            
                int fullMapIndex = fullTerrainCoord.y * TerrainNumVertexPerLine + fullTerrainCoord.x;
                OrderedVertices[fullMapIndex] = ChunkPositions[ChunkIndex] + MeshVertices[index];
            }
        }
        /*
        public NativeArray<int> GetQuadsIndexOrderedByChunk(in TerrainAspectStruct terrainStruct)
        {
            int terrainNumQuads = cmul(terrainStruct.Terrain.NumQuadsXY);
            NativeArray<int> nativeOrderedIndices = new (terrainNumQuads, TempJob, UninitializedMemory);

            JOrderArrayIndexByChunkIndex job = new()
            {
                CellSize = 1,
                ChunkSize = terrainStruct.Chunk.NumQuadPerLine,
                NumCellX = terrainStruct.Terrain.NumQuadsXY.x,
                NumChunkX = terrainStruct.Terrain.NumChunksXY.x,
                SortedArray = nativeOrderedIndices
            };
            JobHandle jobHandle = job.ScheduleParallel(terrainNumQuads, JobWorkerCount - 1, default);
            jobHandle.Complete();
            return nativeOrderedIndices;
        }
        */
        [BurstCompile(CompileSynchronously = true)]
        private struct JOrderArrayIndexByChunkIndex : IJobFor
        {
            [ReadOnly] public int CellSize;
            [ReadOnly] public int ChunkSize;
            [ReadOnly] public int NumCellX;
            [ReadOnly] public int NumChunkX;
            
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<int> SortedArray;

            public void Execute(int index)
            {
                int2 cellCoord = GetXY2(index, NumCellX);
                
                float ratio = CellSize / (float)ChunkSize; //CAREFULL! NOT ChunkCellWidth but Cell compare to Chunk!
                int2 chunkCoord = (int2)floor((float2)cellCoord * ratio);
                int2 coordInChunk = cellCoord - (chunkCoord * ChunkSize);

                int indexCellInChunk = mad(coordInChunk.y, ChunkSize,coordInChunk.x );
                int chunkIndex =  mad(chunkCoord.y, NumChunkX, chunkCoord.x);
                int totalCellInChunk = ChunkSize * ChunkSize;
                
                int indexFinal = mad(chunkIndex, totalCellInChunk, indexCellInChunk);

                SortedArray[indexFinal] = index;
            }

            public static NativeArray<int> GetQuadsIndexOrderedByChunk(in TerrainAspectStruct terrainStruct)
            {
                int terrainNumQuads = cmul(terrainStruct.Terrain.NumQuadsXY);
                NativeArray<int> nativeOrderedIndices = new (terrainNumQuads, TempJob, UninitializedMemory);

                JOrderArrayIndexByChunkIndex job = new()
                {
                    CellSize = 1,
                    ChunkSize = terrainStruct.Chunk.NumQuadPerLine,
                    NumCellX = terrainStruct.Terrain.NumQuadsXY.x,
                    NumChunkX = terrainStruct.Terrain.NumChunksXY.x,
                    SortedArray = nativeOrderedIndices
                };
                JobHandle jobHandle = job.ScheduleParallel(terrainNumQuads, JobWorkerCount - 1, default);
                jobHandle.Complete();
                return nativeOrderedIndices;
            }
        }
    }
}
