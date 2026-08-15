using UnityEngine;

namespace Rtx.Materials
{
    public static class ColorExtensions
    {
        public static Vector3 ToVec3(this Color col) => new Vector3(col.r, col.g, col.b);
    }
}
