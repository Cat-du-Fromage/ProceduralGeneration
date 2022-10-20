using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KWZTerrainECS
{
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
    }
}
