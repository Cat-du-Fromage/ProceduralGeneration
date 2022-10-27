using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using static KWZTerrainECS.Utilities;

namespace KWZTerrainECS
{
    [Flags]
    public enum ESides : int
    {
        Top    = 0,
        Right  = 1,
        Bottom = 2,
        Left   = 3
    }

    public static class SidesExtension
    {
        public static ESides Opposite(this ESides side) => side switch
        {
            ESides.Top    => ESides.Bottom,
            ESides.Bottom => ESides.Top,
            ESides.Right  => ESides.Left,
            ESides.Left   => ESides.Right,
            _             => side
        };
        
        public static ESides GetDirection(int indexOrigin, int indexDest, int numChunkWidth)
        {
            int2 coordOrigin = GetXY2(indexOrigin, numChunkWidth);
            int2 coordDest = GetXY2(indexDest, numChunkWidth);

            int2 coordDiff = coordDest - indexOrigin;

            if (coordDiff.x > 0)
                return ESides.Right;
            else if (coordDiff.x < 0)
                return ESides.Left;
            else if (coordDiff.y > 0)
                return ESides.Top;
            else //(coordDiff.y < 0)
                return ESides.Bottom;
            
        }
    }
}
