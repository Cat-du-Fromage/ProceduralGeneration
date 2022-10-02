using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using static Unity.Mathematics.math;

namespace RTTCamera
{
    public class CameraBaker : MonoBehaviour
    {
        public CameraInputData CameraData;
        public bool BoxSelection;
        
        private class CameraAuthoring : Baker<CameraBaker>
        {
            public override void Bake(CameraBaker authoring)
            {
                DependsOn(authoring.CameraData);
                AddComponent<Tag_Camera>();
            
                AddComponentObject(new Object_InputsControl());
                AddComponent<Data_CameraInputs>();
            
                Data_CameraSettings settings = new Data_CameraSettings
                {
                    RotationSpeed = max(1,authoring.CameraData.rotationSpeed/10),
                    BaseMoveSpeed = authoring.CameraData.baseMoveSpeed,
                    ZoomSpeed = authoring.CameraData.zoomSpeed,
                    Sprint = authoring.CameraData.sprint,
                    MaxClamp = authoring.CameraData.MaxClamp,
                    MinClamp = authoring.CameraData.MinClamp
                };
                AddComponent(settings);

                if (!authoring.BoxSelection) return;
                AddComponent<Tag_SelectionBox>();
            }
        }
    }
}
