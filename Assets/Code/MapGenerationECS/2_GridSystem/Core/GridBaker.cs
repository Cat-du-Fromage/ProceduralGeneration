using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace KWZTerrainECS
{
    public class GridBaker : MonoBehaviour
    {
        [SerializeField] private GameObject GridDebugPrefab;
        private class GridAuthoring : Baker<GridBaker>
        {
            public override void Bake(GridBaker authoring)
            {
                AddComponent<TagUnInitializeGrid>();
                #if UNITY_EDITOR
                AddComponent(GetEntity(), new PrefabDebugGridSphere()
                {
                    Prefab = GetEntity(authoring.GridDebugPrefab)
                });
                #endif
            }
        }
    }
}
