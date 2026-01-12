using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FloodFill : MonoBehaviour
{
    public GameObject cubePrefab;                                                       // 채울 오브젝트
    public float gridSize = 1f;                                                         // 그리드 사이즈(큐브 사이즈)
    public Color fillColor = Color.blue;                                                // 채울 색

    private bool[,] visited;                                                            // 방문 여부
    private List<GameObject> fillObjects = new List<GameObject>();                      // 채운 오브젝트
    [SerializeField] private List<Transform> boundaryVertices = new List<Transform>();  // 경계 위치

    private int gridWidth = 20;
    private int gridHeight = 20;

    void Start()
    {
        visited = new bool[gridWidth, gridHeight];

        if (boundaryVertices != null && boundaryVertices.Count > 0)
        {
            for (int i = 0; i < boundaryVertices.Count; i++)
            {
                GameObject cube = Instantiate(cubePrefab, boundaryVertices[i].position, Quaternion.identity);
                cube.transform.localScale = new Vector3(gridSize, gridSize, gridSize) * 0.9f;
                cube.GetComponent<Renderer>().material.color = Color.red;
            }
        }

        AddBoundrayEdge();
    }

    /// <summary>
    /// 정점 추가용 메소드
    /// </summary>
    /// <param name="vertex"></param>
    public void AddBoundaryVertex(Transform vertex)
    {
        boundaryVertices.Add(vertex);
    }

    /// <summary>
    /// 경계선 추가용 메소드
    /// </summary>
    void AddBoundrayEdge()
    {
        LineRenderer line;
        if (!gameObject.TryGetComponent(out line))
        {
            line = gameObject.AddComponent<LineRenderer>();
        }

        line.startWidth = 0.2f;
        line.endWidth = 0.2f;
        line.startColor = Color.black;
        line.endColor = Color.black;

        line.positionCount = boundaryVertices.Count + 1;

        for (int i = 0; i < boundaryVertices.Count; i++)
        {
            line.SetPosition(i, boundaryVertices[i].position);
        }

        line.SetPosition(boundaryVertices.Count, boundaryVertices[0].position);
    }

    void ClearFill()
    {
        foreach (GameObject obj in fillObjects)
        {
            Destroy(obj);
        }
        fillObjects.Clear();
        visited = new bool[gridWidth, gridHeight];
    }

    /// <summary>
    /// 시작점(startPoint)에서 시작해서 채우기 위한 메소드
    /// </summary>
    /// <param name="startPoint"></param>
    public IEnumerator Fill(Vector3 startPoint)
    {
        // 면적을 채워야 하니, 정점의 수가 3개가 안되는 경우 진행하지 않음
        if (boundaryVertices.Count < 3) yield break;

        // 현재 채워진 모든 오브젝트 제거
        ClearFill();

        // 시작점을 그리드 좌표로 변환
        int startX = Mathf.RoundToInt(startPoint.x / gridSize) + gridWidth / 2;
        int startY = Mathf.RoundToInt(startPoint.y / gridSize) + gridHeight / 2;

        if (!IsInsidePolygon(startPoint)) yield break;

        // 해당 점을 시작으로 탐색 시작.
        // 좌표를 넣기 위한 큐
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        // 시작점은 방문할 예정
        visited[startX, startY] = true;

        // 4방향 이동을 위한 배열
        // 0 : 왼쪽
        // 1 : 오른쪽
        // 2 : 아래쪽
        // 3 : 위쪽
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        while (queue.Count > 0)
        {
            // 시작점(상, 하, 좌, 우 탐색 예정)
            Vector2Int current = queue.Dequeue();

            // 현재 위치 측정
            Vector3 worldPos = new Vector3(
                (current.x - gridWidth / 2) * gridSize,
                (current.y - gridHeight / 2) * gridSize,
                0
            );

            if (IsInsidePolygon(worldPos))
            {
                // 면적 안의 점이라면 큐브 생성
                GameObject cube = Instantiate(cubePrefab, worldPos, Quaternion.identity);
                cube.transform.localScale = new Vector3(gridSize, gridSize, gridSize) * 0.9f;
                cube.GetComponent<Renderer>().material.color = fillColor;
                fillObjects.Add(cube);

                yield return new WaitForSeconds(0.1f);

                // 4방향 탐색
                for (int i = 0; i < 4; i++)
                {
                    int nextX = current.x + dx[i];
                    int nextY = current.y + dy[i];

                    // 그리드 범위 체크
                    if (nextX >= 0 && nextX < gridWidth && nextY >= 0 && nextY < gridHeight)
                    {
                        if (!visited[nextX, nextY])
                        {
                            // 방문할 예정이므로 true
                            visited[nextX, nextY] = true;
                            queue.Enqueue(new Vector2Int(nextX, nextY));
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 특정 점(point)가 면적 내부에 있는지 확인하기 위한 메소드
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    bool IsInsidePolygon(Vector3 point)
    {
        // 모든 점을 확인하며 교차점을 확인함.
        // 특정 점이 면적 내부에 있다면, 해당 점을 기준으로 한 방향으로 레이를 실행(반직선 레이)
        // 레이랑 면적 경계의 교차점이 홀수 개 -> 특정 점이 면적 내부에 있음.

        int intersections = 0;
        for (int i = 0; i < boundaryVertices.Count; i++)
        {
            Vector3 vert1 = boundaryVertices[i].position;
            Vector3 vert2 = boundaryVertices[(i + 1) % boundaryVertices.Count].position;

            // 교차점을 확인하기 위해서 x축으로 평행하게 반 직선 레이를 쏜다고 가정. 그렇다면 맞을 수 있는 경우는

            // __________________    
            // \                /    -> 이 경우는 레이를 계산할 필요가 없음.
            //  \              /
            //   \    ㅇ ---> /      -> 1. 이 모양과 같이, 특정 점의 y값이 측정할 양 끝점의 사이에 있어야 함
            //    \          / 
            //     ㅡㅡㅡㅡㅡㅡ       -> 이 경우는 레이를 계산할 필요가 없음.

            // 또한, 한 방향으로 쏘는 반직선의 형태
            // 한 변(두 점을 이용하여 직선의 방정식을 구할 수 있음)에서 확인하면,
            // 2. 동일한 y값에서 x좌표가 특정 점보다 더 커야함.

            //  \    ㅇ ---> /
            //  x           o
            if (((vert1.y > point.y && vert2.y <= point.y) || (vert1.y <= point.y && vert2.y > point.y)) &&     // => 1. 조건(y값이 양 끝점 사이에 있음) 
                (point.x < (vert2.x - vert1.x) * (point.y - vert1.y) / (vert2.y - vert1.y) + vert1.x))          // => 2. 조건(동일한 y값에서 직선 위의 점의 x값이 특정 점의 x값보다 큰 것)
            {
                intersections++;
            }
        }
        return (intersections % 2) == 1;
    }

    void Update()
    {
        // 마우스 클릭으로 채우기 시작
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 temp = Input.mousePosition;
            temp.z -= Camera.main.transform.position.z;

            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            Debug.Log(mousePos);

            StartCoroutine(Fill(mousePos));
        }
    }
}