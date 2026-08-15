using System.Runtime.InteropServices;

namespace Rtx.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MeshData
    {
        public int bvhHead;
        public int trigStart,
            trigCount;

        public MeshData(int bvhHead, int trigStart, int trigCount)
        {
            this.bvhHead = bvhHead;
            this.trigStart = trigStart;
            this.trigCount = trigCount;
        }
    }
}
