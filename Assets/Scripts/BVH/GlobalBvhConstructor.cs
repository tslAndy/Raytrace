using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Rtx.BVH
{
    public static class GlobalBvhConstructor
    {
        private const float MARGIN = 0.01f;
        private const int MAX_DEPTH = 10;

        // WARNING: this method changes order of objects
        // order of objects can not be changed anymore
        public static NodeBVH[] Build(RtxObj[] objects)
        {
            // Calculate total box, containing all items
            Bnd totalBounds = Bnd.Zero;
            for (int i = 0; i < objects.Length; i++)
                totalBounds += objects[i].bounds;
            totalBounds.AddMargin(MARGIN);

            Vector3 offset = totalBounds.min;
            Vector3 invSize = new Vector3(
                1.0f / totalBounds.size.x,
                1.0f / totalBounds.size.y,
                1.0f / totalBounds.size.z
            );

            // Fill object's and morton pairs
            (RtxObj obj, uint morton)[] pairs = new (RtxObj, uint)[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                RtxObj obj = objects[i];
                Vector3 normalizedPos = Vector3.Scale(obj.transform.position - offset, invSize);
                uint morton = GetMorton(normalizedPos);
                pairs[i] = (obj, morton);
            }
            Array.Sort(pairs, (a, b) => a.morton.CompareTo(b.morton));

            // Build tree
            List<NodeBVH> nodes = new List<NodeBVH> { new NodeBVH(0, pairs.Length, Bnd.Zero) };

            Span<(int nodeIndex, int depth)> stack = stackalloc (int, int)[MAX_DEPTH + 1];
            stack[0] = (0, 0);
            int stackIndex = 1;

            // Nodes creation
            while (stackIndex > 0)
            {
                (int nodeIndex, int depth) = stack[--stackIndex];
                NodeBVH node = nodes[nodeIndex];

                if (depth + 1 == MAX_DEPTH || node.end - node.start == 1)
                    continue;

                int start = node.start;
                int end = node.end;

                uint maxSplit = 0;
                int maxSplitIndex = 0;
                for (int i = start; i < end - 1; i++)
                {
                    uint split = pairs[i].morton ^ pairs[i + 1].morton;
                    if (split > maxSplit)
                    {
                        maxSplit = split;
                        maxSplitIndex = i + 1;
                    }
                }

                // split wasn't found
                if (maxSplit == 0)
                {
                    nodes.Add(new NodeBVH(start, end, Bnd.Zero));
                    continue;
                }

                node.childIndex = nodes.Count;
                nodes[nodeIndex] = node;

                nodes.Add(new NodeBVH(start, maxSplitIndex, Bnd.Zero));
                nodes.Add(new NodeBVH(maxSplitIndex, end, Bnd.Zero));

                stack[stackIndex++] = (node.childIndex + 1, depth + 1);
                stack[stackIndex++] = (node.childIndex, depth + 1);
            }

            // Box recalculation
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                NodeBVH node = nodes[i];

                if (node.childIndex > 0) // branch
                {
                    NodeBVH childA = nodes[node.childIndex];
                    NodeBVH childB = nodes[node.childIndex + 1];
                    node.bnd += childA.bnd;
                    node.bnd += childB.bnd;
                }
                else // leaf
                {
                    for (int j = node.start; j < node.end; j++)
                        node.bnd += pairs[j].obj.bounds;
                    node.bnd.AddMargin(MARGIN);
                }

                nodes[i] = node;
            }

            // Update array with sorted objects to correspond nodes
            for (int i = 0; i < objects.Length; i++)
                objects[i] = pairs[i].obj;

            return nodes.ToArray();
        }

        // normalized pos between [0, 1]
        private static uint GetMorton(Vector3 pos)
        {
            pos *= 1024.0f;
            uint x = (uint)pos.x;
            uint y = (uint)pos.y;
            uint z = (uint)pos.z;
            return (GetMorton(x) << 2) | (GetMorton(y) << 1) | GetMorton(z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetMorton(uint n)
        {
            return (n & 1)
                | ((n & 2) << 2)
                | ((n & 4) << 4)
                | ((n & 8) << 6)
                | ((n & 16) << 8)
                | ((n & 32) << 10)
                | ((n & 64) << 12)
                | ((n & 128) << 14)
                | ((n & 256) << 16)
                | ((n & 512) << 20);
        }
    }
}
