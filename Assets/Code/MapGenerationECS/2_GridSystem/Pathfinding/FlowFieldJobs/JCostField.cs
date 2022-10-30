using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace KWZTerrainECS
{
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
            CostField[index] = (byte)math.select(1, byte.MaxValue, Obstacles[index]);
        }

        public static JobHandle Process(NativeArray<bool> obstacles, NativeArray<byte> costField, JobHandle dependency = default)
        {
            JCostField job = new (obstacles, costField);
            return job.ScheduleParallel(costField.Length, JobsUtility.JobWorkerCount - 1, dependency);
        }
    }
}