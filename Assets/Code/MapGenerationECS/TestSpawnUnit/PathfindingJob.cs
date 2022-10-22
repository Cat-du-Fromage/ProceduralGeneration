using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static KWZTerrainECS.GridUtilities;
using static Unity.Mathematics.math;
using static KWZTerrainECS.Utilities;
using static Unity.Collections.Allocator;

namespace KWZTerrainECS
{
    [BurstCompile(CompileSynchronously = false)]
    public partial struct JAStar : IJob
    {
        [ReadOnly] public int StartChunkIndex;
        [ReadOnly] public int EndChunkIndex;
        [ReadOnly] public int2 NumChunkAxis;
        public NativeList<int> PathList; // if PathNode.Length == 0 means No Path!
        
        [DeallocateOnJobCompletion] private NativeArray<Node> Nodes;
        
        public JAStar(int start, int end, in int2 chunkAxis, NativeList<int> pathList)
        {
            StartChunkIndex = start;
            EndChunkIndex = end;
            NumChunkAxis = chunkAxis;
            PathList = pathList;

            int numChunks = cmul(chunkAxis);
            Nodes = new NativeArray<Node>(numChunks, TempJob);
            for (int i = 0; i < numChunks; i++)
            {
                Nodes[i] = new Node(GetXY2(i, chunkAxis.x));
            }
        }
        
        public JAStar(int start, int end, in int2 chunkAxis, NativeList<int> pathList, NativeArray<Node> nodes)
        {
            StartChunkIndex = start;
            EndChunkIndex = end;
            NumChunkAxis = chunkAxis;
            PathList = pathList;
            Nodes = nodes;
        }
        
        public void Execute()
        {
            int numChunk = cmul(NumChunkAxis);
            NativeHashSet<int> openSet = new (numChunk, Temp);
            NativeHashSet<int> closeSet = new (numChunk, Temp);
            
            Nodes[StartChunkIndex] = StartNode(Nodes[StartChunkIndex], Nodes[EndChunkIndex]);
            openSet.Add(StartChunkIndex);

            NativeList<int> neighbors = new (4,Temp);

            while (!openSet.IsEmpty)
            {
                int currentNode = GetLowestFCostNodeIndex(openSet);
                //Check if we already arrived
                if (currentNode == EndChunkIndex)
                {
                    CalculatePath();
                    return;
                }
                //Add "already check" Node AND remove from "To check"
                openSet.Remove(currentNode);
                closeSet.Add(currentNode);
                
                //Add Neighbors to OpenSet
                GetNeighborCells(currentNode, neighbors, closeSet);
                if (neighbors.Length > 0)
                {
                    for (int i = 0; i < neighbors.Length; i++)
                    {
                        openSet.Add(neighbors[i]);
                    }
                }
                neighbors.Clear();
            }
        }

        private void CalculatePath()
        {
            PathList.Add(EndChunkIndex);
            int currentNode = EndChunkIndex;
            while(currentNode != StartChunkIndex)
            {
                currentNode = Nodes[currentNode].CameFromNodeIndex;
                PathList.Add(currentNode);
            }
            PathList.Reverse();
        }
        
        private void GetNeighborCells(int index, NativeList<int> curNeighbors, NativeHashSet<int> closeSet)
        {
            int2 coord = GetXY2(index,NumChunkAxis.x);
            for (int i = 0; i < 4; i++)
            {
                int neighborId = AdjacentCellFromIndex(index, (1 << i), coord, NumChunkAxis);
                if (neighborId == -1 || closeSet.Contains(neighborId)) continue;

                Node currentNode = Nodes[index];
                Node neighborNode = Nodes[neighborId];
                
                int tentativeCost = currentNode.GCost + CalculateDistanceCost(currentNode,neighborNode);
                if (tentativeCost < neighborNode.GCost)
                {
                    curNeighbors.Add(neighborId);
                    int gCost = CalculateDistanceCost(neighborNode, Nodes[StartChunkIndex]);
                    int hCost = CalculateDistanceCost(neighborNode, Nodes[EndChunkIndex]);
                    Nodes[neighborId] = new Node(index, gCost, hCost, neighborNode.Coord);
                }
            }
        }

        private int GetLowestFCostNodeIndex(NativeHashSet<int> openSet)
        {
            int indexLowest = -1;
            foreach (int index in openSet)
            {
                indexLowest = indexLowest == -1 ? index : indexLowest;
                indexLowest = select(indexLowest, index, Nodes[index].FCost < Nodes[indexLowest].FCost);
            }
            return indexLowest;
        }

        private Node StartNode(in Node start, in Node end)
        {
            int hCost = CalculateDistanceCost(start, end);
            return new Node(-1, 0, hCost, start.Coord);
        }

        private int CalculateDistanceCost(in Node a, in Node b)
        {
            int2 xyDistance = abs(a.Coord - b.Coord);
            int remaining = abs(xyDistance.x - xyDistance.y);
            return 14 * cmin(xyDistance) + 10 * remaining;
        }
    }
    
    public readonly struct Node
    {
        public readonly int CameFromNodeIndex;
    
        public readonly int GCost; //Distance from Start Node
        public readonly int HCost; // distance from End Node
        public readonly int FCost;
        public readonly int2 Coord;

        public Node(int cameFromNodeIndex, int gCost, int hCost, in int2 coord)
        {
            CameFromNodeIndex = cameFromNodeIndex;
            GCost = gCost;
            HCost = hCost;
            FCost = GCost + HCost;
            Coord = coord;
        }

        public Node(in int2 coord)
        {
            CameFromNodeIndex = -1;
            GCost = int.MaxValue;
            HCost = default;
            FCost = GCost + HCost;
            Coord = coord;
        }
    }
}
    

