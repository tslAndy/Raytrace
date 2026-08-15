using Rtx.Materials;
using UnityEngine;

namespace Rtx
{
    public class RtxObj : MonoBehaviour
    {
        public RtxMaterial material;

        [HideInInspector]
        public Mesh mesh;

        [HideInInspector]
        public Bnd bounds;

        private void Awake()
        {
            mesh = GetComponent<MeshFilter>().sharedMesh;
            bounds = (Bnd)GetComponent<MeshRenderer>().bounds;
        }
    }
}
