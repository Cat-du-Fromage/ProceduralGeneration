using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

using static Unity.Entities.World;
using static Unity.Jobs.LowLevel.Unsafe.JobsUtility;
using static Unity.Collections.Allocator;
using RaycastHit = Unity.Physics.RaycastHit;

namespace KWZTerrainECS
{
    public static class PhysicsUtilities
    {
        public static bool Raycast(out RaycastHit hit, in float3 origin, in float3 direction, float distance, int mask)
        {
            // Set up Entity Query to get PhysicsWorldSingleton
            // If doing this in SystemBase or ISystem
            // , call GetSingleton<PhysicsWorldSingleton>()/SystemAPI.GetSingleton<PhysicsWorldSingleton>() directly.
            EntityQueryBuilder builder = new EntityQueryBuilder(Temp).WithAll<PhysicsWorldSingleton>();
            EntityQuery singletonQuery = DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(builder);
            CollisionWorld collisionWorld = singletonQuery.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            singletonQuery.Dispose();
            //PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

            RaycastInput input = new RaycastInput()
            {
                Start = origin,
                End = origin + direction * distance,
                Filter = new CollisionFilter()
                {
                    BelongsTo = ~0u,
                    CollidesWith = 1u << mask, // all 1s, so all layers, collide with everything
                    GroupIndex = 0
                }
            };
            bool haveHit = collisionWorld.CastRay(input, out hit);
            return haveHit;
        }

        public static JobHandle ScheduleBatchRayCast(this PhysicsWorldSingleton physicsWorld,
            NativeArray<RaycastInput> rayInputs, NativeArray<RaycastHit> rayCastResults, JobHandle dependency = default)
        {
            int numBatch = rayInputs.Length <= JobWorkerCount - 1 ? rayInputs.Length : JobWorkerCount - 1;
            JobHandle rcj = new RaycastJob
            {
                PhysicsWorld = physicsWorld,
                RayInputs = rayInputs,
                RayCastResults = rayCastResults,
            }.ScheduleParallel(rayInputs.Length, numBatch, dependency);
            return rcj;
        }

        [BurstCompile]
        private partial struct RaycastJob : IJobFor
        {
            [ReadOnly] public PhysicsWorldSingleton PhysicsWorld;
            [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<RaycastInput> RayInputs;
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<RaycastHit> RayCastResults;

            public void Execute(int index)
            {
                PhysicsWorld.CastRay(RayInputs[index], out RaycastHit hit);
                RayCastResults[index] = hit;
            }
        }
        
        [BurstCompile]
        private partial struct JSingleRaycast : IJob
        {
            [ReadOnly] public PhysicsWorldSingleton PhysicsWorld;
            [ReadOnly] public RaycastInput RayInput;
            [WriteOnly] public RaycastHit RayCastResult;

            public void Execute()
            {
                PhysicsWorld.CastRay(RayInput, out RaycastHit hit);
                RayCastResult = hit;
            }
        }
    }
}
