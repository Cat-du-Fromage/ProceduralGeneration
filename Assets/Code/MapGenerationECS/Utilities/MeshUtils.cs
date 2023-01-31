using Unity.Collections;
using UnityEngine.Rendering;

using static UnityEngine.Mesh;

using static UnityEngine.Rendering.VertexAttribute;
using static UnityEngine.Rendering.VertexAttributeFormat;
using static Unity.Collections.Allocator;
using static Unity.Collections.NativeArrayOptions;

namespace KWZTerrainECS
{
    public static class MeshUtils
    {
        /// <summary>
        /// setup information about mesh subsets
        /// Must be done after params has 
        /// </summary>
        /// <param name="meshDataArray"></param>
        /// <param name="verticesCount"></param>
        /// <param name="triangleIndicesCount"></param>
        public static void SetSubMeshes(this MeshDataArray meshDataArray, int verticesCount, int triangleIndicesCount)
        {
            SubMeshDescriptor descriptor = new(0, triangleIndicesCount) {vertexCount = verticesCount};
            for (int i = 0; i < meshDataArray.Length; i++)
            {
                meshDataArray[i].SetSubMesh(0, descriptor, MeshUpdateFlags.DontRecalculateBounds);
            }
        }
        
        /// <summary>
        /// Initialize Buffer params necessary for the construction of the mesh
        /// Vertex and Indices(triangle)
        /// </summary>
        /// <param name="meshData">mesh data to be modified</param>
        /// <param name="verticesCount">number of the vertices affected</param>
        /// <param name="triangleIndicesCount">number of triangle indices (num triangles * 3)</param>
        public static void InitializeBufferParams(this MeshData meshData, int verticesCount, int triangleIndicesCount)
        {
            NativeArray<VertexAttributeDescriptor> vertexAttributes = InitializeVertexAttribute();
            meshData.subMeshCount = 1;
            meshData.SetVertexBufferParams(verticesCount, vertexAttributes);
            meshData.SetIndexBufferParams(triangleIndicesCount, IndexFormat.UInt16);
        }
        
        /// <summary>
        /// Initialize each stream of the vertex attributes with the smaller type possible
        /// </summary>
        /// <returns></returns>
        public static NativeArray<VertexAttributeDescriptor> InitializeVertexAttribute()
        {
            NativeArray<VertexAttributeDescriptor> vertexAttributes = new(4, Temp, UninitializedMemory);
            vertexAttributes[0] = new VertexAttributeDescriptor(Position, Float32, dimension: 3, stream: 0);
            vertexAttributes[1] = new VertexAttributeDescriptor(Normal, Float32, dimension: 3, stream: 1);
            vertexAttributes[2] = new VertexAttributeDescriptor(Tangent, Float16, dimension: 4, stream: 2);
            vertexAttributes[3] = new VertexAttributeDescriptor(TexCoord0, Float16, dimension: 2, stream: 3);
            return vertexAttributes;
        }
    }
}
