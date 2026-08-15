using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class Dispatcher : MonoBehaviour
{
    public Vector2Int resolution;
    public int maxBounces,
        samples;

    public Camera cam;
    public Material blitMat;
    public ComputeShader shader;

    private readonly int ResolutionId = Shader.PropertyToID("resolution");
    private readonly int MaxBouncesId = Shader.PropertyToID("maxBounces");
    private readonly int SamplesId = Shader.PropertyToID("samples");

    private readonly int CameraPositionId = Shader.PropertyToID("cameraPosition");
    private readonly int CameraMatrixId = Shader.PropertyToID("cameraMatrix");

    private readonly int FrameIndexId = Shader.PropertyToID("frameIndex");

    private readonly int ResultTextureId = Shader.PropertyToID("result");
    private readonly int VerticesID = Shader.PropertyToID("vertices");
    private readonly int NormalsID = Shader.PropertyToID("normals");
    private readonly int TrianglesID = Shader.PropertyToID("triangles");
    private readonly int MatricesID = Shader.PropertyToID("matrices");
    private readonly int MaterialsID = Shader.PropertyToID("materials");
    private readonly int EmissiveInfoID = Shader.PropertyToID("emissiveInfo");

    private RenderTexture resultTexture;
    private ComputeBuffer verticesBuffer,
        normalsBuffer,
        trianglesBuffer,
        matricesBuffer,
        materialsBuffer,
        emissiveInfoBuffer;

    private Vector2Int dispatch;
    private int mainKernelId;

    private int frameIndex;

    private void Start()
    {
        // ----------------------- CAMERA SETUP -----------------------
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
        shader.SetMatrix(CameraMatrixId, mat);
        shader.SetVector(CameraPositionId, cam.transform.position);

        // ----------------------- SHADER SETUP -----------------------
        shader.SetInts(ResolutionId, resolution.x, resolution.y);
        shader.SetInt(MaxBouncesId, maxBounces);
        shader.SetInt(SamplesId, samples);

        mainKernelId = shader.FindKernel("CSMain");

        shader.GetKernelThreadGroupSizes(mainKernelId, out uint x, out uint y, out uint _);
        int dispX = (int)x;
        int dispY = (int)y;
        dispatch = new Vector2Int(
            resolution.x / dispX + ((resolution.x % dispX != 0) ? 1 : 0),
            resolution.y / dispY + ((resolution.y % dispY != 0) ? 1 : 0)
        );

        // ----------------------- MODELS SETUP -----------------------
        RtxObj[] rtxObjs = FindObjectsByType<RtxObj>(FindObjectsSortMode.None)
            .OrderByDescending(x => x.mat.emissionStrength)
            .ToArray();

        int totalVertsCount = rtxObjs.Sum(x => x.GetComponent<MeshFilter>().mesh.vertices.Length);
        int totalTrigsCount =
            rtxObjs.Sum(x => x.GetComponent<MeshFilter>().mesh.triangles.Length) / 3;
        int totalEmissiveCount = rtxObjs.Count(x =>
            x.GetComponent<RtxObj>().mat.emissionStrength > 0.001f
        );

        List<Vector3> vertices = new List<Vector3>(totalVertsCount);
        List<Vector3> normals = new List<Vector3>(totalVertsCount);
        List<RtxTriangle> triangles = new List<RtxTriangle>(totalTrigsCount);
        List<Matrix4x4> matrices = new List<Matrix4x4>(rtxObjs.Length);
        List<RtxMaterial> materials = new List<RtxMaterial>(rtxObjs.Length);
        List<RtxEmissiveInfo> emissiveInfos = new List<RtxEmissiveInfo>(totalEmissiveCount);

        for (int i = 0; i < rtxObjs.Length; i++)
        {
            RtxObj rtxObj = rtxObjs[i];
            Mesh rtxMesh = rtxObj.GetComponent<MeshFilter>().mesh;
            rtxObj.gameObject.SetActive(false);

            if (i < totalEmissiveCount)
            {
                emissiveInfos.Add(
                    new RtxEmissiveInfo
                    {
                        trigIndex = triangles.Count,
                        trigCount = rtxMesh.triangles.Length / 3,
                    }
                );
            }

            int vertIndOffset = vertices.Count;
            for (int j = 0; j < rtxMesh.triangles.Length; j += 3)
            {
                triangles.Add(
                    new RtxTriangle
                    {
                        indA = vertIndOffset + rtxMesh.triangles[j],
                        indB = vertIndOffset + rtxMesh.triangles[j + 1],
                        indC = vertIndOffset + rtxMesh.triangles[j + 2],
                        modelIndex = i,
                    }
                );
            }

            vertices.AddRange(rtxMesh.vertices);
            normals.AddRange(rtxMesh.normals);
            matrices.Add(rtxObj.transform.localToWorldMatrix);
            materials.Add(rtxObj.mat);
        }

        // ----------------------- BUFFERS, TEXTURES -----------------------
        verticesBuffer = new ComputeBuffer(vertices.Count, Marshal.SizeOf<Vector3>());
        normalsBuffer = new ComputeBuffer(normals.Count, Marshal.SizeOf<Vector3>());
        trianglesBuffer = new ComputeBuffer(triangles.Count, Marshal.SizeOf<RtxTriangle>());
        matricesBuffer = new ComputeBuffer(rtxObjs.Length, Marshal.SizeOf<Matrix4x4>());
        materialsBuffer = new ComputeBuffer(rtxObjs.Length, Marshal.SizeOf<RtxMaterial>());
        emissiveInfoBuffer = new ComputeBuffer(
            emissiveInfos.Count,
            Marshal.SizeOf<RtxEmissiveInfo>()
        );

        verticesBuffer.SetData(vertices);
        normalsBuffer.SetData(normals);
        trianglesBuffer.SetData(triangles);
        matricesBuffer.SetData(matrices);
        materialsBuffer.SetData(materials);
        emissiveInfoBuffer.SetData(emissiveInfos);

        resultTexture = new RenderTexture(resolution.x, resolution.y, 24)
        {
            enableRandomWrite = true,
        };
        resultTexture.Create();

        shader.SetTexture(mainKernelId, ResultTextureId, resultTexture);
        shader.SetBuffer(mainKernelId, VerticesID, verticesBuffer);
        shader.SetBuffer(mainKernelId, NormalsID, normalsBuffer);
        shader.SetBuffer(mainKernelId, TrianglesID, trianglesBuffer);
        shader.SetBuffer(mainKernelId, MatricesID, matricesBuffer);
        shader.SetBuffer(mainKernelId, MaterialsID, materialsBuffer);
        shader.SetBuffer(mainKernelId, EmissiveInfoID, emissiveInfoBuffer);
    }

    private void Update()
    {
        shader.SetInt(FrameIndexId, frameIndex++);
        shader.Dispatch(mainKernelId, dispatch.x, dispatch.y, 1);
    }

    private void OnRenderImage(RenderTexture _, RenderTexture destination)
    {
        Graphics.Blit(resultTexture, destination, blitMat);
    }

    private void OnDisable()
    {
        resultTexture.Release();
        verticesBuffer.Release();
        normalsBuffer.Release();
        trianglesBuffer.Release();
        matricesBuffer.Release();
        materialsBuffer.Release();
        emissiveInfoBuffer.Release();
    }
}

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct RtxMaterial
{
    public Color color;
    public float smooth;

    public Color glossyColor;
    public float glossy;

    public Color emission;
    public float emissionStrength;
}

[StructLayout(LayoutKind.Sequential)]
public struct RtxTriangle
{
    public int indA,
        indB,
        indC;
    public int modelIndex;
}

[StructLayout(LayoutKind.Sequential)]
public struct RtxEmissiveInfo
{
    public int trigIndex,
        trigCount;
}
