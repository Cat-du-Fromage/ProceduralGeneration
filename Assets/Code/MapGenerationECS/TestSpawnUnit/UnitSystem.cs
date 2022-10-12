using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;

namespace KWZTerrainECS
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(GridInitializationSystem))]
    public partial class UnitSystem : SystemBase
    {
        private EntityQuery terrainQuery;

        protected override void OnCreate()
        {
            terrainQuery = new EntityQueryBuilder(Temp)
                .WithAll<TagTerrain>()
                .WithNone<TagUnInitializeTerrain, TagUnInitializeGrid>()
                .Build(this);
        }

        protected override void OnStartRunning()
        {
            return;
        }

        protected override void OnUpdate()
        {
            return;
        }

        // On Update when they have a destination
        private void MoveUnits()
        {

        }

        // On arbitrary key pressed
        private void CreateUnits()
        {
            
        }

        // When reach destination
        private void DestroyUnits()
        {
            
        }
    }
}
