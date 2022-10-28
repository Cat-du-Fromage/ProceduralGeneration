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
using static Unity.Jobs.LowLevel.Unsafe.JobsUtility;
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
            AddPathsComponentToChunks(terrainStruct);
            
            //CreateGridCells();
        }
        
        

        protected override void OnUpdate()
        {
            
        }

        private void AddPathsComponentToChunks(TerrainAspectStruct terrainStruct)
        {
            DynamicBuffer<BufferChunk> chunksBuffer = GetBuffer<BufferChunk>(terrainEntity, true);
            NativeArray<Entity> chunks = chunksBuffer.Reinterpret<Entity>().ToNativeArray(Temp);
            
            EntityCommandBuffer ecb = new (Temp);
            int numChunk = cmul(terrainStruct.Terrain.NumChunksXY);
            for (int i = 0; i < numChunk; i++)
            {
                ecb.AddBuffer<TopPathBuffer>(chunks[i]);
                ecb.AddBuffer<BottomPathBuffer>(chunks[i]);
                ecb.AddBuffer<RightPathBuffer>(chunks[i]);
                ecb.AddBuffer<LeftPathBuffer>(chunks[i]);
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
    
    public partial struct JCostField : IJobFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<bool> Obstacles;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<byte> CostField;

        public JCostField(NativeArray<bool> obstacles, NativeArray<byte> costField)
        {
            Obstacles = obstacles;
            CostField = costField;
        }

        public void Execute(int index)
        {
            CostField[index] = (byte)select(1, byte.MaxValue, Obstacles[index]);
        }

        public static JobHandle Process(NativeArray<bool> obstacles, NativeArray<byte> costField, JobHandle dependency = default)
        {
            JCostField job = new (obstacles, costField);
            return job.ScheduleParallel(costField.Length, JobWorkerCount - 1, dependency);
        }
    }
    
    public partial struct JIntegrationField : IJob
    {
        [ReadOnly] public int ChunkQuadPerLine;

        [ReadOnly] public NativeArray<GateWay> GateWays;
        public NativeArray<byte> CostField;
        public NativeArray<int> BestCostField;

        public void Execute()
        {
            NativeQueue<int> cellsToCheck = new (Temp);
            NativeList<int> currentNeighbors = new (4, Temp);

            for (int i = 0; i < GateWays.Length; i++)
            {
                int gateIndex = GateWays[i].ChunkCellIndex;
                //Set Destination cell cost at 0
                CostField[gateIndex] = 0;
                BestCostField[gateIndex] = 0;

                cellsToCheck.Enqueue(gateIndex);
            
                while (!cellsToCheck.IsEmpty())
                {
                    int currentCellIndex = cellsToCheck.Dequeue();
                    GetNeighborCells(currentCellIndex, currentNeighbors);
                    foreach (int neighborCellIndex in currentNeighbors)
                    {
                        byte costNeighbor = CostField[neighborCellIndex];
                        int currentBestCost = BestCostField[currentCellIndex];

                        if (costNeighbor >= byte.MaxValue) continue;
                        if (costNeighbor + currentBestCost < BestCostField[neighborCellIndex])
                        {
                            BestCostField[neighborCellIndex] = costNeighbor + currentBestCost;
                            cellsToCheck.Enqueue(neighborCellIndex);
                        }
                    }
                    currentNeighbors.Clear();
                }
            }
        }
        private readonly void GetNeighborCells(int index, NativeList<int> curNeighbors)
        {
            int2 coord = GetXY2(index, ChunkQuadPerLine);
            for (int i = 0; i < 4; i++)
            {
                int neighborId = index.AdjCellFromIndex((1 << i), coord, ChunkQuadPerLine);
                if (neighborId == -1) continue;
                curNeighbors.AddNoResize(neighborId);
            }
        }
        
        public static JobHandle Process(int chunkQuadPerLine, 
            NativeArray<GateWay> gateWays,
            NativeArray<byte> costField,
            NativeArray<int> bestCostField,
            JobHandle dependency = default)
        {
            JIntegrationField job = new()
            {
                ChunkQuadPerLine = chunkQuadPerLine,
                GateWays = gateWays,
                CostField = costField,
                BestCostField = bestCostField
            };
            return job.Schedule(dependency);
        }
    }
    
    public partial struct JBestDirection : IJobFor
    {
        [ReadOnly] public ESides DefaultSide;
        [ReadOnly] public int NumCellX;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<int> BestCostField;
        //[WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float3> CellBestDirection;
        [WriteOnly, NativeDisableParallelForRestriction] 
        public NativeArray<FlowFieldDirection> CellBestDirection;
        public void Execute(int index)
        {
            int currentBestCost = BestCostField[index];

            if (currentBestCost >= ushort.MaxValue)
            {
                CellBestDirection[index] = new FlowFieldDirection(DefaultSide.Opposite());
                return;
            }

            int2 currentCellCoord = GetXY2(index, NumCellX);
            NativeList<int> neighbors = GetNeighborCells(index, currentCellCoord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                int currentNeighbor = neighbors[i];
                if (BestCostField[currentNeighbor] < currentBestCost)
                {
                    currentBestCost = BestCostField[currentNeighbor];
                    int2 neighborCoord = GetXY2(currentNeighbor, NumCellX);
                    int2 bestDirection = neighborCoord - currentCellCoord;
                    CellBestDirection[index] = new FlowFieldDirection(bestDirection);
                    //CellBestDirection[index] = new float3(bestDirection.x, 0, bestDirection.y);
                }
            }
        }

        private NativeList<int> GetNeighborCells(int index, in int2 coord)
        {
            NativeList<int> neighbors = new (4, Temp);
            for (int i = 0; i < 4; i++)
            {
                int neighborId = index.AdjCellFromIndex((1 << i), coord, NumCellX);
                if (neighborId == -1) continue;
                neighbors.AddNoResize(neighborId);
            }
            return neighbors;
        }

        public static JobHandle Process(
            ESides side,
            int chunkQuadPerLine, 
            NativeArray<int> bestCostField,
            NativeArray<FlowFieldDirection> cellBestDirection,
            JobHandle dependency = default)
        {
            JBestDirection job = new()
            {
                DefaultSide = side,
                NumCellX = chunkQuadPerLine,
                BestCostField = bestCostField,
                CellBestDirection = cellBestDirection
            };
            return job.ScheduleParallel(cellBestDirection.Length, JobWorkerCount - 1, dependency);
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
