using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static KWZTerrainECS.ESides;

using static KWZTerrainECS.Utilities;
using static Unity.Mathematics.math;
using static Unity.Jobs.LowLevel.Unsafe.JobsUtility;
using int2 = Unity.Mathematics.int2;

namespace KWZTerrainECS
{
    public static class ChunkGridConstructor
    {
        public static void BuildGrid(this DynamicBuffer<ChunkNodeGrid> buffer, int chunkSize, int2 numChunksXY)
        {
            int numChunks = cmul(numChunksXY);
            int bufferCapacity = (chunkSize * 4) * numChunks;
            buffer.EnsureCapacity(bufferCapacity);

            NativeArray<GateWay> gates = new (bufferCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            JConstructGrid job = new JConstructGrid
            {
                ChunkQuadPerLine = chunkSize,
                NumChunksXY = numChunksXY,
                GateWays = gates
            };
            job.ScheduleParallel(numChunks, JobWorkerCount - 1, default).Complete();
            buffer.CopyFrom(gates.Reinterpret<ChunkNodeGrid>());
            gates.Dispose();
        }
    }
    
    [BurstCompile(CompileSynchronously = false)]
    public struct JConstructGrid : IJobFor
    {
        [ReadOnly] public int ChunkQuadPerLine;
        [ReadOnly] public int2 NumChunksXY;
        
        [WriteOnly, NativeDisableParallelForRestriction] 
        public NativeArray<GateWay> GateWays;

        public void Execute(int chunkIndex)
        {
            int2 terrainSize = NumChunksXY * ChunkQuadPerLine;
            
            int2 chunkCoord = GetXY2(chunkIndex, NumChunksXY.x);
            int2 offsetChunk = ChunkQuadPerLine * chunkCoord;

            for (int i = 0; i < 4; i++)
            {
                ESides side = (ESides)i;

                bool2 isXOffset = new(side == Left, side == Right);
                bool2 isYOffset = new(side == Top, side == Bottom);

                for (int j = 0; j < ChunkQuadPerLine; j++)
                {
                    int2 gateCoord = GetXY2(j, ChunkQuadPerLine);
                    
                    gateCoord = select(gateCoord,gateCoord.yx,any(isXOffset)) + offsetChunk;
                    
					gateCoord.x += select(0, ChunkQuadPerLine - 1, side == Right);
					gateCoord.y += select(0, ChunkQuadPerLine - 1, side == Top);

                    int2 offsetXY = new int2
                    (
                        select( select(1,-1,isXOffset.x), 0,!any(isXOffset) ),
                        select( select(1,-1,isYOffset.y), 0,!any(isYOffset) )
                    );
                    
                    int gateIndex = mad(gateCoord.y, terrainSize.x, gateCoord.x);
                    int2 coordOffset = gateCoord + offsetXY;
                    int adjGateIndex = mad(coordOffset.y,terrainSize.x,coordOffset.x);

                    bool2 isOutLimit = new bool2
                    (
                        coordOffset.x < 0 || coordOffset.x > terrainSize.x - 1,
                        coordOffset.y < 0 || coordOffset.y > terrainSize.y - 1
                    );

                    int offsetChunkIndex = chunkIndex * ChunkQuadPerLine * 4;
                    int indexOffset = offsetChunkIndex + (i * ChunkQuadPerLine + j);
                    
                    GateWays[indexOffset] = any(isOutLimit) 
                        ? new GateWay(chunkIndex, side) 
                        : new GateWay(chunkIndex, side,gateIndex, adjGateIndex);
                }
            }
        }

    }
}
