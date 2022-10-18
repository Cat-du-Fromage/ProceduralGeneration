using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

using static UnityEngine.Mesh;

using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;

namespace KWZTerrainECS
{
    public static class EntityManagerExtension
    {
        public static MeshDataArray GetEntitiesMeshDataArray(this EntityManager em, NativeArray<Entity> entities)
        {
            Mesh[] meshes = new Mesh[entities.Length];
            for (int i = 0; i < entities.Length; i++)
            {
                meshes[i] = em.GetSharedComponentManaged<RenderMeshArray>(entities[i]).Meshes[0];
            }
            return AcquireReadOnlyMeshData(meshes);
        }
        
        public static NativeArray<T> GetEntitiesComponentsArray<T>(this EntityManager em, NativeArray<Entity> entities)
        where T : unmanaged, IComponentData
        {
            NativeArray<T> positions = new(entities.Length, TempJob, UninitializedMemory);
            for (int i = 0; i < entities.Length; i++)
            {
                positions[i] = em.GetComponentData<T>(entities[i]);
            }
            return positions;
        }
    }
}
