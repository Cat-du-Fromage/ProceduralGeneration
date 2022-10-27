using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;

using static KWZTerrainECS.Utilities;

namespace KWZTerrainECS
{
    [Flags]
    public enum AdjacentCell : int
    {
        Top         = 1 << 0,
        Right       = 1 << 1,
        Left        = 1 << 2,
        Bottom      = 1 << 3,
        TopLeft     = 1 << 4,
        TopRight    = 1 << 5,
        BottomRight = 1 << 6,
        BottomLeft  = 1 << 7,
    }
    
    public static class GridUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AdjCellFromIndex(this int index, AdjacentCell adjCell, in int2 pos, int width) 
        => adjCell switch
        {
            AdjacentCell.Left        when pos.x > 0                              => index - 1,
            AdjacentCell.Right       when pos.x < width - 1                      => index + 1,
            AdjacentCell.Top         when pos.y < width - 1                      => index + width,
            AdjacentCell.TopLeft     when pos.y < width - 1 && pos.x > 0         => (index + width) - 1,
            AdjacentCell.TopRight    when pos.y < width - 1 && pos.x < width - 1 => (index + width) + 1,
            AdjacentCell.Bottom      when pos.y > 0                              => index - width,
            AdjacentCell.BottomLeft  when pos.y > 0 && pos.x > 0                 => (index - width) - 1,
            AdjacentCell.BottomRight when pos.y > 0 && pos.x < width - 1         => (index - width) + 1,
            _ => -1,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AdjacentCellFromIndex(int index, int adjCell, in int2 coord, in int2 bounds)
        {
            bool isLeft =  coord.x > 0;
            bool isRight = coord.x < bounds.x - 1;
            bool isTop = coord.y < bounds.y - 1;
            bool isBottom = coord.y > 0;
            return (AdjacentCell)adjCell switch
            {
                AdjacentCell.Top         when isTop               => index + bounds.x,
                AdjacentCell.Right       when isRight             => index + 1,
                AdjacentCell.Left        when isLeft              => index - 1,
                AdjacentCell.Bottom      when isBottom            => index - bounds.x,
                AdjacentCell.TopLeft     when isTop && isLeft     => (index + bounds.x) - 1,
                AdjacentCell.TopRight    when isTop && isRight    => (index + bounds.x) + 1,
                AdjacentCell.BottomRight when isBottom && isRight => (index - bounds.x) + 1,
                AdjacentCell.BottomLeft  when isBottom && isLeft  => (index - bounds.x) - 1,
                _ => -1
            };
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AdjacentCellFromIndex(int index, AdjacentCell adjCell, in int2 coord, in int2 bounds)
        {
            bool isLeft =  coord.x > 0;
            bool isRight = coord.x < bounds.x - 1;
            bool isTop = coord.y < bounds.y - 1;
            bool isBottom = coord.y > 0;

            int width = bounds.x;
            return adjCell switch
            {
                AdjacentCell.Top         when isTop               => index + width,
                AdjacentCell.Right       when isRight             => index + 1,
                AdjacentCell.Left        when isLeft              => index - 1,
                AdjacentCell.Bottom      when isBottom            => index - width,
                AdjacentCell.TopLeft     when isTop && isLeft     => (index + width) - 1,
                AdjacentCell.TopRight    when isTop && isRight    => (index + width) + 1,
                AdjacentCell.BottomRight when isBottom && isRight => (index - width) + 1,
                AdjacentCell.BottomLeft  when isBottom && isLeft  => (index - width) - 1,
                _ => -1
            };
        }
        
        /*
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AdjacentCellFromIndex(int index, AdjacentCell adjCell, in int2 pos, int width)
        {
            switch (adjCell) 
            {
                case AdjacentCell.Top when pos.x > 0: 
                    return index - 1;
                case AdjacentCell.Right when pos.x < width - 1: 
                    return index + 1;
                case AdjacentCell.Left when pos.y < width - 1: 
                    return index + width;
                case AdjacentCell.Bottom when pos.y < width - 1 && pos.x > 0:
                    return (index + width) - 1;
                case AdjacentCell.TopLeft when pos.y < width - 1 && pos.x < width - 1:
                    return (index + width) + 1;
                case AdjacentCell.TopRight when pos.y > 0:
                    return index - width;
                case AdjacentCell.BottomRight when pos.y > 0 && pos.x > 0:
                    return (index - width) - 1;
                case AdjacentCell.BottomLeft  when pos.y > 0 && pos.x < width - 1:
                    return (index - width) + 1;
                default:
                    return -1;
            }
        }
         */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AdjCellFromIndex(this int index, int adjCell, in int2 pos, int width) 
        => adjCell switch
        {
            (int)AdjacentCell.Left        when pos.x > 0                              => index - 1,
            (int)AdjacentCell.Right       when pos.x < width - 1                      => index + 1,
            (int)AdjacentCell.Top         when pos.y < width - 1                      => index + width,
            (int)AdjacentCell.TopLeft     when pos.y < width - 1 && pos.x > 0         => (index + width) - 1,
            (int)AdjacentCell.TopRight    when pos.y < width - 1 && pos.x < width - 1 => (index + width) + 1,
            (int)AdjacentCell.Bottom      when pos.y > 0                              => index - width,
            (int)AdjacentCell.BottomLeft  when pos.y > 0 && pos.x > 0                 => (index - width) - 1,
            (int)AdjacentCell.BottomRight when pos.y > 0 && pos.x < width - 1         => (index - width) + 1,
            _ => -1,
        };
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<Cell> GetCellsAtChunk(ref this GridCells gridCells, int chunkIndex, Allocator allocator = TempJob)
        {
            //store value from blob
            int chunkQuadsPerLine = gridCells.ChunkSize;
            int mapNumChunkX = gridCells.NumChunkX;
            
            int mapNumQuadsX = mapNumChunkX * chunkQuadsPerLine;
            int numCells = chunkQuadsPerLine * chunkQuadsPerLine;
            
            NativeArray<Cell> chunkCells = new(numCells, allocator, UninitializedMemory);
            int2 chunkCoord = GetXY2(chunkIndex, mapNumChunkX);

            for (int i = 0; i < numCells; i++)
            {
                int2 cellCoordInChunk = GetXY2(i, chunkQuadsPerLine);
                int2 cellGridCoord = chunkCoord * chunkQuadsPerLine + cellCoordInChunk;
                int index = cellGridCoord.y * mapNumQuadsX + cellGridCoord.x;
                chunkCells[i] = gridCells.Cells[index];
            }
            return chunkCells;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<GateWay> GetGateWaysAt(
            this DynamicBuffer<ChunkNodeGrid> buffer, 
            int chunkIndex, 
            ESides side, 
            in TerrainAspectStruct terrainStruct,
            Allocator allocator = Temp)
        {
            int chunkNumQuadPerLine = terrainStruct.Chunk.NumQuadPerLine;
            int offsetChunk = chunkIndex * 4 * chunkNumQuadPerLine;
            int startIndex = offsetChunk + (int)side * chunkNumQuadPerLine;
            NativeArray<GateWay> gateWays = new (chunkNumQuadPerLine, allocator, UninitializedMemory);
            /*
            for (int i = 0; i < chunkNumQuadPerLine; i++)
            {
                gateWays[i] = buffer[startIndex + i].Value;
            }
            */
            buffer
                .AsNativeArray()
                .Reinterpret<GateWay>()
                .Slice(startIndex, chunkNumQuadPerLine)
                .CopyTo(gateWays);
            return gateWays;
        }
    }
}
