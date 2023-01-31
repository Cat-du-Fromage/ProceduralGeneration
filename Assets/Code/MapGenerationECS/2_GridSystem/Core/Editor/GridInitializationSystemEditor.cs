using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using static Unity.Jobs.LowLevel.Unsafe.JobsUtility;
using static Unity.Mathematics.math;
using static UnityEngine.Mesh;
using static KWZTerrainECS.Utilities;

using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;

#if UNITY_EDITOR
namespace KWZTerrainECS
{
    public partial class GridInitializationSystem : SystemBase
    {
        private void Test(NativeArray<float3> verticesNtv)
        {
            Entity terrain = SystemAPI.GetSingletonEntity<TagTerrain>();
            NativeArray<Entity> gridCheck = new(verticesNtv.Length, Temp, UninitializedMemory);
            Entity prefabDebug = SystemAPI.GetComponent<PrefabDebugGridSphere>(terrain).Prefab;
            EntityManager.Instantiate(prefabDebug, gridCheck);
            
            for (int j = 0; j < verticesNtv.Length; j++)
            {
                Entity sphereDebug = gridCheck[j];
                EntityManager.SetName(sphereDebug, $"cell_{j}");
                SystemAPI.SetComponent(sphereDebug, new WorldTransform(){Position = verticesNtv[j]});
            }
        }

        private void Test2(ref BlobArray<Cell> cells)
        {
            Entity terrain = GetSingletonEntity<TagTerrain>();
            NativeArray<Entity> gridCheck = new(cells.Length*4, Temp, UninitializedMemory);
            Entity prefabDebug = GetComponent<PrefabDebugGridSphere>(terrain).Prefab;
            EntityManager.Instantiate(prefabDebug, gridCheck);
            int index = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                Cell cell = cells[i];
                for (int j = 0; j < cell.Vertices.Length; j++)
                {
                    Entity sphereDebug = gridCheck[index];
                    EntityManager.SetName(sphereDebug, $"cell_{i}_{j}");
                    SystemAPI.GetComponent<WorldTransform>(sphereDebug).Translate(cell.Vertices[j]);
                    //SystemAPI.SetComponent(sphereDebug, new WorldTransform(){Position = cell.Vertices[j]});
                    index += 1;
                }
            }
        }

    }
}
#endif