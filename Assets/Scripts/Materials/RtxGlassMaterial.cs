using System;
using System.Runtime.InteropServices;
using Rtx.Data;
using UnityEngine;

namespace Rtx.Materials
{
    [CreateAssetMenu(fileName = "Glass Material", menuName = "RtxMaterial/Glass")]
    public class RtxGlassMaterial : RtxMaterial
    {
        [Range(0.0f, 5.0f)]
        public float ior;

        [Range(0.0f, 1.0f)]
        public float transmissionDiffuse, reflectionDiffuse;

        [Range(0.0f, 20.0f)]
        public float absorption;

        public Color mainColor; // will be converted to absorption by 1 - mainColor

        public override MaterialData materialData => CreateMaterial<Data>(
            MaterialType.Glass,
            new Data
            {
                ior = ior,
                transmissionDiffuse = transmissionDiffuse,
                reflectionDiffuse = reflectionDiffuse,
                absorption = absorption,
                absorptionColor = Vector3.one - mainColor.ToVec3()

            });

        public override float emissionStrength => 0.0f;

        [StructLayout(LayoutKind.Sequential)]
        private struct Data
        {
            public float ior, transmissionDiffuse, reflectionDiffuse, absorption;
            public Vector3 absorptionColor;
        }
    }
}
