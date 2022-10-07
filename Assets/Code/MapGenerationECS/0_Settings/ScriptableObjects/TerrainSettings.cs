using UnityEngine;
using Unity.Mathematics;
    
using static Unity.Mathematics.math;

namespace KWZTerrainECS
{
    [CreateAssetMenu(fileName = "TerrainSettingsECS", menuName = "KWZTerrainECS/TerrainSettings")]
    public class TerrainSettings : ScriptableObject
    {
        [field: SerializeField] public int NumChunkX { get; private set; }
        [field: SerializeField] public int NumChunkY { get; private set; }
        [field: SerializeField] public ChunkSettings ChunkSettings { get; private set; }
        [field: SerializeField] public NoiseSettings NoiseSettings { get; private set; }

        public int2 NumChunkXY => new int2(NumChunkX, NumChunkY);
        public int ChunksCount => NumChunkX * NumChunkY;
        
        //QUAD
        public int NumQuadX => NumChunkX * ChunkSettings.NumQuadPerLine;
        public int NumQuadY => NumChunkY * ChunkSettings.NumQuadPerLine;

        public int2 NumQuadsXY => new int2(NumQuadX, NumQuadY);
        public int MapQuadCount => NumQuadX * NumQuadY;
        
        //VERTEX
        public int NumVerticesX => NumQuadX + 1;
        public int NumVerticesY => NumQuadY + 1;
        public int2 NumVerticesXY => new int2(NumVerticesX, NumVerticesY);
        public int MapVerticesCount => NumVerticesX * NumVerticesY;
        
        //CHUNK DIRECT ACCESS
        //==============================================================================================================
        //QUAD
        public int ChunkQuadsPerLine => ChunkSettings.NumQuadPerLine;
        public int ChunkQuadsCount => ChunkSettings.QuadsCount;
        
        //VERTEX
        public int ChunkVerticesPerLine => ChunkSettings.NumVertexPerLine;
        public int ChunkVerticesCount => ChunkSettings.VerticesCount;
        
        private void OnEnable()
        {
            NumChunkX = max(1, ceilpow2(NumChunkX));
            NumChunkY = max(1, ceilpow2(NumChunkY));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            NumChunkX = max(1, ceilpow2(NumChunkX));
            NumChunkY = max(1, ceilpow2(NumChunkY));
        }
#endif
        
        public static implicit operator DataTerrain(TerrainSettings terrain)
        {
            return new DataTerrain
            {
                NumChunksXY = new int2(terrain.NumChunkX, terrain.NumChunkY),
                NumQuadsXY = new int2(terrain.NumQuadX, terrain.NumQuadY),
                NumVerticesXY = new int2(terrain.NumVerticesX, terrain.NumVerticesY)
            };
        }
        
    }
}

