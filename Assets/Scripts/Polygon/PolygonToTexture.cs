using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PolygonToTexture : MonoBehaviour
{
    public MeshRenderer terrainMeshRenderer;

    private List<Vector3> pointList = new List<Vector3>();

    private int ROAD_TEXTURE_WIDTH = 8196;
    private int ROAD_TEXTURE_HEIGHT = 8196;
    private int lineThickness = 10;

    private Bounds terrainBounds;

    private void Start()
    {
        ReadPoint();

        terrainBounds = terrainMeshRenderer.bounds;
        Texture2D newTexture = CreateRoadTexture(pointList);

        List<Vector3> testPoint1 = new List<Vector3>();
        List<Vector3> testPoint2 = new List<Vector3>();

        TestMethod(testPoint1, pointList[0], true);
        TestMethod(testPoint2, pointList[3], false);

        newTexture = CreateRoadTexture(testPoint1, newTexture);
        newTexture = CreateRoadTexture(testPoint2, newTexture);

        newTexture = newTexture.DeCompress();
        byte[] bytes = newTexture.EncodeToPNG();
        File.WriteAllBytes($"{Application.dataPath}/LineTexture.png", bytes);
    }

    private void ReadPoint()
    {
        string filePath = $"{Application.dataPath}/PointComma.txt";

        if (!File.Exists(filePath))
        {
            Debug.Log("Error");
            return;
        }

        string[] lines = File.ReadAllLines(filePath);
        Vector3 tempVector = Vector3.zero;

        foreach (string line in lines)
        {
            string[] values = (new string(line.Where(c => !char.IsWhiteSpace(c)).ToArray())).Split(',');

            if (values.Length == 3)
            {
                if (float.TryParse(values[0], out float x) &&
                    float.TryParse(values[1], out float y) &&
                    float.TryParse(values[2], out float z))
                {
                    tempVector.Set(x, y + 20, z);
                    pointList.Add(tempVector);
                }
            }
        }
    }

    private Texture2D CreateRoadTexture(List<Vector3> boundaryPointList)
    {
        Texture2D roadTexture = new Texture2D(ROAD_TEXTURE_WIDTH, ROAD_TEXTURE_HEIGHT);

        SetTextureBackGroundColor(roadTexture, Color.clear);

        List<Vector2Int> texturePointList = new List<Vector2Int>(boundaryPointList.Count);
        List<Color> roadTextureColorList = new List<Color>(ROAD_TEXTURE_HEIGHT * ROAD_TEXTURE_WIDTH);

        ConvertWorldToTexturePoint(boundaryPointList, texturePointList);
        FillRoadTextureColors(roadTextureColorList, texturePointList);
        DrawRoadLines(roadTextureColorList, texturePointList);

        roadTexture.SetPixels(roadTextureColorList.ToArray());
        roadTexture.Apply();

        return roadTexture;
    }

    private Texture2D CreateRoadTexture(List<Vector3> boundaryPointList, Texture2D roadTexture)
    {
        List<Vector2Int> texturePointList = new List<Vector2Int>();
        List<Color> roadTextureColorList = roadTexture.GetPixels().ToList();

        ConvertWorldToTexturePoint(boundaryPointList, texturePointList);
        FillRoadTextureColors(roadTextureColorList, texturePointList, roadTexture);
        DrawRoadLines(roadTextureColorList, texturePointList);

        roadTexture.SetPixels(roadTextureColorList.ToArray());
        roadTexture.Apply();

        return roadTexture;
    }

    private void ConvertWorldToTexturePoint(List<Vector3> boundaryPointList, List<Vector2Int> texturePointList)
    {
        Vector2Int temp = new Vector2Int(0, 0);
        
        foreach (var worldPoint in boundaryPointList)
        {
            float normalizedX = Mathf.InverseLerp(terrainBounds.min.x, terrainBounds.max.x, worldPoint.x);
            float normalizedZ = Mathf.InverseLerp(terrainBounds.min.z, terrainBounds.max.z, worldPoint.z);

            int texelXPoint = Mathf.FloorToInt(normalizedX * ROAD_TEXTURE_WIDTH);
            int texelYPoint = Mathf.FloorToInt(normalizedZ * ROAD_TEXTURE_HEIGHT);

            temp.Set(texelXPoint, texelYPoint);
            texturePointList.Add(temp);
        }
    }

    private void FillRoadTextureColors(List<Color> roadTextureColorList, List<Vector2Int> texturePointList)
    {
        for (int h = 0; h < ROAD_TEXTURE_HEIGHT; h++)
        {
            GetTextureColor(h, texturePointList, out Color[] textureLineColor);

            roadTextureColorList.AddRange(textureLineColor);
        }
    }

    private void FillRoadTextureColors(List<Color> roadTextureColorList, List<Vector2Int> texturePointList, Texture2D roadTexture)
    {
        for (int h = 0; h < ROAD_TEXTURE_HEIGHT; h++)
        {
            GetTextureColor(h, texturePointList, roadTexture, out Color[] textureLineColor);

            int startIndex = h * ROAD_TEXTURE_WIDTH;
            for (int i = 0; i < ROAD_TEXTURE_WIDTH; i++)
            {
                roadTextureColorList[startIndex + i] = textureLineColor[i];
            }
        }
    }

    private bool GetTextureColor(int y, List<Vector2Int> texturePointList, out Color[] textureLineColor)
    {
        List<float> intersections = new List<float>();
        for (int i = 0; i < texturePointList.Count; i++)
        {
            Vector2Int p1 = texturePointList[i];
            Vector2Int p2 = texturePointList[(i + 1) % texturePointList.Count];
            FindIntersectionPoint(y, intersections, p1, p2);
        }

        textureLineColor = InitColorArray();
        FillScanlineSpans(intersections, textureLineColor, Color.green);

        return true;
    }

    private bool GetTextureColor(int y, List<Vector2Int> texturePointList, Texture2D roadTexture, out Color[] textureLineColor)
    {
        List<float> intersections = new List<float>();
        for (int i = 0; i < texturePointList.Count; i++)
        {
            Vector2Int p1 = texturePointList[i];
            Vector2Int p2 = texturePointList[(i + 1) % texturePointList.Count];
            FindIntersectionPoint(y, intersections, p1, p2);
        }

        textureLineColor = InitColorArray(y, roadTexture);
        FillScanlineSpans(intersections, textureLineColor, Color.green);

        return true;
    }

    private void FindIntersectionPoint(int scanlineY, List<float> intersections, Vector2Int pointA, Vector2Int pointB)
    {
        if (IsEdgeCrossScanline(scanlineY, pointA.y, pointB.y))
        {
            float x = GetIntersectionPoint(scanlineY, pointA, pointB);

            if (!intersections.Contains(x))
            {
                intersections.Add(x);
            }
        }
    }

    private bool IsEdgeCrossScanline(int scanlineY, int edgePointAY, int edgePointBY)
    {
        return (edgePointAY > scanlineY && edgePointBY <= scanlineY) || (edgePointAY <= scanlineY && edgePointBY > scanlineY);
    }

    private float GetIntersectionPoint(int scanlineY, Vector2Int pointA, Vector2Int pointB)
    {
        if (Mathf.Approximately(pointA.y - pointB.y, 0)) return pointA.x;

        return pointA.x + (scanlineY - pointA.y) * (pointB.x - pointA.x) / (pointB.y - pointA.y);
    }

    private void FillScanlineSpans(List<float> intersections, Color[] textureLineColor, Color scanlineColor)
    {
        intersections.Sort();

        for (int i = 0; i < intersections.Count - 1; i = i + 2)
        {
            if (i + 1 < intersections.Count)
            {
                int startPos = Mathf.RoundToInt(intersections[i]);
                int endPos = Mathf.RoundToInt(intersections[i + 1]);

                for (int texelPoint = startPos + 1; texelPoint < endPos; texelPoint++)
                {
                    textureLineColor[texelPoint] = scanlineColor;
                }
            }
        }
    }

    private void DrawRoadLines(List<Color> roadTextureColorList, List<Vector2Int> texturePointList)
    {
        for (int i = 0; i < texturePointList.Count; i++)
        {
            Vector2Int pointA = texturePointList[i];
            Vector2Int pointB = texturePointList[(i + 1) % texturePointList.Count];
            DrawRoadLine(roadTextureColorList, pointA, pointB, Color.red);
        }
    }

    /*
    // 직선 알고리즘 : 픽셀 그래픽에서 n차원 직선이 두 점 사이의 직선에 대한 가장 가까운 근사을 형성하기 위해 선택되어야 하는 점들을 결정하는 선 그리기 알고리즘
    
    // 기본적으로 알고리즘에 대하여 다음과 같은 규칙이 적용.
    // 1. 왼쪽 상단은 0,0 -> 오른쪽 하단으로 갈 수록 좌표값이 증가
    // 2. 픽셀 중심은 정수 좌표를 가짐
    // 3. 선은 두 픽셀 중심의 좌표를 가지며(x1, y1) (x2, y2) 쌍의 첫 번째 좌표는 열, 두 번째 좌표는 행이다.
    // -> 알고리즘의 핵심은 현재 픽셀 위치에서 다음 픽셀로 이동할 때 직선 경로와 가장 가까운 픽셀을 선택하는 것.
    
    // 대표적인 방법이 브레젠험 알고리즘.
    // 정수 덧셈, 비트 시프팅 만으로 구현이 가능하며, 처리 속도가 굉장히 빨라 선 기본 도형을 그리는데 일반적으로 사용
    // 영역을 8등분 영역으로 구분한 후, (1팔분면 ~ 8팔분면) 각 영역별로 그려내는 방식
    
    // 1팔분면(0 ~ 45도)
    // -> 해당 영역에 존재하는 모든 선의 기울기는 1을 넘어설 수 없음    
    // -> 평행하거나 한 칸만 내려감                                   
    
    // 시작점 : (x0, y0), w : 너비, h : 높이(-> 직선의 변화량)
    // 첫 픽셀(x0, y0)를 찍고 다음 픽셀을 어디를 찍을지 결정해야 함.
    // 다음 좌표의 대상은 
    // -> 1팔분면은 평행 or 한 칸내려감 -> y0 or y0 + 1만 가능
    // -> 중간값인 (x0 + 1, y0 + 0.5) -> x0 + 1에서 그릴 선이 y0 + 0.5위 or 아래만 판단하면 다음 픽셀을 선별함
    // 직선의 방정식을 y = ax + b라고 가정하면, a : h / w이며, (x0, y0)를 지남
    // 그대로 이용해도 좋으나, 픽셀 연산에서 소수점 연산은 매우 느린 연산 -> 정수형 연산으로의 변화
    */
    private void DrawRoadLine(List<Color> roadTextureColorList, Vector2Int pointA, Vector2Int pointB, Color lineColor)
    {
        int x0 = pointA.x;
        int y0 = pointA.y;
        int x1 = pointB.x;
        int y1 = pointB.y;

        // 기울기를 위한 변화량
        int dx = Mathf.Abs(x1 - x0);
        int signX = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int signY = y0 < y1 ? 1 : -1;

        // 비용 체크
        int err = dx + dy;
        int radius = lineThickness / 2;

        while (true)
        {
            DrawCircle(roadTextureColorList, x0, y0, radius, lineColor);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;

            if (e2 >= dy)
            {
                err += dy;
                x0 += signX;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += signY;
            }
        }
    }

    private void DrawCircle(List<Color> colorList, int centerX, int centerY, int radius, Color lineColor)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int drawX = centerX + x;
                    int drawY = centerY + y;
                    float nomalizeValue = (radius * radius) - (x * x + y * y);
                    nomalizeValue /= (radius * radius);
                    nomalizeValue = Mathf.Pow(nomalizeValue, 2);

                    if (drawX >= 0 && drawX < ROAD_TEXTURE_WIDTH && drawY >= 0 && drawY < ROAD_TEXTURE_HEIGHT)
                    {
                        colorList[drawY * ROAD_TEXTURE_HEIGHT + drawX] += (lineColor * nomalizeValue);
                    }
                }
            }
        }
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

    private Color[] InitColorArray()
    {
        Color[] newArray = new Color[ROAD_TEXTURE_WIDTH];

        for (int i = 0; i < newArray.Length; i++)
        {
            newArray[i] = Color.clear;
        }

        return newArray;
    }

    private Color[] InitColorArray(int y, Texture2D roadTexture)
    {
        return roadTexture.GetPixels(0, y, ROAD_TEXTURE_WIDTH, 1);
    }

    private void TestMethod(List<Vector3> pointList, Vector3 startPoint, bool flag)
    {
        float minX = flag ? startPoint.x + 80f : startPoint.x - 80f;
        float maxX = flag ? startPoint.x + 180f : startPoint.x - 180f;

        float minZ = flag ? startPoint.z + 80f : startPoint.z - 80f;
        float maxZ = flag ? startPoint.z + 180f : startPoint.z - 180f;

        pointList.Add(new Vector3(minX, 0f, minZ));
        pointList.Add(new Vector3(maxX, 0f, minZ));
        pointList.Add(new Vector3(maxX, 0f, maxZ));
        pointList.Add(new Vector3(minX, 0f, maxZ));
        pointList.Add(new Vector3(minX, 0f, minZ));
    }
}
