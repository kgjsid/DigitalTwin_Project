using System.Collections.Generic;
using UnityEngine;

public class HighlightPolygon : MonoBehaviour
{
    MeshRenderer meshRenderer;
    MeshFilter meshFilter;

    private List<Vector4> pointVector = new List<Vector4>();

    private void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();

        pointVector = new List<Vector4>();
    }

    private void Update()
    {
        pointVector.Clear();

        pointVector.Add(new Vector4(5, 0, 5, 0));
        pointVector.Add(new Vector4(-5, 0, 5, 0));
        pointVector.Add(new Vector4(1, 0, 2, 0));

        CheckMousePoint();

        pointVector.Add(new Vector4(5, 0, 5, 0));
        
        meshRenderer.material.SetInt("_VertexCount", pointVector.Count);
        meshRenderer.material.SetVectorArray("_PointArray", pointVector.ToArray());
    }

    private void CheckMousePoint()
    {
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit raycastHit, 10000f))
        {
            pointVector.Add(new Vector4(raycastHit.point.x, raycastHit.point.y, raycastHit.point.z, 0f));
        }
    }

}
