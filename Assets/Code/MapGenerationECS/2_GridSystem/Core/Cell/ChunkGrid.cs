using Unity.Entities;

namespace KWZTerrainECS
{
    public struct ChunkNodeGrid : IBufferElementData
    {
        public GateWay Value;
        public static implicit operator ChunkNodeGrid(GateWay e) => new ChunkNodeGrid {Value = e};
    }
}