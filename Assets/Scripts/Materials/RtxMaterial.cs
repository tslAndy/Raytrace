using System;
using System.Runtime.InteropServices;
using Rtx.Data;
using UnityEngine;

namespace Rtx.Materials
{
    public abstract class RtxMaterial : ScriptableObject
    {
        public abstract MaterialData materialData { get; }
        public abstract float emissionStrength { get; } // required in sorting for MIS

        // user defined material can be 60 bytes long
        protected static MaterialData CreateMaterial<T>(MaterialType type, T data)
            where T : unmanaged
        {
            if (Marshal.SizeOf<T>() > 60)
                throw new Exception($"{typeof(T)} is too big");

            Span<byte> span = stackalloc byte[64];
            //
            MemoryMarshal.Write<T>(span.Slice(0, 60), ref data);
            //
            MemoryMarshal.Write<MaterialType>(span.Slice(60, 4), ref type);
            //
            MaterialData result = MemoryMarshal.Read<MaterialData>(span);
            //
            return result;
        }
    }
}
