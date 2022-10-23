using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KWZTerrainECS
{
    //FEU le bug depuis 1.0!!
    public struct TestFixedList : IBufferElementData
    {
        public FixedList512Bytes<int> Value;
    }
}
