using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class ScanlineFill : MonoBehaviour
{
    public List<Vector3> positions;                           // 정점의 위치
    public List<Transform> vertices = new List<Transform>();  // 다각형의 꼭짓점들
    public GameObject vertexPrefab;                           // 꼭짓점으로 사용할 프리팹

    public GameObject startVertex;
    public GameObject endVertex;

    public Material lineMaterial;                             // 선을 그릴 머티리얼
    public Color fillColor = Color.red;
    public List<float> intersections;
    private LineRenderer lineRenderer;
    private List<GameObject> fillObjects = new List<GameObject>();

    void Start()
    {
        // 라인 렌더러 초기화
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = lineMaterial;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;

        // 초기 정점 생성
        CreateInitialVertices();

        // 정점들을 연결하는 선 그리기
        UpdateLineRenderer();

        intersections = new List<float>();
    }

    void CreateInitialVertices()
    {
        foreach (Vector3 pos in positions)
        {
            GameObject vertex = Instantiate(vertexPrefab, pos, Quaternion.identity);
            vertices.Add(vertex.transform);
        }
    }

    void Update()
    {
        // Space 키를 누르면 폴리곤 채우기 실행
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ClearFill();
            FillPolygon();
            //StartCoroutine(FillPolygon());
        }
    }

    void UpdateLineRenderer()
    {
        lineRenderer.positionCount = vertices.Count + 1;
        for (int i = 0; i <= vertices.Count; i++)
        {
            lineRenderer.SetPosition(i, vertices[i % vertices.Count].position);
        }
    }

    void ClearFill()
    {
        foreach (GameObject obj in fillObjects)
        {
            Destroy(obj);
        }
        fillObjects.Clear();
    }

    void FillPolygon()
    {
        // 정점의 수가 3개라면 도형을 만들 수 없음
        if (vertices.Count < 3) return;

        // 1. 최소, 최대 Y값 찾기
        float minY = vertices[0].position.y;
        float maxY = vertices[0].position.y;

        foreach (Transform vertex in vertices)
        {
            minY = Mathf.Min(minY, vertex.position.y);
            maxY = Mathf.Max(maxY, vertex.position.y);
        }

        // 2. 모든 Edge에 대한 교차점 검증 후 그리기

        // 스캔라인 처리 -> 최소값부터 최대값까지
        for (float y = minY; y <= maxY; y += 0.1f)  // 간격 조절 가능
        {
            intersections = new List<float>();

            // [0] : (0, 0) / [1] : (10, 0) / [2] : (10, 10) / [3] : (5, 5) / [4] : (0, 10) -> 5개의 정점
            // y : 현재 스캔하는 위치 / p1 : 현재 정점 / p2 : 현재 정점과 연결된 다음 정점
            // 최소 y부터 최대 y까지 전부 채워야 함
            // 평평하다면 좋겠으나 꼭지점처럼 채워야 하는 부분은 정점의 위치를 찾기 위해서 직선의 방정식을 활용   

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p1 = vertices[i].position;
                Vector3 p2 = vertices[(i + 1) % vertices.Count].position;

                // p1 - p2 연결선
                // 두 연결선의 y값을 비교 
                // 두 연결선의 y값이 같은 경우는 scanline의 대상이 아님 -> 폴리곤을 채울 이유가 없음 ( _________ )
                // \(p2.x, p2.y)
                //  \
                //   \  ____________ : 두 점(p1, p2)를 계산하여 직선의 방정식을 구할 수 있음
                //    \              -> 즉 y값을 알 때 x값을 알 수 있으며 교차점의 지점을 알 수 있음
                //     \             -> (p1.y - p2.y)x + (p2.x - p1.x)y + p1.x*p2.y - p2.x*p1.y = 0 : 직선의 방정식
                //      \(p1.x, p1.y)
                if ((p1.y >= y && p2.y <= y) || (p1.y <= y && p2.y >= y))
                {
                    if (Mathf.Approximately(p1.y, p2.y)) continue; // 거의 유사한 경우에도 그리지 않음

                    // 직선이 기울어져 있다면 y값에 따라 x값이 변경되며 이는 두 정점을 알고 있으므로 직선으로 표현하여 계산할 수 있음
                    // 기울기의 역수 -> 즉 직선에서 y값이 1증가할 땐, x값은 기울기의 역수로 증가
                    // ex) y = 3x -> y값이 1 증가하면 x는 1/3 증가(1,3) -> (1 + 1/3 ,4)
                    float x = p1.x + (y - p1.y) * (p2.x - p1.x) / (p2.y - p1.y);
                    intersections.Add(x);
                }
            }

            // 교차점 -> 정점이 2개있는 지점은 그 지점을 이으면 됨.
            // 다만 그게 아닌 중간에 점이 있거나, 공간이 있는 경우에?
            // |\    /|
            // | \  / |  -> 여기 부분은 가운데를 비우고, 각각 2개의 점 사이만 채워야 함
            // |  \/  |
            // 위 작업은 저런 점들을 교차점으로 판단하고 그 지점의 좌표값을 활용하여 채우는 방식
            intersections.Sort();

            for (int i = 0; i < intersections.Count - 1; i += 2)
            {
                if (i + 1 >= intersections.Count) break;

                float startX = intersections[i];
                float endX = intersections[i + 1];

                // Instantiate(startVertex, new Vector3(startX, y, 0), Quaternion.identity);
                // Instantiate(endVertex, new Vector3(endX, y, 0), Quaternion.identity);

                // 라인 채우기 -> 시작,끝점 중간에 오브젝트 설치 후 늘리기
                GameObject fillLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fillLine.transform.position = new Vector3((startX + endX) / 2, y, 0);
                fillLine.transform.localScale = new Vector3(Mathf.Abs(endX - startX), 0.1f, 0.1f);
                fillLine.GetComponent<Renderer>().material.color = fillColor;
                fillObjects.Add(fillLine);
            }
            // 위 작업을 miny -> maxy될 때 까지 반복함으로써 전부 채워나감
        }
    }
}