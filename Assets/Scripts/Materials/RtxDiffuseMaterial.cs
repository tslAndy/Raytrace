using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Rtx.Data;

namespace Rtx.Materials
{
    [CreateAssetMenu(fileName = "Diffuse Material", menuName = "RtxMaterial/Diffuse")]
    public class RtxDiffuseMaterial : RtxMaterial
    {
        public Color mainColor, specularColor, emissionColor;

        [Range(0.0f, 1.0f)]
        public float smooth, specular;

        [Range(0.0f, 50.0f)]
        public float emission;

        public override MaterialData materialData => CreateMaterial<Data>(
            MaterialType.Diffuse,
            new Data
            {
                mainColor = mainColor.ToVec3(),
                specularColor = specularColor.ToVec3(),
                emissionColor = emissionColor.ToVec3(),
                smooth = smooth,
                specular = specular,
                emission = emission
            }
        );

        public override float emissionStrength => emission;

        [StructLayout(LayoutKind.Sequential)]
        private struct Data
        {
            public Vector3 mainColor, specularColor, emissionColor;
            public float smooth, specular, emission;
        }
    }
}
