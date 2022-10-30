using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

namespace KWZTerrainECS
{
    public static class ContainerExtension
    {
        // ===========================================================================================================
        // NativeArray<T>
        // ===========================================================================================================
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reverse<T>(this NativeArray<T> nativeArray)
        where T : struct
        {
            int numIteration = nativeArray.Length / 2;
            for (int i = 0; i < numIteration; i++)
            {
                int rightIndex = nativeArray.Length - (i + 1);
                (nativeArray[i], nativeArray[rightIndex]) = (nativeArray[rightIndex], nativeArray[i]);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Fill<T>(this NativeArray<T> nativeArray, T value)
            where T : struct
        {
            for (int i = 0; i < nativeArray.Length; i++)
            {
                nativeArray[i] = value;
            }
        }

        // ===========================================================================================================
        // NativeList<T>
        // ===========================================================================================================
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reverse<T>(this NativeList<T> nativeArray)
        where T : unmanaged
        {
            int numIteration = nativeArray.Length / 2;
            for (int i = 0; i < numIteration; i++)
            {
                int rightIndex = nativeArray.Length - (i + 1);
                (nativeArray[i], nativeArray[rightIndex]) = (nativeArray[rightIndex], nativeArray[i]);
            }
        }
    }
}
