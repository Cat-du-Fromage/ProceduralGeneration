/*
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

using static Unity.Entities.ComponentType;
using static Unity.Mathematics.math;
using static Unity.Entities.SystemAPI;
using static Unity.Mathematics.float3;
using quaternion = Unity.Mathematics.quaternion;
using static RTTCamera.CameraUtility;

namespace RTTCamera
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(GatherCameraInputSystem))]
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
        
            //state.RequireForUpdate(query);

            query = state.GetEntityQuery
            (
                ReadOnly<Tag_Camera>(),
                ReadOnly<Data_CameraSettings>(),
                ReadOnly<Data_CameraInputs>(),
                ReadWrite<TransformAspect>()
            );
            
            state.RequireForUpdate<Tag_Camera>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { return; }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            JCameraMove job = new JCameraMove()
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            };
            job.Run();
            
            
            //foreach (var inputs in Query<TransformAspect, RefRO<Data_CameraSettings>, RefRO<Data_CameraInputs>>()){}
            
        }

        private partial struct JCameraMove : IJobEntity
        {
            public float DeltaTime;
            
            public void Execute(ref TransformAspect transform, in Data_CameraSettings settings, in Data_CameraInputs inputs)
            {
                float3 cameraForwardXZ = new float3(transform.Forward.x, 0, transform.Forward.z);
                float2 moveAxis = inputs.MoveAxis;
                
                float3 cameraRightValue = select(transform.Right, -transform.Right, moveAxis.x > 0);
                float3 xAxisRotation = select(zero, cameraRightValue, moveAxis.x != 0);

                float3 cameraForwardValue = select(-cameraForwardXZ, cameraForwardXZ, moveAxis.y > 0);
                float3 zAxisRotation = select(zero, cameraForwardValue, moveAxis.y != 0);

                float3 currentPosition = transform.Position;
                float ySpeedMultiplier = max(1f, currentPosition.y);
                int moveSpeed = settings.BaseMoveSpeed * select(1, settings.Sprint, inputs.IsSprint);

                float3 zoomPosition = inputs.Zoom * settings.ZoomSpeed * DeltaTime * up();
                float3 horizontalPosition = ySpeedMultiplier * moveSpeed * DeltaTime * (xAxisRotation + zAxisRotation);
                transform.Position = currentPosition + zoomPosition + horizontalPosition;

                //ROTATION
                if(!any(inputs.RotationDragDistanceXY)) return;
                quaternion rotationVal = transform.Rotation;
            
                float2 distanceXY = inputs.RotationDragDistanceXY;
                rotationVal = RotateFWorld(rotationVal,0f,distanceXY.x * DeltaTime,0f);//Rotation Horizontal
                transform.Rotation = rotationVal;

                rotationVal = RotateFSelf(rotationVal,-distanceXY.y * DeltaTime,0f,0f);//Rotation Vertical
                float angleX = clampAngle(degrees(rotationVal.ToEulerAngles(RotationOrder.ZXY).x), settings.MinClamp, settings.MaxClamp);
                
                float2 currentRotationEulerYZ = transform.Rotation.ToEulerAngles(RotationOrder.ZXY).yz;
                //transform.RotateLocal(quaternion.EulerZXY(new float3(radians(angleX), currentRotationEulerYZ)));
                transform.Rotation = quaternion.EulerZXY(new float3(radians(angleX), currentRotationEulerYZ));
            }
        }
    }

}
*/