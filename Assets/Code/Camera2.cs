using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct Test : IComponentData
{
    
}
public class Camera2 : MonoBehaviour
{
    public GameObject prefab;
        
    class CameraAuthoring : Baker<Camera2>
    {
        public override void Bake(Camera2 authoring)
        {
            AddComponent<Test>();
        }
    }
}
