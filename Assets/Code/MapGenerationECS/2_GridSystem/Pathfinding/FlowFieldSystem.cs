using System;
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

using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;

namespace KWZTerrainECS
{
    public struct FlowFieldDirection
    {
        private byte Index;

        public FlowFieldDirection(int direction)
        {
            Index = (byte)direction;
        }
        
        public FlowFieldDirection(ESides direction)
        {
            Index = direction switch
            {
                ESides.Top    => 0, //ESides.Top
                ESides.Bottom => 1, //ESides.Right
                ESides.Right  => 2, //ESides.Bottom
                ESides.Left   => 3, //ESides.Left
            };
        }

        public readonly float2 Value
        {
            get 
            {
                return Index switch
                {
                    0 => new float2(0,1), //ESides.Top
                    1 => new float2(1,0), //ESides.Right
                    2 => new float2(0,-1), //ESides.Bottom
                    3 => new float2(-1,0), //ESides.Left
                };
            }
        }
        
    }
    
    public struct PathsComponent : IComponentData
    {
        public FixedList4096Bytes<FlowFieldDirection> Top;
        public FixedList4096Bytes<FlowFieldDirection> Right;
        public FixedList4096Bytes<FlowFieldDirection> Bottom;
        public FixedList4096Bytes<FlowFieldDirection> Left;

        public readonly FixedList4096Bytes<FlowFieldDirection> this[ESides index]
        {
            get
            {
                return index switch
                {
                    ESides.Top => Top,
                    ESides.Right => Right,
                    ESides.Bottom => Bottom,
                    ESides.Left => Left,
                    _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
                };
            }
        }
    }

    public partial class FlowFieldSystem : SystemBase
    {
        private EntityQuery chunksQuery;
        protected override void OnUpdate()
        {
            
        }

        private void CreateGridCells(in TerrainAspectStruct terrainStruct)
        {
            NativeArray<Entity> chunks = chunksQuery.ToEntityArray(Temp);
            EntityManager.AddComponent<PathsComponent>(chunks);

            int numQuads = Square(terrainStruct.Chunk.NumQuadPerLine);
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                for (int quadIndex = 0; quadIndex < numQuads; quadIndex++)
                {
                    
                }
            }
        }

        private void GetFlowField()
        {
            
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
            JCostField job = new JCostField(obstacles, costField);
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
            JIntegrationField job = new JIntegrationField
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
        [ReadOnly] public int NumCellX;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<int> BestCostField;
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float3> CellBestDirection;

        public void Execute(int index)
        {
            int currentBestCost = BestCostField[index];

            if (currentBestCost >= ushort.MaxValue)
            {
                CellBestDirection[index] = float3.zero;
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
                    CellBestDirection[index] = new float3(bestDirection.x, 0, bestDirection.y);
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
    }
}
