using System;
using System.Collections.Generic;
using Rtx.Data;
using UnityEngine;

namespace Rtx.BVH
{
    public static class MeshBvhConstructor
    {
        private const int SLICES_COUNT = 5;
        private const int MAX_DEPTH = 10;
        private const float EPS = 0.001f;

        private const int MIN_TRIGS = 3;

        public static void Build(
            Mesh[] meshes,
            out NodeBVH[] bvh,
            out Vector3[] verts,
            out int[] trigs,
            out MeshData[] meshData
        )
        {
            // allocate verts and trigs
            int vertsCount = 0,
                trigsCount = 0;
            for (int i = 0; i < meshes.Length; i++)
            {
                Mesh mesh = meshes[i];
                vertsCount += mesh.vertexCount;
                trigsCount += (int)mesh.GetIndexCount(0);
            }
            verts = new Vector3[vertsCount];
            trigs = new int[trigsCount];
            meshData = new MeshData[meshes.Length];
            List<NodeBVH> nodes = new List<NodeBVH>(); // later will be converted to bvh

            int bvhOffset = 0,
                vertOffset = 0,
                trigOffset = 0;

            // Calculate BVH for each mesh and insert it
            for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                Mesh mesh = meshes[meshIndex];
                BuildBvh(mesh, out Vector3[] tempVerts, out int[] tempTrigs, out NodeBVH[] tempBvh);

                // apply offset to triangles
                for (int j = 0; j < tempTrigs.Length; j++)
                {
                    tempTrigs[j] += vertOffset;
                }

                // apply offset to bvh
                for (int j = 0; j < tempBvh.Length; j++)
                {
                    NodeBVH node = tempBvh[j];
                    if (node.isLeaf) // if leaaf then points to triangles
                    {
                        node.start = trigOffset + node.start * 3;
                        node.end = trigOffset + node.end * 3;
                    }
                    else // if branch points to bvh nodes
                    {
                        node.childIndex += bvhOffset;
                    }
                    tempBvh[j] = node;
                }

                meshData[meshIndex] = new MeshData(bvhOffset, trigOffset, tempTrigs.Length / 3);
                tempVerts.CopyTo(verts, vertOffset);
                tempTrigs.CopyTo(trigs, trigOffset);
                nodes.AddRange(tempBvh);

                vertOffset += tempVerts.Length;
                trigOffset += tempTrigs.Length;
                bvhOffset += tempBvh.Length;
            }

            // when tree is ready, put it into array
            bvh = nodes.ToArray();
        }

        private static void BuildBvh(
            Mesh mesh,
            out Vector3[] verts,
            out int[] trigs,
            out NodeBVH[] bvh
        )
        {
            verts = mesh.vertices;

            int[] tempTrigs = mesh.triangles;
            Elem[] elems = new Elem[tempTrigs.Length / 3];
            for (int i = 0; i < tempTrigs.Length; i += 3)
            {
                int indA = tempTrigs[i];
                int indB = tempTrigs[i + 1];
                int indC = tempTrigs[i + 2];

                Vector3 a = verts[indA];
                Vector3 b = verts[indB];
                Vector3 c = verts[indC];

                elems[i / 3] = new Elem
                {
                    bnd = new Bnd(
                        Vector3.Min(Vector3.Min(a, b), c),
                        Vector3.Max(Vector3.Max(a, b), c)
                    ),
                    trigId = i,
                };
            }

            // Create initial node and calculate it's box (required for splitting)
            NodeBVH head = new NodeBVH(0, elems.Length, Bnd.Zero);
            for (int i = 0; i < elems.Length; i++)
                head.bnd += elems[i].bnd;
            List<NodeBVH> nodes = new List<NodeBVH> { head };

            Span<(int nodeId, int depth)> stack = stackalloc (int, int)[MAX_DEPTH + 1];
            Span<(Bnd bnd, int count)> slices = stackalloc (Bnd, int)[SLICES_COUNT + 1];

            int stackIndex = 1;
            while (stackIndex > 0)
            {
                (int nodeId, int depth) = stack[--stackIndex];
                if (depth + 1 == MAX_DEPTH)
                    continue;

                NodeBVH node = nodes[nodeId];
                if (node.end - node.start <= MIN_TRIGS)
                    continue;

                int bestSliceId = -1;
                int bestSliceAxis = -1;
                float bestSah = float.MaxValue;
                Bnd bestLeftBox = Bnd.Zero;
                Bnd bestRightBox = Bnd.Zero;

                for (int axis = 0; axis < 3; axis++)
                {
                    float size = node.bnd.size[axis];
                    if (size < EPS)
                        continue;

                    float invSize = (SLICES_COUNT + 1.0f) / size;

                    slices.Fill((Bnd.Zero, 0));
                    for (int i = node.start; i < node.end; i++)
                    {
                        Elem elem = elems[i];
                        int sliceId = (int)((elem.bnd.center[axis] - node.bnd.min[axis]) * invSize);
                        sliceId = sliceId > SLICES_COUNT ? SLICES_COUNT : sliceId;

                        slices[sliceId].bnd += elem.bnd;
                        slices[sliceId].count++;
                    }

                    for (int sliceId = 0; sliceId < SLICES_COUNT; sliceId++)
                    {
                        Bnd leftBox = Bnd.Zero;
                        int leftCount = 0;

                        Bnd rightBox = Bnd.Zero;
                        int rightCount = 0;

                        for (int leftId = 0; leftId < sliceId + 1; leftId++)
                        {
                            leftBox += slices[leftId].bnd;
                            leftCount += slices[leftId].count;
                        }

                        for (int rightId = sliceId + 1; rightId < SLICES_COUNT + 1; rightId++)
                        {
                            rightBox += slices[rightId].bnd;
                            rightCount += slices[rightId].count;
                        }

                        float sah = leftBox.surface * leftCount + rightBox.surface * rightCount;
                        if (sah >= bestSah)
                            continue;

                        bestSah = sah;
                        bestSliceId = sliceId;
                        bestSliceAxis = axis;
                        bestLeftBox = leftBox;
                        bestRightBox = rightBox;
                    }
                }

                int left = node.start;
                int right = node.end - 1;
                float factor = (SLICES_COUNT + 1.0f) / node.bnd.size[bestSliceAxis];

                while (left <= right)
                {
                    int sliceId = (int)(
                        (elems[left].bnd.center[bestSliceAxis] - node.bnd.min[bestSliceAxis])
                        * factor
                    );
                    if (sliceId <= bestSliceId)
                    {
                        left++;
                    }
                    else
                    {
                        (elems[left], elems[right]) = (elems[right], elems[left]);
                        right--;
                    }
                }

                if (left == node.start || left == node.end)
                    continue;

                if (bestLeftBox.surface < EPS || bestRightBox.surface < EPS)
                    continue;

                NodeBVH leftChild = new NodeBVH(node.start, left, bestLeftBox);
                NodeBVH rightChild = new NodeBVH(left, node.end, bestRightBox);

                node.childIndex = nodes.Count;
                stack[stackIndex++] = (node.childIndex, depth + 1);
                stack[stackIndex++] = (node.childIndex + 1, depth + 1);

                nodes[nodeId] = node;
                nodes.Add(leftChild);
                nodes.Add(rightChild);
            }

            // because nodes contains indices pointing to elements
            // we can reorder triangles so they match given elements
            // by that we eleminate 1 jump from
            // nodes -> elements -> trigs to
            // nodes -> trigs

            trigs = new int[tempTrigs.Length];
            for (int i = 0; i < elems.Length; i++)
            {
                Elem elem = elems[i];
                trigs[i * 3] = tempTrigs[elem.trigId];
                trigs[i * 3 + 1] = tempTrigs[elem.trigId + 1];
                trigs[i * 3 + 2] = tempTrigs[elem.trigId + 2];
            }

            bvh = nodes.ToArray();
        }

        private struct Elem
        {
            public Bnd bnd;
            public int trigId;
        }
    }
}
