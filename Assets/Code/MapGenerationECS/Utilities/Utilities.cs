using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using int2 = Unity.Mathematics.int2;

namespace KWZTerrainECS
{
    public static class Utilities
    {
        //GRID UTILITIES
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndex(in int2 coord, int width)
        {
            return coord.y * width + coord.x;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int, int) GetXY(int index, int width)
        {
            int y = index / width;
            int x = index - (y * width);
            return (x, y);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 GetXY2(int index, int width)
        {
            int y = index / width;
            int x = index - (y * width);
            return new int2(x, y);
        }
        
        //==============================================================================================================
        // Chunk Index From GRID Index
        //==============================================================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChunkIndexFromGridIndex(int gridIndex, int chunkNumQuadsX ,int numChunkX)
        {
            int2 cellCoord = GetXY2(gridIndex, chunkNumQuadsX * numChunkX);
            //int2 chunkCoord = (int2)floor(cellCoord / chunkNumQuadsX);
            int2 chunkCoord = cellCoord / chunkNumQuadsX;
            return chunkCoord.y * numChunkX + chunkCoord.x;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChunkIndexFromPosition(in float3 pointPos, in int2 numChunkXY, int chunkNumQuadsPerLine)
        {
            int2 cellCoord = GetCoordFromPositionOffset(pointPos, numChunkXY * chunkNumQuadsPerLine);
            //int2 chunkCoord = (int2)floor(cellCoord / chunkNumQuadsPerLine);
            int2 chunkCoord = cellCoord / chunkNumQuadsPerLine;
            return chunkCoord.y * numChunkXY.x + chunkCoord.x;
        }
        
        /*
        //Get CellIndex From Position
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndexFromPosition(float2 pointPos, int2 mapXY)
        {
            float2 percents = pointPos / mapXY;
            percents = clamp(percents, 0, 1f);
            int2 xy = clamp((int2)floor(mapXY * percents), 0, mapXY - 1);
            return xy.y * mapXY.x + xy.x;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndexFromPositionOffset(float2 pointPos, int2 mapXY)
        {
            float2 offset = (float2)mapXY / 2f;
            float2 percents = (pointPos + offset) / mapXY;
            percents = clamp(percents, float2.zero, 1f);
            int2 xy =  clamp((int2)floor(mapXY * percents), int2.zero, mapXY - 1);
            return xy.y * mapXY.x + xy.x;
        }
        */
        
        //==============================================================================================================
        //Get CellIndex From Position IF cell are exactly size 1!
        //==============================================================================================================
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndexFromPositionOffset(in float3 pointPos, in int2 mapXY)
        {
            //int2 offset = (int2)(pointPos.xz + ((float2)mapXY * 0.5f));
            int2 offset = (int2)mad(mapXY, 0.5f, pointPos.xz);
            return offset.y * mapXY.x + offset.x;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndexFromPositionOffset(in float2 pointPos, in int2 mapXY)
        {
            //int2 offset = (int2)(pointPos + ((float2)mapXY * 0.5f));
            int2 offset = (int2)mad(mapXY, 0.5f, pointPos);
            return offset.y * mapXY.x + offset.x;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 GetCoordFromPositionOffset(in float3 pointPos, in int2 mapXY)
        {
            return (int2)(pointPos.xz + (float2)mapXY / 2f);
        }
        
        //==============================================================================================================
        //Cell index inside Chunk from : position
        //==============================================================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CellChunkIndexFromGridIndex(in float3 pointPos, in int2 mapSizeXY, int chunkNumQuadsPerLine)
        {
            int gridIndex = GetIndexFromPositionOffset(pointPos, mapSizeXY);
            int2 cellCoord = GetXY2(gridIndex, mapSizeXY.x);
            //before : (int2)floor(cellCoord / chunkNumQuadsPerLine);
            int2 chunkCoord = cellCoord / chunkNumQuadsPerLine;
            int2 cellCoordInChunk = cellCoord - (chunkCoord * chunkNumQuadsPerLine);
            return cellCoordInChunk.y * chunkNumQuadsPerLine + cellCoordInChunk.x;
        }
        
        //==============================================================================================================
        //Grid Cell index from : Cell index inside Chunk
        //==============================================================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetGridCellIndexFromChunkCellIndex(int cellIndexInsideChunk, int chunkSizeX, int mapSizeX, int2 chunkCoord)
        {
            int2 cellCoordInChunk = GetXY2(cellIndexInsideChunk, chunkSizeX);
            int2 cellFullGridCoord = chunkCoord * chunkSizeX + cellCoordInChunk;
            return (cellFullGridCoord.y * mapSizeX) + cellFullGridCoord.x;
        }
        
        //==============================================================================================================
        //MATH UTILITIES
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool approximately(float a, float b)
        {
            const float minValue = 1E-06f;
            const float epsilon8 = EPSILON * 8f;

            //float2 ab = round(new float2(a, b) * 100) * 0.01f;
            float2 ab = new float2(round(a * 100), round(b * 100)) / 100;
            float maxValue1 = max(minValue * cmax(ab), epsilon8);
            return abs(ab.y - ab.x) < maxValue1;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool approximately(float2 a, float2 b)
        {
            bool componentX = approximately(a.x, b.x);
            bool componentY = approximately(a.y, b.y);
            return all(new bool2(componentX, componentY));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool approximately(float3 a, float3 b)
        {
            bool componentX = approximately(a.x, b.x);
            bool componentY = approximately(a.y, b.y);
            bool componentZ = approximately(a.z, b.z);
            return all(new bool3(componentX, componentY, componentZ));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float cmul(float2 a)
        {
            return a.x * a.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int cmul(int2 a)
        {
            return a.x * a.y;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Square(int value)
        {
            return value * value;
        }


        //==============================================================================================================
        //MATH Extension
        //public static float half(this float a) => a / 2f;
        //public static int half(this int a) => a / 2;

        //==============================================================================================================
        //Unity Extension
        public static Mesh[] GetMeshesComponent<T>(this T[] gameObjects) where T : MonoBehaviour
        {
            Mesh[] results = new Mesh[gameObjects.Length];
            for (int i = 0; i < gameObjects.Length; i++)
            {
                results[i] = gameObjects[i].GetComponent<MeshFilter>().mesh;
            }
            return results;
        }
        
        public static Mesh[] GetMeshesComponent(this GameObject[] gameObjects)
        {
            Mesh[] results = new Mesh[gameObjects.Length];
            for (int i = 0; i < gameObjects.Length; i++)
            {
                results[i] = gameObjects[i].GetComponent<MeshFilter>().mesh;
            }
            return results;
        }
    }
}
