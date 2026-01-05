using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComputeMeshSplitter : MonoBehaviour
{
    public ComputeShader computeShader;
    public MeshFilter meshFilter;
    public Mesh originMesh;

    private ComputeBuffer vertexDataBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer newVertexDataBuffer;
    private ComputeBuffer newTriangleBuffer;

    struct VertexData
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector4 tangent;
        public Vector2 uv;
    }

    private void Start()
    {
        if (originMesh == null)
            originMesh = meshFilter != null ? meshFilter.mesh : GetComponent<MeshFilter>().mesh;

        for (int i = 0; i < 1; i++)
        {
            GameObject centerObj = new GameObject($"terrainObj{i}");
            centerObj.transform.position = Vector3.zero;
            RunComputeShader(i, centerObj);
        }
    }

    private void RunComputeShader(int subMeshIndex, GameObject centerObj)
    {
        UnityEngine.Rendering.SubMeshDescriptor descriptor = originMesh.GetSubMesh(subMeshIndex);

        int vertexCount = descriptor.vertexCount;
        int triangleCount = originMesh.GetTriangles(subMeshIndex).Length;

        // VertexData 구조체 배열 생성
        VertexData[] vertexDataArray = new VertexData[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            int vertexIndex = i + descriptor.firstVertex;
            vertexDataArray[i] = new VertexData
            {
                position = originMesh.vertices[vertexIndex],
                normal = originMesh.normals[vertexIndex],
                tangent = originMesh.tangents[vertexIndex],
                uv = originMesh.uv[vertexIndex]
            };
        }

        int[] triangles = originMesh.GetTriangles(subMeshIndex);

        // ComputeBuffer 생성 (구조체 크기 사용)
        int vertexDataSize = sizeof(float) * (3 + 3 + 4 + 2); // Vector3 + Vector3 + Vector4 + Vector2
        vertexDataBuffer = new ComputeBuffer(vertexCount, vertexDataSize);
        triangleBuffer = new ComputeBuffer(triangleCount, sizeof(int));
        newVertexDataBuffer = new ComputeBuffer(triangleCount, vertexDataSize, ComputeBufferType.Append);
        newTriangleBuffer = new ComputeBuffer(triangleCount, sizeof(int), ComputeBufferType.Append);

        // 버퍼에 데이터 전송
        vertexDataBuffer.SetData(vertexDataArray);
        triangleBuffer.SetData(triangles);

        // ComputeShader에 데이터 전달
        computeShader.SetBuffer(0, "vertexData", vertexDataBuffer);
        computeShader.SetBuffer(0, "triangleBuffer", triangleBuffer);
        computeShader.SetBuffer(0, "newVertexData", newVertexDataBuffer);
        computeShader.SetBuffer(0, "newTriangleBuffer", newTriangleBuffer);

        // GPU 실행
        int threadGroups = Mathf.CeilToInt(triangleCount / 3.0f / 64);
        computeShader.Dispatch(0, threadGroups, 1, 1);

        // GPU에서 새로운 데이터 가져오기
        VertexData[] newVertexData = new VertexData[triangleCount];
        int[] newTriangle = new int[triangleCount];

        newVertexDataBuffer.GetData(newVertexData);
        newTriangleBuffer.GetData(newTriangle);

        // 새로운 Mesh 생성
        Mesh newMesh = new Mesh();
        Vector3[] newVertices = new Vector3[triangleCount];
        Vector3[] newNormals = new Vector3[triangleCount];
        Vector4[] newTangents = new Vector4[triangleCount];
        Vector2[] newUVs = new Vector2[triangleCount];

        for (int i = 0; i < triangleCount; i++)
        {
            newVertices[i] = newVertexData[i].position;
            newNormals[i] = newVertexData[i].normal;
            newTangents[i] = newVertexData[i].tangent;
            newUVs[i] = newVertexData[i].uv;
        }

        newMesh.vertices = newVertices;
        newMesh.normals = newNormals;
        newMesh.tangents = newTangents;
        newMesh.uv = newUVs;
        newMesh.triangles = newTriangle;
        newMesh.RecalculateNormals();

        GameObject newMeshObj = new GameObject("NewSubMesh");
        newMeshObj.transform.position = transform.position;
        MeshFilter newMeshFilter = newMeshObj.AddComponent<MeshFilter>();
        newMeshFilter.mesh = newMesh;
        newMeshObj.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        // 버퍼 해제
        vertexDataBuffer.Release();
        triangleBuffer.Release();
        newVertexDataBuffer.Release();
        newTriangleBuffer.Release();
    }
}