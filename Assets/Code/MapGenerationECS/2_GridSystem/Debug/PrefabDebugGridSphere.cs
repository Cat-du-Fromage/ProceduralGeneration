using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.Entities;
using UnityEngine;

namespace KWZTerrainECS
{
    public struct PrefabDebugGridSphere : IComponentData
    {
        public Entity Prefab;
    }
}
