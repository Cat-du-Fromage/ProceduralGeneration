using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

using static Unity.Entities.ComponentType;

namespace RTTCamera
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct NewCameraMovementSystem : ISystem
    {
        private EntityQuery query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Tag_Camera>()
                .WithAll<Data_CameraSettings>()
                .WithAll<Data_CameraInputs>()
                .WithAllRW<TransformAspect>()
                .Build(ref state);
            
            state.RequireForUpdate(query);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            return;
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            return;
        }
    }
}
