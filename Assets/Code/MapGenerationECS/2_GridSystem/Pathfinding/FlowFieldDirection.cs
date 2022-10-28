using System;
using Unity.Mathematics;

namespace KWZTerrainECS
{
    public struct FlowFieldDirection
    {
        private byte Index;

        public FlowFieldDirection(int2 direction)
        {
            Index = direction switch
            {
                {x:0,  y:1} => 0, //ESides.Top
                {x:1,  y:0} => 1, //ESides.Right
                {x:0, y:-1} => 2, //ESides.Bottom
                {x:-1, y:0} => 3, //ESides.Left
                _ => 0
            };
        }
        
        public FlowFieldDirection(float2 direction)
        {
            Index = direction switch
            {
                {x:0,  y:1} => 0, //ESides.Top
                {x:1,  y:0} => 1, //ESides.Right
                {x:0, y:-1} => 2, //ESides.Bottom
                {x:-1, y:0} => 3, //ESides.Left
                _ => 0
            };
        }
        
        public FlowFieldDirection(int direction)
        {
            Index = (byte)direction;
        }
        
        public FlowFieldDirection(ESides direction)
        {
            Index = direction switch
            {
                ESides.Top    => 0, //ESides.Top
                ESides.Bottom => 1, //ESides.Right
                ESides.Right  => 2, //ESides.Bottom
                ESides.Left   => 3, //ESides.Left
            };
        }

        public readonly float2 Value
        {
            get 
            {
                return Index switch
                {
                    0 => new float2(0,1), //ESides.Top
                    1 => new float2(1,0), //ESides.Right
                    2 => new float2(0,-1), //ESides.Bottom
                    3 => new float2(-1,0), //ESides.Left
                };
            }
        }

        public static bool operator ==(FlowFieldDirection lhs, FlowFieldDirection rhs)
        {
            return lhs.Index == rhs.Index;
        }

        public static bool operator !=(FlowFieldDirection lhs, FlowFieldDirection rhs)
        {
            return lhs.Index != rhs.Index;
        }
        
        public bool Equals(FlowFieldDirection other)
        {
            return Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is FlowFieldDirection other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Index.GetHashCode();
        }
    }
}