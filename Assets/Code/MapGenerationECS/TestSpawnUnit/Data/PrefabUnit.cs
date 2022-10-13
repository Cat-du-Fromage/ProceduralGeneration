using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace KWZTerrainECS
{
    public struct PrefabUnit : IComponentData
    {
        public Entity Prefab;
    }
}
