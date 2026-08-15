using System;
using Rtx.BVH;
using UnityEngine;

namespace Rtx.Test
{
    public class TestObjectBVH : MonoBehaviour
    {
        [Range(0, 9)]
        public int selectedDepth;

        private RtxObj[] objects;
        private NodeBVH[] bvh;

        private Color[] colors = new Color[]
        {
            Color.white,
            Color.red,
            Color.yellow,
            Color.green,
            Color.cyan,
            Color.blue,
            Color.magenta,
        };

        private void Start()
        {
            objects = FindObjectsByType<RtxObj>(FindObjectsSortMode.None);
            bvh = GlobalBvhConstructor.Build(objects);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            Span<(int nodeIndex, int depth)> stack = stackalloc (int, int)[11];

            int index = 1;
            while (index > 0)
            {
                (int nodeIndex, int depth) = stack[--index];
                NodeBVH node = bvh[nodeIndex];

                if (depth <= selectedDepth)
                {
                    Gizmos.color = colors[depth % colors.Length];
                    Gizmos.DrawWireCube(node.bnd.center, node.bnd.size);
                }

                if (node.childIndex > 0)
                {
                    stack[index++] = (node.childIndex + 1, depth + 1);
                    stack[index++] = (node.childIndex, depth + 1);
                }
            }
        }
    }
}
