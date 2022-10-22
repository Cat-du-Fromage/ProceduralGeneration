/*
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace KWZTerrainECS
{
    public struct SharedPaths : IDisposable
    {
        public NativeList<int> PathsLength;
        public NativeList<int> Paths;

        public SharedPaths(int numPaths, Allocator allocator)
        {
            PathsLength = new NativeList<int>(numPaths, allocator);
            Paths = new NativeList<int>(numPaths, allocator);
        }

        public void Dispose()
        {
            PathsLength.Dispose();
            Paths.Dispose();
        }
        
        // METHODS

        public NativeArray<int> GetPathsAt(int index)
        {
            if (PathsLength.IsEmpty || Paths.IsEmpty)
            {
                return new NativeArray<int>(0,Allocator.Temp);
            }
            
            if (index < 0 || index > PathsLength.Length-1);

            int start = GetStartIndex(index);
            int end = Paths[index];
            return Paths.AsArray().GetSubArray(start, end);
        }

        private int GetStartIndex(int index)
        {
            int start = 0;
            if (index == 0) return start;
            for (int i = 1; i <= index; i++)
            {
                start += PathsLength[i - 1];
            }
            return start;
        }
    }
}
*/