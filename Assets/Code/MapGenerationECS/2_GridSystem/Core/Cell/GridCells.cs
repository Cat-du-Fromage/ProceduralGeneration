using Unity.Entities;

namespace KWZTerrainECS
{
    public struct GridCells
    {
        public int ChunkSize;
        public int NumChunkX;
        public BlobArray<Cell> Cells;
    }
}