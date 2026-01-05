using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshUVConverter
{
    // 원본 Mesh UV -> 변환된 텍스쳐의 UV값으로 변환 필요
    public Mesh mesh;
    public MeshFilter meshFilter;

    public Vector2[] originUV;
    public Vector2[] newUV;

    public float widthValue;
    public float heightValue;

    public MeshUVConverter(Mesh originMesh)
    {
        mesh = originMesh;
    }

    public void ConvertUV(Vector2Int originTextureSize, Vector2Int newTextureSize, int startXIndex, int startYIndex, out Mesh newMesh)
    {
        // 원본 uv값 -> 원본 텍스쳐에서의 위치
        // 새로 uv값을 만들어야 함. -> 변형된 텍스쳐에서 매칭될 수 있도록
        originUV = mesh.uv;
        newUV = new Vector2[originUV.Length];

        float widthConvertValue = 1f / newTextureSize.x;
        float heightConvertValue = 1f / newTextureSize.y;

        int originPixelXCoord = 0;
        int originPixelYCoord = 0;
        Vector2 newUVPoint = Vector2.zero;

        // 지금 텍스쳐를 압축할 때, (8192x8192 -> 512x64)
        // mesh에 해당하는 uv 영역의 좌표를 구하고, 거기 내부의 픽셀을 전부 찾아서
        // 새로운 텍스쳐의 원점으로 평행이동 시킨 것.
        // uv영역을 역으로 찾아내려면,
        // 변경된 픽셀의 위치를 구하고 그 위치에서의 uv를 새로 계산해서 넣어주는 작업이 필요

        // 그러면 평행이동 시킬 때의 첫 원점의 좌표가 필수
        // 변환된 픽셀 위치는
        // (origin.x, origin.y) - (trans.x, trans.y) => (new.x, new.y) 이니까.

        for(int i = 0; i < originUV.Length; i++)
        {
            // 1. 원본의 픽셀 위치 찾기
            originPixelXCoord = Mathf.RoundToInt(originTextureSize.x * originUV[i].x);
            originPixelYCoord = Mathf.RoundToInt(originTextureSize.y * originUV[i].y);

            // 2. 변환된 픽셀 위치 찾아오기
            newUVPoint.x = originPixelXCoord - startXIndex;      // startX
            newUVPoint.y = originPixelYCoord - startYIndex;      // startY

            // 이를 통해서 새로운 텍스쳐의 픽셀 위치에서 UV값을 새로 계산
            newUV[i].x = newUVPoint.x * widthConvertValue;
            newUV[i].y = newUVPoint.y * heightConvertValue;
        }

        newMesh = new Mesh();
        if (newUV.Length > 65535)
        {
            newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        newMesh.vertices = mesh.vertices;
        newMesh.normals = mesh.normals;
        newMesh.tangents = mesh.tangents;
        newMesh.uv = newUV;
        newMesh.triangles = mesh.triangles;
    }
}
