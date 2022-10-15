using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace KWZTerrainECS
{
    public struct EnableChunkDestination : IComponentData, IEnableableComponent
    {
        public int Index;
    }
}
