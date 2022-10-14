using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;
using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;
using RaycastHit = Unity.Physics.RaycastHit;

namespace KWZTerrainECS
{
    public class CameraRaycastObject : IComponentData
    {
        public Mouse Mouse;
        public Camera Camera;
        public Entity CameraEntity;
    }
    
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(GridInitializationSystem)), UpdateAfter(typeof(GridInitializationSystem))]
    public partial class UnitSystem : SystemBase
    {
        private readonly float screenWidth = Screen.width;
        private readonly float screenHeight = Screen.height;
        
        private EntityQuery terrainQuery;
        private EntityQuery cameraQuery;

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
        }

        protected override void OnStartRunning()
        {
            Entity terrain = terrainQuery.GetSingletonEntity();
            /*
            Entity cameraEntity = cameraQuery.GetSingletonEntity();
            EntityManager.AddComponentObject(terrain, new CameraRaycastObject
            {
                Mouse = Mouse.current,
                Camera = EntityManager.GetComponentObject<Camera>(cameraEntity),
                CameraEntity = cameraEntity,
            });
            */
            CreateUnits(terrain);
            
            cameraEntity = cameraQuery.GetSingletonEntity();
            playerCamera = EntityManager.GetComponentObject<Camera>(cameraEntity);
            mouse = Mouse.current;
            
            //Debug.Log(playerCamera.name);
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
                TestCreateEntityAt(hit.Position);
            }
            
            //Met une destination
            //Utiliser le EnableComponent ! pour le move

            //Savoir par quel chunkPasser
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
        private void CreateUnits(Entity terrain)
        {
            Entity prefab = GetComponent<PrefabUnit>(terrain).Prefab;
            ref GridCells gridSystem = ref GetComponent<BlobCells>(terrain).Blob.Value;
            
            NativeArray<Cell> spawnCells = gridSystem.GetCellsAtChunk(0,Temp);
            NativeArray<Entity> units = EntityManager.Instantiate(prefab, spawnCells.Length, Temp);
            
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i];
                EntityManager.SetName(unit, $"UnitTest_{i}");
                SetComponent(unit, new Translation(){Value = spawnCells[i].Center});
            }
            EntityManager.AddComponent<TagUnit>(units);
        }

        // When reach destination
        private void DestroyUnits()
        {
            EntityQuery unitQuery = GetEntityQuery(typeof(TagUnit));
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
        
    }
}
