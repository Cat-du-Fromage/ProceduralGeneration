using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

using static KWZTerrainECS.Utilities;
using static KWZTerrainECS.SidesExtension;

namespace KWZTerrainECS
{
    public partial class UnitSystem : SystemBase
    {
        // On Update when they have a destination
        private void OnMoveUnits()
        {
            
        }
        
        [BurstCompile]
        public partial struct JMoveUnits : IJobEntity
        {
            [ReadOnly] public int ChunkNumQuadsPerLine;
            [ReadOnly] public int2 NumChunkXY;
            //[ReadOnly] public int2 MapSizeXY;
            [ReadOnly] public ComponentLookup<PathsComponent> Paths;
            [ReadOnly] public NativeArray<Entity> ChunksEntity; //Get LinkedEntityGroup in Terrain
            
            public void Execute(ref WorldTransform position, in DynamicBuffer<BufferPathList> bufferUnitPath)
            {
                int2 mapSizeXY = NumChunkXY * ChunkNumQuadsPerLine;
                int chunkIndex = ChunkIndexFromPosition(position.Position, NumChunkXY, ChunkNumQuadsPerLine);
                
                if (bufferUnitPath[0].Value == chunkIndex)
                {
                    ESides side = GetDirection(bufferUnitPath[0].Value, bufferUnitPath[1].Value, ChunkNumQuadsPerLine);
                    Entity chunkUnitIsIn = ChunksEntity[chunkIndex];
                    int indexInChunk = CellChunkIndexFromGridIndex(position.Position, mapSizeXY, ChunkNumQuadsPerLine);
                    
                    float2 direction =Paths[chunkUnitIsIn][side][indexInChunk].Value;
                }
                //DynamicBuffer<Paths> pathBuffer = 
            }
        }
    }
}
