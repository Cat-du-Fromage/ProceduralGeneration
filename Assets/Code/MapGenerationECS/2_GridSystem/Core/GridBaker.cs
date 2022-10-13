using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace KWZTerrainECS
{
    public class GridBaker : MonoBehaviour
    {
        private class GridAuthoring : Baker<GridBaker>
        {
            public override void Bake(GridBaker authoring)
            {
                AddComponent<TagUnInitializeGrid>();
            }
        }
    }
}
