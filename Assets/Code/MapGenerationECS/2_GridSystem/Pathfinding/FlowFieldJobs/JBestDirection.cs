using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace KWZTerrainECS
{
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

            int2 currentCellCoord = Utilities.GetXY2(index, NumCellX);
            NativeList<int> neighbors = GetNeighborCells(index, currentCellCoord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                int currentNeighbor = neighbors[i];
                if (BestCostField[currentNeighbor] < currentBestCost)
                {
                    currentBestCost = BestCostField[currentNeighbor];
                    int2 neighborCoord = Utilities.GetXY2(currentNeighbor, NumCellX);
                    int2 bestDirection = neighborCoord - currentCellCoord;
                    CellBestDirection[index] = new FlowFieldDirection(bestDirection);
                    //CellBestDirection[index] = new float3(bestDirection.x, 0, bestDirection.y);
                }
            }
        }

        private NativeList<int> GetNeighborCells(int index, in int2 coord)
        {
            NativeList<int> neighbors = new (4, Allocator.Temp);
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
            return job.ScheduleParallel(cellBestDirection.Length, JobsUtility.JobWorkerCount - 1, dependency);
        }
    }
}