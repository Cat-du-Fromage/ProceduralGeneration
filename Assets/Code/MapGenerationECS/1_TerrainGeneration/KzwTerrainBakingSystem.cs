using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Physics.Authoring;
using UnityEngine;

namespace KWZTerrainECS
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class KzwTerrainBakingSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            
        }
    }
}
