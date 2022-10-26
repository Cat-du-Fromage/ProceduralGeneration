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
        public static int Square(int value)
        {
            return value * value;
        }
        
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
        
<<<<<<< Updated upstream
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CellChunkIndexFromGridIndex(in float3 pointPos, in int2 mapSizeXY, int chunkNumQuadsPerLine)
        {
            int gridIndex = GetIndexFromPositionOffset(pointPos, mapSizeXY);
            int2 cellCoord = GetXY2(gridIndex, mapSizeXY.x);
            int2 chunkCoord = (int2)floor(cellCoord / chunkNumQuadsPerLine);
            int2 cellCoordInChunk = cellCoord - (chunkCoord * chunkNumQuadsPerLine);
            return cellCoordInChunk.y * chunkNumQuadsPerLine + cellCoordInChunk.x;
        }
        
        /*
        public static float3 Get3DTranslatedPosition(this ref GridCells cells, float2 position2D, int2 mapSizeXY)
        {
            int cellIndex = GetIndexFromPositionOffset(position2D, mapSizeXY);
            Cell cell = cells.Cells[cellIndex];

            bool isLeftTri = IsPointInTriangle(cell.LeftTriangle, position2D);
            //bool isRightTri = IsPointInTriangle(cell.RightTriangle, position2D);

            //Ray origin
            float3 rayOrigin = new float3(position2D.x, cell.HighestPoint, position2D.y);
            //NORMAL
            float3 triangleNormal = isLeftTri ? cell.NormalTriangleLeft : cell.NormalTriangleRight;
            //Point A : start
            float3 a = isLeftTri ? cell.LeftTriangle[0] : cell.RightTriangle[0];
            float t = dot(a - rayOrigin, triangleNormal) / dot(down(), triangleNormal);
            return mad(t,down(), rayOrigin);
        }
        
        public static bool IsPointInTriangle(NativeSlice<float3> triangle, float2 position2D)
        {
            float2 triA = triangle[0].xz;
            float2 triB = triangle[1].xz;
            float2 triC = triangle[2].xz;
            
            bool isAEqualC = approximately(triC.y, triA.y);
            float2 a = select(triA, triB, isAEqualC);
            float2 b = select(triB, triA, isAEqualC);
            
            float s1 = triC.y - a.y;
            float s2 = triC.x - a.x;

            float s3 = b.y - a.y;
            float s4 = position2D.y - a.y;

            float w1 = (a.x * s1 + s4 * s2 - position2D.x * s1) / (s3 * s2 - (b.x - a.x) * s1);
            float w2 = (s4 - w1 * s3) / s1;
            return w1 >= 0 && w2 >= 0 && (w1 + w2) <= 1;
        }
        */
=======
>>>>>>> Stashed changes
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
