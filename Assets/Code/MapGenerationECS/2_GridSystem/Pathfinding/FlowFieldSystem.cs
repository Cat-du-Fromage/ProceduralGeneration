using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static UnityEngine.Mesh;
using static KWZTerrainECS.Utilities;

using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
using int2 = Unity.Mathematics.int2;

namespace KWZTerrainECS
{
    [RequireMatchingQueriesForUpdate]
    //[CreateAfter(typeof(TerrainInitializationSystem))]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(GridInitializationSystem))]
    public partial class FlowFieldSystem : SystemBase
    {
        private Entity terrainEntity;
        private EntityQuery terrainQuery;
        private EntityQuery chunksQuery;

        protected override void OnCreate()
        {
            terrainQuery = GetEntityQuery(typeof(TagTerrain));
            chunksQuery = GetEntityQuery(typeof(TagChunk));
        }

        protected override void OnStartRunning()
        {
            terrainEntity = terrainQuery.GetSingletonEntity();
            TerrainAspectStruct terrainStruct = new(SystemAPI.GetAspectRO<TerrainAspect>(terrainEntity));
            AddPathsBufferToChunks(terrainStruct);
            
            //CreateGridCells();
        }

        protected override void OnUpdate()
        {
            
        }

        private void AddPathsBufferToChunks(TerrainAspectStruct terrainStruct)
        {
            DynamicBuffer<BufferChunk> chunksBuffer = GetBuffer<BufferChunk>(terrainEntity, true);
            NativeArray<Entity> chunks = chunksBuffer.Reinterpret<Entity>().ToNativeArray(Temp);
            EntityCommandBuffer ecb = new (Temp);
            int numChunk = cmul(terrainStruct.Terrain.NumChunksXY);
            for (int i = 0; i < numChunk; i++)
            {
                Entity chunk = chunks[i];
                ecb.AddBuffer<TopPathBuffer>(chunk);
                ecb.AddBuffer<BottomPathBuffer>(chunk);
                ecb.AddBuffer<RightPathBuffer>(chunk);
                ecb.AddBuffer<LeftPathBuffer>(chunk);
            }
            ecb.Playback(EntityManager);
        }

        private void CreateGridCells()
        {
            TerrainAspectStruct terrainStruct = new(SystemAPI.GetAspectRO<TerrainAspect>(terrainEntity));
            DynamicBuffer<BufferChunk> chunksBuffer = GetBuffer<BufferChunk>(terrainEntity, true);
            using NativeArray<Entity> chunks = chunksBuffer.Reinterpret<Entity>().ToNativeArray(TempJob);
            
            EntityManager.AddComponent<PathsComponent>(chunks);
            int chunkQuadPerLine = terrainStruct.Chunk.NumQuadPerLine;

            int numChunkQuads = Square(chunkQuadPerLine);

            using NativeArray<bool> obstacles = new (numChunkQuads, TempJob);
            using NativeArray<byte> costField = new (numChunkQuads, TempJob);
            //JIntegrationField.Process(chunkQuadPerLine, )
            
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                JobHandle costFieldJh = JCostField.Process(obstacles, costField);
                costFieldJh.Complete();
                PathsComponent pathsComponent = new PathsComponent();

                //GetFlowFieldAtSide(chunkIndex, chunkQuadPerLine, costField, costFieldJh);
                for (int i = 0; i < 4; i++)
                {
                    ESides side = (ESides)i;
                    using NativeArray<int> bestCostField = new(numChunkQuads, TempJob);
                    using NativeArray<GateWay> gateWays = GetGateWaysAtChunk(chunkIndex, (ESides)i);
                    
                    JobHandle integrationJh = JIntegrationField
                        .Process(chunkQuadPerLine, gateWays, costField, bestCostField);
                    
                    using NativeArray<FlowFieldDirection> cellBestDirection = new(numChunkQuads, TempJob, UninitializedMemory);
                    JobHandle bestDirectionJh = JBestDirection
                        .Process(side, chunkQuadPerLine, bestCostField, cellBestDirection, integrationJh);
                    bestDirectionJh.Complete();
                    
                    //for (int j = 0; j < cellBestDirection.Length; j++)
                    //{
                    //    pathsComponent[side].Add(cellBestDirection[j]);
                    //}
                }
                SetComponent(chunks[chunkIndex], pathsComponent);
            }
        }

        private PathsComponent GetFlowFieldAtSide(
            int chunkIndex, 
            int chunkQuadPerLine, 
            NativeArray<byte> costField, 
            JobHandle costFieldJh)
        {
            int numChunkQuads = Square(chunkQuadPerLine);
            PathsComponent pathsComponent = new PathsComponent();
            for (int i = 0; i < 4; i++)
            {
                ESides side = (ESides)i;
                using NativeArray<int> bestCostField = new(numChunkQuads, TempJob);
                
                using NativeArray<GateWay> gateWays = GetGateWaysAtChunk(chunkIndex, (ESides)i);
                    
                JobHandle integrationJh = JIntegrationField
                    .Process(chunkQuadPerLine, gateWays, costField, bestCostField, costFieldJh);
                    
                using NativeArray<FlowFieldDirection> cellBestDirection = new(numChunkQuads, TempJob, UninitializedMemory);
                JobHandle bestDirectionJh = JBestDirection
                    .Process(side, chunkQuadPerLine, bestCostField, cellBestDirection, integrationJh);
                bestDirectionJh.Complete();

                for (int j = 0; j < cellBestDirection.Length; j++)
                {
                    pathsComponent[side].Add(cellBestDirection[j]);
                }
            }
            return pathsComponent;
        }

        private NativeArray<GateWay> GetGateWaysAtChunk(int chunkIndex, ESides side)
        {
            TerrainAspectStruct terrainStruct = new(SystemAPI.GetAspectRO<TerrainAspect>(terrainEntity));
            DynamicBuffer<ChunkNodeGrid> buffer = GetBuffer<ChunkNodeGrid>(terrainEntity);
            return buffer.GetGateWaysAt(chunkIndex, side, terrainStruct, TempJob);
        }
    }

    public partial struct JBestDirection2 : IJobFor
    {
        [ReadOnly] public ESides DefaultSide;
        [ReadOnly] public int NumCellX;
        
        [NativeDisableParallelForRestriction]
        public NativeArray<FlowFieldDirection> CellBestDirection;

        public void Execute(int index)
        {
            FlowFieldDirection defaultOpposite = new (DefaultSide.Opposite());
            FlowFieldDirection direction = CellBestDirection[index];
            //direction.Value
            if (direction == defaultOpposite) return;
            
            
            int2 currentCoord = GetXY2(index, NumCellX);
            int2 coordToCheck = currentCoord + (int2)direction.Value;
            if (IsOutOfBound(coordToCheck)) return;
            
            int indexToCheck = coordToCheck.y * NumCellX + coordToCheck.x;
            
            
        }

        private bool IsOutOfBound(int2 coordToCheck)
        {
            bool xOutBound = coordToCheck.x < 0 || coordToCheck.x > NumCellX - 1;
            bool yOutBound = coordToCheck.y < 0 || coordToCheck.y > NumCellX - 1;
            return any(new bool2(xOutBound, yOutBound));
        }
    }
}
