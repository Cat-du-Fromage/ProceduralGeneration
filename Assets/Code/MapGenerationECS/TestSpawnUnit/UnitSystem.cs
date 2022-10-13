using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;
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
            Entity terrain = terrainQuery.GetSingletonEntity();
            Entity prefab = GetComponent<PrefabUnit>(terrain).Prefab;
            CreateUnits(prefab);
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
        private void CreateUnits(Entity prefab)
        {
            Entity terrain = terrainQuery.GetSingletonEntity();
            ref GridCells gridSystem = ref GetComponent<BlobCells>(terrain).Blob.Value;
            
            NativeArray<Cell> spawnCells = gridSystem.GetCellsAtChunk(0,Temp);
            NativeArray<Entity> units = EntityManager.Instantiate(prefab, spawnCells.Length, Temp);

            for (int i = 0; i < units.Length; i++)
            {
                SetComponent(units[i], new Translation(){Value = spawnCells[i].Center});
            }
            EntityManager.AddComponent<TagUnit>(units);
        }

        // When reach destination
        private void DestroyUnits()
        {
            EntityQuery unitQuery = GetEntityQuery(typeof(TagUnit));
            EntityManager.DestroyEntity(unitQuery);
        }
    }
}
