using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class TerrainMeshSplitter
{
    // 1. 원본 Mesh 데이터(vertex, uv, tangents, normal, triangleIndex)값 전달 
    // 2. 공유 정점을 모두 분리하여 삼각형 데이터로 분리(GPU)
    // 3. 배열을 다시 CPU로 전달받아 중복된 정점을 제거하고, 하나의 Mesh로 통합

    public Mesh originMesh;
    public MeshRenderer originMeshRenderer;
    public List<MeshFilter> meshFilterList;
    public GameObject[] centerObj;
    public Material material;

    public ComputeShader meshSplitterCS;

    private ComputeBuffer vertexBuffer;         // 전달할 vertex 데이터(Vector3)
    private ComputeBuffer uvBuffer;             // 전달할 UV 데이터(Vector2)
    private ComputeBuffer normalBuffer;         // 전달할 normal 데이터(Vector3)
    private ComputeBuffer tangentBuffer;        // 전달할 tangent 데이터(Vector4)
    private ComputeBuffer triangleBuffer;       // 전달할 triangle 데이터(int)

    private ComputeBuffer[] outputBuffer;       // 전달 받을 삼각형 Mesh 데이터(vertex, uv, normal, tangent 각각 3개씩)
    // 중복된 정점을 처리하기 위한 Dictionary
    private Dictionary<VertexData, int> vertexDic;

    private int[] frontFaceArray;
    private int[] backFaceArray;

    public struct TriangleData
    {
        public Vector3 vertex0;
        public Vector3 vertex1;
        public Vector3 vertex2;
        public Vector2 uv0;
        public Vector2 uv1;
        public Vector2 uv2;
        public Vector3 normal0;
        public Vector3 normal1;
        public Vector3 normal2;
        public Vector4 tangent0;
        public Vector4 tangent1;
        public Vector4 tangent2;
    }

    public struct VertexData
    {
        public Vector3 position;
        public Vector2 uv;
        public Vector3 normal;
        public Vector4 tangent;

        public override bool Equals(object obj)
        {
            return obj is VertexData vertexData &&
                   vertexData == this;
        }

        public static bool operator == (VertexData left, VertexData right)
        {
            return (left.position == right.position
                    && left.uv == right.uv
                    && left.normal == right.normal
                    && left.tangent == right.tangent);
        }

        public static bool operator != (VertexData left, VertexData right)
        {
            return !(left == right);
        }
    }

    public TerrainMeshSplitter(ComputeShader meshSplitterCS)
    {
       meshFilterList = new List<MeshFilter>(2 << 16);
       
       frontFaceArray = new int[] { 0, 1, 2 };
       backFaceArray = new int[] { 2, 0, 1 };
       
       this.meshSplitterCS = meshSplitterCS;
    }

    public void InitSetting(ComputeShader meshSplitterCS)
    {
        meshFilterList = new List<MeshFilter>(2 << 16);

        frontFaceArray = new int[] { 0, 1, 2 };
        backFaceArray = new int[] { 2, 0, 1 };

        this.meshSplitterCS = meshSplitterCS;
    }

    public void MeshSplit(MeshFilter meshFilter, MeshRenderer meshRenderer, out Mesh[] newMeshList)
    {
        originMesh = meshFilter.sharedMesh;
        originMeshRenderer = meshRenderer;
        newMeshList = new Mesh[originMesh.subMeshCount];
        centerObj = new GameObject[originMesh.subMeshCount];
        vertexDic = new Dictionary<VertexData, int>();

        outputBuffer = new ComputeBuffer[originMesh.subMeshCount];
        for (int i = 0; i < originMesh.subMeshCount; i++)
        {
            centerObj[i] = new GameObject($"terrainObj{i}");
            centerObj[i].transform.position = Vector3.zero;

            InitComputeBuffer(i);
            SendComputeBuffer(i);
            newMeshList[i] = GetComputeBuffer(i);

            ReleaseBuffer(i);
        }

        for (int i = 0; i < centerObj.Length; i++) UnityEngine.Object.DestroyImmediate(centerObj[i]);
    }

    private void InitComputeBuffer(int subMeshIndex)
    {
        var meshTriangle = originMesh.GetTriangles(subMeshIndex);
        UnityEngine.Rendering.SubMeshDescriptor descriptor = originMesh.GetSubMesh(subMeshIndex);

        // 요소 개수, 요소 하나의 바이트 크기
        vertexBuffer = new ComputeBuffer(originMesh.vertexCount, sizeof(float) * 3);
        uvBuffer = new ComputeBuffer(originMesh.vertexCount, sizeof(float) * 2);
        normalBuffer = new ComputeBuffer(originMesh.vertexCount, sizeof(float) * 3);
        tangentBuffer = new ComputeBuffer(originMesh.vertexCount, sizeof(float) * 4);
        triangleBuffer = new ComputeBuffer(originMesh.triangles.Length, sizeof(int));

        // triangle 하나에 vertex, uv, normal, tangents 각각 3개씩
        outputBuffer[subMeshIndex] = new ComputeBuffer(meshTriangle.Length / 3, sizeof(float) * ((3 + 2 + 3 + 4) * 3));

        // ComputeBuffer에 데이터 저장
        /* OriginMesh 전달 이유
         * 3DMax에서 데이터가 넘어올 때, Vertex 중복을 피하기 위한 처리를 추가로 해주시는 것 같음
         * 그래서 데이터가 연속적이지 않음(하나의 삼각형에 Index가 2, 3, 19002 이런 식으로 사용됨)
         * 실제 SubMesh가 사용하는 Vertex만 찾으려면 전 Vertex를 탐색하는 과정이 필수적 -> 비효율적(캐시 비효율성도 문제)
         * 그래서 OriginMesh 데이터를 통으로 전달하는 것이 훨씬 빠르게 처리됨
         */
        vertexBuffer.SetData(originMesh.vertices);
        uvBuffer.SetData(originMesh.uv);
        normalBuffer.SetData(originMesh.normals);
        tangentBuffer.SetData(originMesh.tangents);
        triangleBuffer.SetData(originMesh.triangles);
    }

    private void SendComputeBuffer(int subMeshIndex)
    {
        int kernel = meshSplitterCS.FindKernel("CSMain");

        meshSplitterCS.SetBuffer(kernel, "_vertex", vertexBuffer);
        meshSplitterCS.SetBuffer(kernel, "_uv", uvBuffer);
        meshSplitterCS.SetBuffer(kernel, "_normal", normalBuffer);
        meshSplitterCS.SetBuffer(kernel, "_tangents", tangentBuffer);
        meshSplitterCS.SetBuffer(kernel, "_triangle", triangleBuffer);
        meshSplitterCS.SetBuffer(kernel, "_outTriangles", outputBuffer[subMeshIndex]);
        
        UnityEngine.Rendering.SubMeshDescriptor descriptor = originMesh.GetSubMesh(subMeshIndex);
        int triangleCount = originMesh.GetTriangles(subMeshIndex).Length / 3;
        int startIndex = descriptor.indexStart;  // subMesh에서 시작하는 vertex의 Index

        meshSplitterCS.SetInt("_indexStart", startIndex);
        meshSplitterCS.SetInt("_triangleCount", triangleCount);

        meshSplitterCS.GetKernelThreadGroupSizes(kernel, out uint threadsPerGroup, out uint _, out uint _);
        // thread 그룹 당 thread 수는 64.
        // triangle이 300라면 스레드가 64개니까 그룹은 총 5개가 필요
        int threadGroupCount = Mathf.CeilToInt((triangleCount) / (float)threadsPerGroup);

        meshSplitterCS.Dispatch(kernel, threadGroupCount, 1, 1);
    }

    private Mesh GetComputeBuffer(int subMeshIndex)
    {
        int triangleCount = originMesh.GetTriangles(subMeshIndex).Length;
        TriangleData[] triangleData = new TriangleData[triangleCount / 3];
        outputBuffer[subMeshIndex].GetData(triangleData);

        List<Vector3> vertex = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<Vector3> normal = new List<Vector3>();
        List<Vector4> tangent = new List<Vector4>();
        List<int> triangle = new List<int>();
        VertexData compareData = new VertexData();

        for(int i = 0; i < triangleData.Length; i++)
        {
            compareData.position = triangleData[i].vertex0;
            compareData.uv = triangleData[i].uv0;
            compareData.normal = triangleData[i].normal0;
            compareData.tangent = triangleData[i].tangent0;

            if(!vertexDic.TryGetValue(compareData, out int triangleIndex))
            {
                triangleIndex = vertex.Count;
                vertexDic.Add(compareData, triangleIndex);
                vertex.Add(compareData.position);
                uv.Add(compareData.uv);
                normal.Add(compareData.normal);
                tangent.Add(compareData.tangent);
            }

            triangle.Add(triangleIndex);

            compareData.position = triangleData[i].vertex1;
            compareData.uv = triangleData[i].uv1;
            compareData.normal = triangleData[i].normal1;
            compareData.tangent = triangleData[i].tangent1;

            if (!vertexDic.TryGetValue(compareData, out triangleIndex))
            {
                triangleIndex = vertex.Count;
                vertexDic.Add(compareData, triangleIndex);
                vertex.Add(compareData.position);
                uv.Add(compareData.uv);
                normal.Add(compareData.normal);
                tangent.Add(compareData.tangent);
            }

            triangle.Add(triangleIndex);

            compareData.position = triangleData[i].vertex2;
            compareData.uv = triangleData[i].uv2;
            compareData.normal = triangleData[i].normal2;
            compareData.tangent = triangleData[i].tangent2;

            if (!vertexDic.TryGetValue(compareData, out triangleIndex))
            {
                triangleIndex = vertex.Count;
                vertexDic.Add(compareData, triangleIndex);
                vertex.Add(compareData.position);
                uv.Add(compareData.uv);
                normal.Add(compareData.normal);
                tangent.Add(compareData.tangent);
            }

            triangle.Add(triangleIndex);
        }

        GameObject meshObject = new GameObject();
        meshObject.transform.SetParent(centerObj[subMeshIndex].transform);

        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh();

        if (vertex.Count > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.SetVertices(vertex.ToArray());
        mesh.SetUVs(0, uv.ToArray());
        mesh.SetNormals(normal.ToArray());
        mesh.SetTangents(tangent.ToArray());
        mesh.SetTriangles(triangle, 0);

        meshFilter.mesh = mesh;
        meshFilterList.Add(meshFilter);

        return mesh;
    }

    private void ReleaseBuffer(int subMeshIndex)
    {
        vertexBuffer.Release();
        uvBuffer.Release();
        normalBuffer.Release();
        tangentBuffer.Release();
        triangleBuffer.Release();
        outputBuffer[subMeshIndex].Release();

        vertexDic.Clear();
        meshFilterList.Clear();
    }
}