using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace KWZTerrainECS
{
    public partial struct JIntegrationField : IJob
    {
        [ReadOnly] public int ChunkQuadPerLine;

        [ReadOnly] public NativeArray<GateWay> GateWays;
        public NativeArray<byte> CostField;
        public NativeArray<int> BestCostField;

        public void Execute()
        {
            NativeQueue<int> cellsToCheck = new (Allocator.Temp);
            NativeList<int> currentNeighbors = new (4, Allocator.Temp);

            for (int i = 0; i < GateWays.Length; i++)
            {
                int gateIndex = GateWays[i].ChunkCellIndex;
                //Set Destination cell cost at 0
                CostField[gateIndex] = 0;
                BestCostField[gateIndex] = 0;
                cellsToCheck.Enqueue(gateIndex);
            }

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
        private readonly void GetNeighborCells(int index, NativeList<int> curNeighbors)
        {
            int2 coord = Utilities.GetXY2(index, ChunkQuadPerLine);
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
}