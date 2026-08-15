using System.Runtime.InteropServices;

namespace Rtx.Data
{
    // THANKS UNITY FOR NOT SUPPORTING C# 12,
    // INSTEAD OF USING INLINE ARRAY
    // HAVE TO WRITE THIS SHIT
    [StructLayout(LayoutKind.Sequential)]
    public struct MaterialData
    {
        public long f0,
            f1,
            f2,
            f3,
            f4,
            f5,
            f6,
            f7;
    }
}
