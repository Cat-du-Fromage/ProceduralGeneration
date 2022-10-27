using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace KWZTerrainECS
{
    public readonly partial struct PathsAspect : IAspect
    {
        [ReadOnly] private readonly DynamicBuffer<TopPathBuffer> top;
        [ReadOnly] private readonly DynamicBuffer<BottomPathBuffer> bottom;
        [ReadOnly] private readonly DynamicBuffer<RightPathBuffer> right;
        [ReadOnly] private readonly DynamicBuffer<LeftPathBuffer> left;
        
        [CreateProperty]
        public DynamicBuffer<TopPathBuffer> Top
        {
            get => top;
        }
        
        [CreateProperty]
        public DynamicBuffer<BottomPathBuffer> Bottom
        {
            get => bottom;
        }
        
        [CreateProperty]
        public DynamicBuffer<RightPathBuffer> Right
        {
            get => right;
        }
        
        [CreateProperty]
        public DynamicBuffer<LeftPathBuffer> Left
        {
            get => left;
        }
    }
    
    public struct TopPathBuffer : IBufferElementData
    {
        public FlowFieldDirection Value;
    }
    public struct BottomPathBuffer : IBufferElementData
    {
        public FlowFieldDirection Value;
    }
    public struct RightPathBuffer : IBufferElementData
    {
        public FlowFieldDirection Value;
    }
    public struct LeftPathBuffer : IBufferElementData
    {
        public FlowFieldDirection Value;
    }
    
    public struct TopPaths : IComponentData
    {
        public FixedList4096Bytes<FlowFieldDirection> Top;
    }

    public struct PathsComponent : IComponentData
    {
        public FixedList512Bytes<FlowFieldDirection> Top;
        public FixedList512Bytes<FlowFieldDirection> Right;
        public FixedList512Bytes<FlowFieldDirection> Bottom;
        public FixedList512Bytes<FlowFieldDirection> Left;

        public readonly FixedList512Bytes<FlowFieldDirection> this[ESides index]
        {
            get
            {
                return index switch
                {
                    ESides.Top => Top,
                    ESides.Right => Right,
                    ESides.Bottom => Bottom,
                    ESides.Left => Left,
                    _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
                };
            }
        }
    }
}