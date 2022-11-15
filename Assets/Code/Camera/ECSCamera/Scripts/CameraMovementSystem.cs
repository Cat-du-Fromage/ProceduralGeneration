#if HYBRID_ENTITIES_CAMERA_CONVERSION
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

using static Unity.Entities.ComponentType;
using static RTTCamera.CameraUtility;
using static Unity.Mathematics.math;
using static Unity.Mathematics.float3;
using quaternion = Unity.Mathematics.quaternion;

namespace RTTCamera
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup)), UpdateAfter(typeof(GatherCameraInputSystem))]
    public partial class CameraMovementSystem : SystemBase
    {
        private EntityQuery query;
        protected override void OnCreate()
        {
            query = GetEntityQuery
            (
                ReadOnly<Tag_Camera>(),
                ReadOnly<Data_CameraSettings>(),
                ReadOnly<Data_CameraInputs>()
            );
            RequireForUpdate(query);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            Entity cameraEntity = GetSingletonEntity<Tag_Camera>();
            EntityManager.SetName(cameraEntity, "RTTCamera");
            if (HasComponent<Tag_SelectionBox>(cameraEntity))
            {
                InitializeCompanionGameObject(cameraEntity);
            }
        }

        private void InitializeCompanionGameObject(Entity cameraEntity)
        {
            Camera camera = EntityManager.GetComponentObject<Camera>(cameraEntity);
            if (!camera.gameObject.TryGetComponent(out SelectionRectangleECS comp))
            {
                comp = camera.gameObject.AddComponent<SelectionRectangleECS>();
            }
            comp.Initialize(EntityManager, cameraEntity);
        }

        protected override void OnUpdate()
        {
            float deltaTime =  SystemAPI.Time.DeltaTime;
            MoveCamera(deltaTime);
        }

        private void MoveCamera(float deltaTime)
        {
            Entities
            .WithName("CameraMovement")
            .WithBurst()
            .WithAll<Tag_Camera>()
            .ForEach((ref TransformAspect transform, in Data_CameraInputs camInputs, in Data_CameraSettings settings) => 
            {
                //TRANSLATION
                float3 cameraForwardXZ = new (transform.Forward.x, 0, transform.Forward.z);
                //half2 moveAxis = half2(camInputs.MoveAxis);
                int2 moveAxis = (int2)(camInputs.MoveAxis * 10);
                
                float3 cameraRightValue   = select(transform.Right, -transform.Right, moveAxis.x > 0);
                float3 xAxisMove          = select(zero, cameraRightValue, moveAxis.x != 0);

                float3 cameraForwardValue = select(-cameraForwardXZ, cameraForwardXZ, moveAxis.y > 0);
                float3 zAxisMove          = select(zero, cameraForwardValue, moveAxis.y != 0);
                
                float ySpeedMultiplier    = max(1f, transform.Position.y);
                int moveSpeed             = settings.BaseMoveSpeed * select(1, settings.Sprint, camInputs.IsSprint);

                float3 zoomPosition       = camInputs.Zoom * settings.ZoomSpeed * up();
                float3 horizontalPosition = ySpeedMultiplier * moveSpeed * (xAxisMove + zAxisMove);
                transform.TranslateLocal((zoomPosition + horizontalPosition) * deltaTime);
                
                //ROTATION
                if(!any(camInputs.RotationDragDistanceXY)) return;
                quaternion rotationVal = transform.Rotation;
            
                float2 distanceXY = camInputs.RotationDragDistanceXY * deltaTime;
                rotationVal = RotateFWorld(rotationVal,0f,distanceXY.x,0f);//Rotation Horizontal
                transform.Rotation = rotationVal;
                
                rotationVal = RotateFSelf(rotationVal,-distanceXY.y,0f,0f);//Rotation Vertical
                float angleX = clampAngle(degrees(rotationVal.ToEulerAngles(RotationOrder.ZXY).x), settings.MinClamp, settings.MaxClamp);
                float2 currentRotationEulerYZ = transform.Rotation.ToEulerAngles(RotationOrder.ZXY).yz;
                transform.Rotation = quaternion.EulerZXY(new float3(radians(angleX), currentRotationEulerYZ));
            }).Run();
        }
    }
    
}
#endif
