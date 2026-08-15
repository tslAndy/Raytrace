using System;
using System.Linq;
using Rtx.BVH;
using Rtx.Data;
using UnityEngine;

namespace Rtx.Test
{
    public class TestTriangleBVH : MonoBehaviour
    {
        [Range(0, 9)]
        public int selectedDepth;

        private RtxObj[] objects;
        private NodeBVH[] bvh;
        private Vector3[] verts;
        private int[] trigs;
        private MeshData[] meshData;

        private Color[] colors = new Color[]
        {
            Color.white,
            Color.yellow,
            Color.green,
            Color.cyan,
            Color.blue,
            Color.magenta,
        };

        private void Start()
        {
            objects = FindObjectsByType<RtxObj>(FindObjectsSortMode.None);
            Mesh[] meshes = objects.Select(x => x.mesh).ToArray();
            MeshBvhConstructor.Build(meshes, out bvh, out verts, out trigs, out meshData);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            for (int i = 0; i < objects.Length; i++)
            {
                DrawMesh(i);
            }
        }

        private void DrawMesh(int meshIndex)
        {
            Matrix4x4 localToWorld = objects[meshIndex].transform.localToWorldMatrix;

            Span<(int nodeIndex, int depth)> stack = stackalloc (int, int)[20];
            stack[0] = (meshData[meshIndex].bvhHead, 0);

            int index = 1;
            while (index > 0)
            {
                (int nodeIndex, int depth) = stack[--index];
                NodeBVH node = bvh[nodeIndex];

                if (depth == selectedDepth)
                {
                    Gizmos.color = colors[depth % colors.Length];
                    Gizmos.DrawWireCube(node.bnd.center, node.bnd.size);
                }

                if (node.childIndex > 0)
                {
                    stack[index++] = (node.childIndex + 1, depth + 1);
                    stack[index++] = (node.childIndex, depth + 1);
                    continue;
                }

                if (depth > selectedDepth)
                    continue;

                Gizmos.color = Color.red;
                for (int i = node.start; i < node.end; i += 3)
                {
                    int ind1 = trigs[i];
                    int ind2 = trigs[i + 1];
                    int ind3 = trigs[i + 2];

                    Vector3 a = verts[ind1];
                    Vector3 b = verts[ind2];
                    Vector3 c = verts[ind3];

                    Gizmos.DrawLine(a, b);
                    Gizmos.DrawLine(c, b);
                    Gizmos.DrawLine(a, c);
                }
            }
        }
    }
}
