#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

float4 _PointArray[600];
half4 _PolygonColor;
half4 _LineColor;
float _LineWidth;
int _VertexCount;
half _CrossHatchSpeed;
half _CrossHatchWidth;
bool _EnableCrosshatchEffect;
float _ShaderDistanceCutoff;

float DistanceToLineSegment(float2 p, float2 A, float2 B)
{
    float2 AB = B - A;
    float2 AP = p - A;
    float denom = dot(AB, AB);
    float t = saturate(dot(AP, AB) * rcp(denom));
    float2 closest = A + t * AB;
    return distance(p, closest);
}

float GetPolygonFactor(float3 positionOS)
{
    bool pointInPolygon = false;
    uint crossingCount = 0;

    float2 p = positionOS.xz;

    for (uint i = 0; i < _VertexCount; i++)
    {
        float2 A = (_PointArray[i].xz);
        float2 B = _PointArray[(i + 1) % _VertexCount].xz;

        if ((A.y > p.y && B.y <= p.y) || (B.y > p.y && A.y <= p.y) && (B.y - A.y) > 1e-6)
        {
            float xIntersect = A.x + (p.y - A.y) * (B.x - A.x) * rcp(B.y - A.y);

            crossingCount += xIntersect > p.x ? 1 : 0;
        }
    }

    return crossingCount % 2 == 1 ? 1 : 0;
}

float GetLineFactor(float3 positionOS)
{
    bool pointOnLine = false;

    float minDist = 10000;

    float2 p = positionOS.xz;

    for (uint i = 0; i < _VertexCount; i++)
    {
        float2 A = (_PointArray[i].xz);
        float2 B = _PointArray[(i + 1) % _VertexCount].xz;

        float dist = DistanceToLineSegment(p, A, B);
        minDist = min(dist, minDist);

        if (minDist < _LineWidth - 0.3)
        {
            break;
        }
    }
    return minDist - 0.3;
}

half4 GetHighlightColor(float3 positionOS, float lineFactor, float polygonFactor, half4 baseColor)
{
    float timeValue = frac(_Time.y * _CrossHatchSpeed);
    float colorValue = step(0.5, _EnableCrosshatchEffect ? frac(positionOS.z * _CrossHatchWidth + timeValue) : 1);

    baseColor.rgba = lineFactor < _LineWidth ? _LineColor.rgba : baseColor.rgba;
    baseColor.rgba += !(lineFactor < _LineWidth) && polygonFactor > 0.9 ? (_PolygonColor.rgba * colorValue) : 0;

    return baseColor;
}

half4 GetHighlightColor(float3 positionOS, float cameraFactor, half4 baseColor)
{
    bool pointInPolygon = false;
    bool pointOnLine = false;
    
    float minDist = 10000;
    uint crossingCount = 0;
    
    float2 p = positionOS.xz;

    if (cameraFactor > _ShaderDistanceCutoff)
        return baseColor;

    for (uint i = 0; i < _VertexCount; i++)
    {
        float2 A = (_PointArray[i].xz);
        float2 B = _PointArray[(i + 1) % _VertexCount].xz;
       
        float dist = DistanceToLineSegment(p, A, B);
        minDist = min(dist, minDist);

        if (minDist < _LineWidth)
            break;

        if ((A.y > p.y && B.y <= p.y) || (B.y > p.y && A.y <= p.y) && (B.y - A.y) > 1e-6)
        {
            float xIntersect = A.x + (p.y - A.y) * (B.x - A.x) * rcp(B.y - A.y);

            crossingCount += xIntersect > p.x ? 1 : 0;
        }
    }

    pointInPolygon = crossingCount % 2 == 1;
    
    pointOnLine = minDist < _LineWidth;
    
    float timeValue = frac(_Time.y * _CrossHatchSpeed);
    float colorValue = step(0.5, _EnableCrosshatchEffect ? frac(positionOS.z * _CrossHatchWidth + timeValue) : 1);
    baseColor.rgba = pointOnLine ? _LineColor.rgba : baseColor.rgba;
    baseColor.rgba += !pointOnLine && pointInPolygon ? (_PolygonColor.rgba * colorValue) : 0;

    return baseColor;
}
