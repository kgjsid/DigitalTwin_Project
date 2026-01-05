using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

public class TerrainTextureExtractor
{
    public Mesh originMesh;
    public MeshRenderer originMeshRender;
    public ComputeShader textureExtractorCS;

    // 1. submesh의 triangleInfo로부터 vertex , uv 값 찾아오기. 
    // 2. uv값과 텍스쳐 사이즈를 바탕으로 텍스쳐에서 사용하고 있는 픽셀 위치 얻어오기
    // 3. 픽셀 위치를 기준. triangle 내부의 픽셀값만 선별하여 저장
    // 4. 저장한 픽셀값을 새로운 텍스쳐에 할당

    private ComputeBuffer vertexBuffer;
    private ComputeBuffer uvBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer outputUVBuffer;

    private Texture2D newTexture;
    private string fileName;
    public RenderTexture maskTexture;

    private struct UVBound
    {
        public float minX;
        public float maxX;
        public float minY;
        public float maxY;
    }

    private struct UVData
    {
        public Vector2 maxUV;
        public Vector2 minUV;
    }

    public TerrainTextureExtractor(ComputeShader textureExtractorCS, string fileName)
    {
        this.textureExtractorCS = textureExtractorCS;
        this.fileName = fileName;
    }

    public void ExtractTexture(MeshFilter meshFilter, MeshRenderer meshRenderer, bool isFoldering, out Vector2Int[] startIndexList, out Vector2Int[] originTextureList, out Vector2Int[] newTextureList, out Material[] newMaterialList)
    {
        originMesh = meshFilter.sharedMesh;
        originMeshRender = meshRenderer;
        startIndexList = new Vector2Int[meshFilter.sharedMesh.subMeshCount];
        originTextureList = new Vector2Int[meshFilter.sharedMesh.subMeshCount];
        newTextureList = new Vector2Int[meshFilter.sharedMesh.subMeshCount];
        newMaterialList = new Material[meshFilter.sharedMesh.subMeshCount];

        for (int i = 0; i < meshFilter.sharedMesh.subMeshCount; i++)
        {
            InitComputeBuffer(i);
            CreateRenderTexture(i);
            SendComputeBuffer(i);
            GetComputeBuffer(i, isFoldering, out int startXIndex, out int startYIndex, out Texture2D originTexture, out Texture2D newTexture, out Material newMaterial);
            startIndexList[i] = new Vector2Int(startXIndex, startYIndex);
            originTextureList[i] = new Vector2Int(originTexture.width, originTexture.height);
            newTextureList[i] = new Vector2Int(newTexture.width, newTexture.height);
            newMaterialList[i] = newMaterial;

            RelaseBuffer();
        }
    }

    private void InitComputeBuffer(int subMeshIndex)
    {
        var meshTriangle = originMesh.GetTriangles(subMeshIndex);
        UnityEngine.Rendering.SubMeshDescriptor descriptor = originMesh.GetSubMesh(subMeshIndex);

        vertexBuffer = new ComputeBuffer(originMesh.vertexCount, sizeof(float) * 3);
        uvBuffer = new ComputeBuffer(originMesh.vertexCount, sizeof(float) * 2);
        triangleBuffer = new ComputeBuffer(originMesh.triangles.Length, sizeof(int));
        outputUVBuffer = new ComputeBuffer(meshTriangle.Length / 3, sizeof(float) * 4);

        vertexBuffer.SetData(originMesh.vertices);
        uvBuffer.SetData(originMesh.uv);
        triangleBuffer.SetData(originMesh.triangles);
    }

    private void CreateRenderTexture(int subMeshIndex)
    {
        var subTexture = originMeshRender.sharedMaterials[subMeshIndex].GetTexture("_BaseMap");

        maskTexture = new RenderTexture(subTexture.width, subTexture.height, 0);
        maskTexture.enableRandomWrite = true;
        maskTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;
        maskTexture.Create();
    }

    private void SendComputeBuffer(int subMeshIndex)
    {
        int triangleKernel = textureExtractorCS.FindKernel("CSMain");

        // input Buffer
        textureExtractorCS.SetBuffer(triangleKernel, "_vertex", vertexBuffer);
        textureExtractorCS.SetBuffer(triangleKernel, "_uv", uvBuffer);
        textureExtractorCS.SetBuffer(triangleKernel, "_triangle", triangleBuffer);
        textureExtractorCS.SetBuffer(triangleKernel, "_outUV", outputUVBuffer);
        
        UnityEngine.Rendering.SubMeshDescriptor descriptor = originMesh.GetSubMesh(subMeshIndex);
        int triangleCount = originMesh.GetTriangles(subMeshIndex).Length / 3;
        int startIndex = descriptor.indexStart;
        var subTexture = originMeshRender.sharedMaterials[subMeshIndex].GetTexture("_BaseMap");

        textureExtractorCS.SetInt("_indexStart", startIndex);
        textureExtractorCS.SetInt("_triangleCount", triangleCount);
        textureExtractorCS.SetInt("_textureWidth", subTexture.width);
        textureExtractorCS.SetInt("_textureHeight", subTexture.height);
        textureExtractorCS.SetTexture(triangleKernel, "_originTexture", subTexture);
        textureExtractorCS.SetTexture(triangleKernel, "_maskTexture", maskTexture);

        textureExtractorCS.GetKernelThreadGroupSizes(triangleKernel, out uint threadsPerGroup, out uint _, out uint _);

        int threadGroupCount = Mathf.CeilToInt((triangleCount) / (float)threadsPerGroup);
        textureExtractorCS.Dispatch(triangleKernel, threadGroupCount, 1, 1);

        RenderTexture.active = maskTexture;
        
        var convertTexture = new Texture2D(subTexture.width, subTexture.height);
        convertTexture.ReadPixels(new Rect(0, 0, maskTexture.width, maskTexture.height), 0, 0);
        convertTexture.Apply();

        RenderTexture.active = null;

        newTexture = convertTexture.DeCompress();
    }

    private void GetComputeBuffer(int subMeshIndex, bool isFoldering, out int startXIndex, out int startYIndex, out Texture2D originTexture, out Texture2D newTexture, out Material newMaterial)
    {
        int uvCount = originMesh.GetTriangles(subMeshIndex).Length / 3;
        var uvData = new UVData[uvCount];
        outputUVBuffer.GetData(uvData);

        originTexture = this.newTexture.DeCompress();
        var newTextureSize = GetNewTextureSize(uvData);

        int newTextureWidth = Mathf.CeilToInt(originTexture.width * (newTextureSize.maxX - newTextureSize.minX));
        int newTextureHeight = Mathf.CeilToInt(originTexture.height * (newTextureSize.maxY - newTextureSize.minY));

        newTexture = new Texture2D(Mathf.NextPowerOfTwo(newTextureWidth), Mathf.NextPowerOfTwo(newTextureHeight), TextureFormat.RGBA32, false);
        // Texture2D debugTexture = new Texture2D(originTexture.width, originTexture.height);

        Color[] originFillColorArray = new Color[originTexture.width * originTexture.height];
        Color[] newFillColorArray = new Color[newTexture.width * newTexture.height];
        SetTextureBackGroundColor(newTexture, Color.clear);

        startXIndex = (int)(newTextureSize.minX * originTexture.width);
        startYIndex = (int)(newTextureSize.minY * originTexture.width);
        int originPoint = startXIndex + startYIndex * originTexture.height;

        originFillColorArray = originTexture.GetPixels();

        for (int h = 0; h < newTexture.height - 1; h++)
        {
            for (int w = 0; w < newTexture.width - 1; w++)
            {
                if (w + h * newTexture.width > newFillColorArray.Length - 1)
                    continue;

                if (w + h * originTexture.width + originPoint > originFillColorArray.Length - 1)
                    continue;

                if (originFillColorArray[w + h * originTexture.width + originPoint] == Color.clear)
                    continue;

                newFillColorArray[w + h * newTexture.width] = originFillColorArray[w + h * originTexture.width + originPoint];
            }
        }

        // debugTexture.SetPixels(originFillColorArray);
        // debugTexture.Apply();

        newTexture.SetPixels(newFillColorArray);
        newTexture.Apply();
        newTexture = newTexture.DeCompress();
        byte[] bytes = newTexture.EncodeToJPG();
        File.WriteAllBytes(isFoldering ? $"{Application.dataPath}/TextureFolder/{fileName}/{fileName}_{subMeshIndex}.jpg"
                                       : $"{Application.dataPath}/TextureFolder/{fileName}_{subMeshIndex}.jpg", bytes);

        newMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        // 디버깅용 텍스쳐. (사이즈 압축 전 원본)
        // bytes = debugTexture.EncodeToJPG();
        // File.WriteAllBytes($"{Application.dataPath}/TerrainFolder/computeDebug.jpg", bytes);

        // UnityEngine.Object.DestroyImmediate(newTexture);
        // UnityEngine.Object.Destroy(debugTexture);
    }

    private void RelaseBuffer()
    {
        vertexBuffer.Release();
        uvBuffer.Release();
        triangleBuffer.Release();
        outputUVBuffer.Release();

        UnityEngine.Object.DestroyImmediate(newTexture);
    }

    private UVBound GetNewTextureSize(UVData[] uvData)
    {
        float minX = uvData.Select(uv => uv.minUV.x).Min();
        float maxX = uvData.Select(uv => uv.maxUV.x).Max();
        float minY = uvData.Select(uv => uv.minUV.y).Min();
        float maxY = uvData.Select(uv => uv.maxUV.y).Max();

        return new UVBound()
        {
            minX = minX,
            maxX = maxX,
            minY = minY,
            maxY = maxY
        };
    }

    private void SetTextureBackGroundColor(Texture2D texture, Color color)
    {
        Color[] clearColors = new Color[texture.width * texture.height];

        for (int i = 0; i < clearColors.Length; i++)
        {
            clearColors[i] = color;
        }
        texture.SetPixels(clearColors);
    }
}
