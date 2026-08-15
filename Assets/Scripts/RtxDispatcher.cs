using System;
using System.Linq;
using System.Runtime.InteropServices;
using Rtx.BVH;
using Rtx.Data;
using Rtx.Materials;
using UnityEngine;

namespace Rtx
{
    public class RtxDispatcher : MonoBehaviour
    {
        [Header("Settings")]
        public Vector2Int resolution;
        public int maxBounces,
            samples,
            tileSize;

        public bool enableRIS,
            enableGlobalBVH;

        [Header("Other")]
        public Camera cam;
        public Material blitMat;
        public ComputeShader shader;

        // shared data buffers
        private ComputeBuffer verticesBuffer,
            trianglesBuffer,
            meshesBuffer,
            bvhBuffer,
            materialsBuffer;

        // unshared data buffers
        private ComputeBuffer modelsBuffer,
            localToWorldMatricesBuffer,
            worldToLocalMatricesBuffer,
            globalBvhBuffer,
            lightsIdBuffer;

        private RenderTexture resultTexture;
        private Vector2Int dispatch;
        private int kernelId;

        private int frameIndex;

        // Start is called before the first frame update
        void Start()
        {
            if (enableRIS)
                shader.EnableKeyword("RIS_ON");
            else
                shader.DisableKeyword("RIS_ON");

            if (enableGlobalBVH)
                shader.EnableKeyword("GLOBAL_BVH_ON");
            else
                shader.DisableKeyword("GLOBAL_BVH_ON");

            kernelId = shader.FindKernel("CSMain");

            RtxObj[] objects = FindObjectsOfType<RtxObj>();

            Mesh[] meshes = objects.Select(x => x.mesh).Distinct().ToArray();

            #region Shared Models Data
            // GENERATE DATA
            MeshBvhConstructor.Build(
                meshes,
                out NodeBVH[] bvhData,
                out Vector3[] verticesData,
                out int[] trianglesData,
                out MeshData[] meshesData
            );

            RtxMaterial[] materials = objects.Select(x => x.material).Distinct().ToArray();
            MaterialData[] materialsData = materials.Select(x => x.materialData).ToArray();

            // CREATE BUFFERS
            verticesBuffer = new ComputeBuffer(verticesData.Length, Marshal.SizeOf<Vector3>());
            trianglesBuffer = new ComputeBuffer(trianglesData.Length, Marshal.SizeOf<int>());
            meshesBuffer = new ComputeBuffer(meshesData.Length, Marshal.SizeOf<MeshData>());

            bvhBuffer = new ComputeBuffer(bvhData.Length, Marshal.SizeOf<NodeBVH>());

            materialsBuffer = new ComputeBuffer(
                materialsData.Length,
                Marshal.SizeOf<MaterialData>()
            );

            // SET DATA
            verticesBuffer.SetData(verticesData);
            trianglesBuffer.SetData(trianglesData);
            meshesBuffer.SetData(meshesData);
            bvhBuffer.SetData(bvhData);
            materialsBuffer.SetData(materialsData);

            shader.SetBuffer(kernelId, "vertices", verticesBuffer);
            shader.SetBuffer(kernelId, "triangles", trianglesBuffer);
            shader.SetBuffer(kernelId, "meshes", meshesBuffer);
            shader.SetBuffer(kernelId, "bvh", bvhBuffer);
            shader.SetBuffer(kernelId, "materials", materialsBuffer);
            #endregion

            #region Unshared Models Data

            // GENERATE DATA

            // !!! SHOULD BE EXECUTED FIRST BECAUSE CHANGE ORDER OF OBJECTS
            NodeBVH[] globalBvhData = GlobalBvhConstructor.Build(objects);

            ModelData[] modelsData = objects
                .Select(x => new ModelData
                {
                    meshIndex = Array.IndexOf(meshes, x.mesh),
                    materialIndex = Array.IndexOf(materials, x.material),
                    boxMin = x.bounds.min,
                    boxMax = x.bounds.max,
                })
                .ToArray();

            Matrix4x4[] localToWorldMatricesData = objects
                .Select(x => x.transform.localToWorldMatrix)
                .ToArray();

            Matrix4x4[] worldToLocalMatricesData = objects
                .Select(x => x.transform.worldToLocalMatrix)
                .ToArray();

            int[] lightsIdData = objects
                .Where(x => x.material.emissionStrength > 0.01f)
                .OrderByDescending(x => x.material.emissionStrength)
                .Select(x => Array.IndexOf(objects, x))
                .ToArray();

            // CREATE BUFFERS
            modelsBuffer = new ComputeBuffer(objects.Length, Marshal.SizeOf<ModelData>());

            localToWorldMatricesBuffer = new ComputeBuffer(
                objects.Length,
                Marshal.SizeOf<Matrix4x4>()
            );

            worldToLocalMatricesBuffer = new ComputeBuffer(
                objects.Length,
                Marshal.SizeOf<Matrix4x4>()
            );

            globalBvhBuffer = new ComputeBuffer(globalBvhData.Length, Marshal.SizeOf<NodeBVH>());

            lightsIdBuffer = new ComputeBuffer(lightsIdData.Length, Marshal.SizeOf<int>());

            // SET DATA
            modelsBuffer.SetData(modelsData);
            localToWorldMatricesBuffer.SetData(localToWorldMatricesData);
            worldToLocalMatricesBuffer.SetData(worldToLocalMatricesData);
            globalBvhBuffer.SetData(globalBvhData);
            lightsIdBuffer.SetData(lightsIdData);

            shader.SetBuffer(kernelId, "models", modelsBuffer);
            shader.SetBuffer(kernelId, "localToWorldMatrices", localToWorldMatricesBuffer);
            shader.SetBuffer(kernelId, "worldToLocalMatrices", worldToLocalMatricesBuffer);
            shader.SetBuffer(kernelId, "globalBvh", globalBvhBuffer);
            shader.SetBuffer(kernelId, "lightsId", lightsIdBuffer);

            #endregion

            #region Raytrace Params Setup

            // CAMERA SETUP
            Matrix4x4 clip = new Matrix4x4
            {
                m00 = 2.0f / resolution.x,
                m11 = 2.0f / resolution.y,
                m03 = -1.0f,
                m13 = -1.0f,
                m22 = 1.0f,
                m33 = 1.0f,
            };
            Matrix4x4 mat = cam.cameraToWorldMatrix * cam.projectionMatrix.inverse * clip;
            shader.SetMatrix("cameraMatrix", mat);
            shader.SetVector("cameraPosition", cam.transform.position);

            // OTHER PARAMS
            shader.SetInts("resolution", resolution.x, resolution.y);
            shader.SetInt("maxBounces", maxBounces);
            shader.SetInt("samples", samples);
            shader.SetInt("tileSize", tileSize);

            resultTexture = new RenderTexture(resolution.x, resolution.y, 0)
            {
                enableRandomWrite = true,
            };

            resultTexture.Create();
            shader.SetTexture(kernelId, "result", resultTexture);

            shader.GetKernelThreadGroupSizes(kernelId, out uint x, out uint y, out uint _);
            int dispX = (int)x;
            int dispY = (int)y;
            dispatch = new Vector2Int(
                tileSize / dispX + ((tileSize % dispX != 0) ? 1 : 0),
                tileSize / dispY + ((tileSize % dispY != 0) ? 1 : 0)
            );
            #endregion


            // DISABLE MESH RENDERERS
            foreach (RtxObj obj in objects)
                obj.gameObject.SetActive(false);
        }

        void Update()
        {
            shader.SetInt("frameIndex", frameIndex++);
            shader.SetInt("randOffset", UnityEngine.Random.Range(int.MinValue, int.MaxValue));

            for (int y = 0; y < resolution.y; y += tileSize)
            {
                for (int x = 0; x < resolution.x; x += tileSize)
                {
                    shader.SetInt("offsetX", x);
                    shader.SetInt("offsetY", y);
                    shader.Dispatch(kernelId, dispatch.x, dispatch.y, 1);
                }
            }
        }

        private void OnRenderImage(RenderTexture _, RenderTexture destination)
        {
            Graphics.Blit(resultTexture, destination, blitMat);
        }

        private void OnDisable()
        {
            resultTexture.DiscardContents();

            // shared data buffers
            verticesBuffer.Dispose();
            trianglesBuffer.Dispose();
            meshesBuffer.Dispose();
            bvhBuffer.Dispose();
            materialsBuffer.Dispose();

            // unshared data buffers
            modelsBuffer.Dispose();
            localToWorldMatricesBuffer.Dispose();
            worldToLocalMatricesBuffer.Dispose();
            globalBvhBuffer.Dispose();
            lightsIdBuffer.Dispose();
        }
    }
}
