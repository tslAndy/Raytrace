using System.Runtime.InteropServices;
using UnityEngine;

namespace Rtx.Data
{

    [StructLayout(LayoutKind.Sequential)]
    public struct ModelData
    {
        public Vector3 boxMin,
            boxMax;
        public int meshIndex,
            materialIndex;
    }
}
