using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

using static Unity.Mathematics.math;
using static KWZTerrainECS.Utilities;
using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;
using RaycastHit = Unity.Physics.RaycastHit;

namespace KWZTerrainECS
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(GridInitializationSystem)), UpdateAfter(typeof(GridInitializationSystem))]
    public partial class UnitSystem : SystemBase
    {
        private BeginInitializationEntityCommandBufferSystem beginInitSys;
        
        private readonly float screenWidth = Screen.width;
        private readonly float screenHeight = Screen.height;
        
        private EntityQuery terrainQuery;
        private EntityQuery cameraQuery;
        private EntityQuery unitQuery;
        
        private Entity TerrainEntity;

        private Entity cameraEntity;
        private Mouse mouse;
        private Camera playerCamera;

        protected override void OnCreate()
        {
            cameraQuery = GetEntityQuery(typeof(Camera));
            
            terrainQuery = new EntityQueryBuilder(Temp)
                .WithAll<TagTerrain>()
                .WithNone<TagUnInitializeTerrain, TagUnInitializeGrid>()
                .Build(this);
            
            unitQuery = new EntityQueryBuilder(Temp)
                .WithAll<TagUnit, EnableChunkDestination>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build(this);
            
            beginInitSys = World.GetExistingSystemManaged<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnStartRunning()
        {
            TerrainEntity = terrainQuery.GetSingletonEntity();

            CreateUnits(TerrainEntity, 0);
            
            cameraEntity = cameraQuery.GetSingletonEntity();
            playerCamera = EntityManager.GetComponentObject<Camera>(cameraEntity);

            mouse = Mouse.current;
        }

        protected override void OnUpdate()
        {
            OrderUnitsMove();
            return;
        }
        
        //Order Move by Mouse Click
        private void OrderUnitsMove()
        {
            if (!mouse.rightButton.wasReleasedThisFrame) return;
            
            float2 mousePosition = mouse.position.ReadValue();
            if (TerrainRaycast(out RaycastHit hit, mousePosition, 100))
            {
                //TerrainAspect terrainAspect = EntityManager.GetAspectRO<TerrainAspect>(TerrainEntity);
                //TestCreateEntityAt(hit.Position);
                AssignDestinationToUnits();
            }

            void AssignDestinationToUnits()
            {
                int2 numChunkXY = GetComponent<DataTerrain>(TerrainEntity).NumChunksXY;
                
                int chunkQuadsPerLine = GetComponent<DataChunk>(TerrainEntity).NumQuadPerLine;
                int chunkIndex = ChunkIndexFromPosition(hit.Position, numChunkXY, chunkQuadsPerLine);
                unitQuery.SetEnabledBitsOnAllChunks<EnableChunkDestination>(true);
                
                Entities
                .WithBurst()
                .WithStoreEntityQueryInField(ref unitQuery)
                .ForEach((Entity ent, int entityInQueryIndex, ref EnableChunkDestination chunkDest) =>
                {
                    chunkDest.Index = chunkIndex;
                }).ScheduleParallel();
                
                
            }
            
            //Met une destination
            //Utiliser le EnableComponent ! pour le move

            //Savoir par quel chunkPasser
            void GetChunksPath(int chunkStartIndex, int chunkDestIndex, int2 numChunkXY)
            {
                //Get current chunk in
                using NativeList<int> pathList = new (cmul(numChunkXY),TempJob);
                JAStar aStar = new (chunkStartIndex, chunkDestIndex, numChunkXY, pathList);
                JobHandle jobHandle = aStar.Schedule();
                //Get chunkDestination
                //Calculate A* on chunks
            }
        }

        // On Update when they have a destination
        private void OnMoveUnits()
        {
            
        }

        private void TestCreateEntityAt(float3 position)
        {
            Entity terrain = terrainQuery.GetSingletonEntity();
            Entity prefab = GetComponent<PrefabUnit>(terrain).Prefab;
            Entity spawn = EntityManager.Instantiate(prefab);
            SetComponent(spawn, new Translation(){Value = position});
        }

        // On arbitrary key pressed
        private void CreateUnits(Entity terrain, int spawnIndex)
        {
            Entity prefab = GetComponent<PrefabUnit>(terrain).Prefab;
            ref GridCells gridSystem = ref GetComponent<BlobCells>(terrain).Blob.Value;
            
            NativeArray<Cell> spawnCells = gridSystem.GetCellsAtChunk(spawnIndex,Temp);
            NativeArray<Entity> units = EntityManager.Instantiate(prefab, spawnCells.Length, Temp);
            
            EntityManager.AddComponent<TagUnit>(units);
            EntityManager.AddComponent<EnableChunkDestination>(units);
            
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i];
                EntityManager.SetName(unit, $"UnitTest_{i}");
                SetComponent(unit, new Translation(){Value = spawnCells[i].Center});
                SetComponent(unit, new EnableChunkDestination(){Index = spawnIndex});
                EntityManager.SetComponentEnabled<EnableChunkDestination>(unit, false);
            }

        }

        // When reach destination
        private void DestroyUnits()
        {
            //EntityQuery unitQuery = GetEntityQuery(typeof(TagUnit));
            EntityManager.DestroyEntity(unitQuery);
        }
        
        //==============================================================================================================
        //Mouses Positions
        private bool TerrainRaycast(out RaycastHit hit, in float2 mousePosition, float distance)
        {
            float3 origin = GetComponent<Translation>(cameraEntity).Value;
            float3 direction = playerCamera.ScreenToWorldDirection(mousePosition, screenWidth, screenHeight);
            return PhysicsUtilities.Raycast(out hit, origin, direction, distance, 0);
        }
        //==============================================================================================================

        private void GetSharedUnitsPath()
        {
            NativeList<int> SharedStart = new NativeList<int>(Allocator.TempJob);

            //Job 
            
            NativeArraySharedInt sharedStartChunkIndex = new NativeArraySharedInt(SharedStart.AsArray(), TempJob);
            sharedStartChunkIndex.Schedule(default).Complete();

            int numSharedValue = sharedStartChunkIndex.GetSharedValueIndexCountArray().Length;
            
            // foreach Unique start index calculate Pathfinding
            for (int i = 0; i < numSharedValue; i++)
            {
                
            }
            
            // foreach Units => depending of units current chunk index
            // Get path corresponding to path[0] (unit_startIndex == path[0])
            
        }
    }

    [BurstCompile]
    public partial struct JTest : IJobEntity
    {
        [ReadOnly] public int ChunkDestinationIndex;
        [ReadOnly] public int ChunkQuadsPerLine;
        [ReadOnly] public int2 NumChunkXY;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeList<int>.ParallelWriter ChunkStartIndices;
        
        /*[EntityInQueryIndex] int entityInQueryIndex, */
        public void Execute(in Translation position, ref EnableChunkDestination enableDest)
        {
            int startChunkIndex = ChunkIndexFromPosition(position.Value, NumChunkXY, ChunkQuadsPerLine);
            ChunkStartIndices.AddNoResize(startChunkIndex);
            enableDest.Index = ChunkDestinationIndex;
        }
    }
}
