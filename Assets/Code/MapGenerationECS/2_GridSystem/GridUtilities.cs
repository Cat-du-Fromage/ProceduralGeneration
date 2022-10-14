using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

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
        public static NativeArray<Cell> GetCellsAtChunk(ref this GridCells gridCells, int chunkIndex, Allocator allocator = Allocator.TempJob)
        {
            //store value from blob
            int chunkQuadsPerLine = gridCells.ChunkSize;
            int mapNumChunkX = gridCells.NumChunkX;
            
            int mapNumQuadsX = mapNumChunkX * chunkQuadsPerLine;
            int numCells = chunkQuadsPerLine * chunkQuadsPerLine;
            
            NativeArray<Cell> chunkCells = new(numCells, allocator, NativeArrayOptions.UninitializedMemory);
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
        public static int GetGridCellIndexFromChunkCellIndex(int chunkSizeX,int mapSizeX, int cellIndexInsideChunk, int2 chunkCoord)
        {
            int2 cellCoordInChunk = GetXY2(cellIndexInsideChunk, chunkSizeX);
            int2 cellGridCoord = chunkCoord * chunkSizeX + cellCoordInChunk;
            return (cellGridCoord.y * mapSizeX) + cellGridCoord.x;
        }
    }
}
