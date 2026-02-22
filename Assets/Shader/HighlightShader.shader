Shader "Custom/HighlightShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PolygonColor("Polygon Color", Color) = (1, 1, 1, 1)
        _LineColor("Line Color", Color) = (1, 1, 1, 1)
        _LineWidth("Line Width", float) = 0.2
        
        _VertexCount("Vertex Count", Int) = 0
        _CrossHatchSpeed("Cross Hatch Speed", Float) = 1.0
        _CrossHatchWidth("Cross Hatch Width", Float) = 10.0
        [Toggle] _EnableCrosshatchEffect("Enable Crosshatch", Float) = 1
        _ShaderDistanceCutoff("Distance Cutoff", Float) = 50.0
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        
        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _PolygonColor;
                half4 _LineColor;
                float _LineWidth;
                
                // 추가된 파라미터들
                float4 _PointArray[32]; 
                int _VertexCount;
                half _CrossHatchSpeed;
                half _CrossHatchWidth;
                float _EnableCrosshatchEffect;
                float _ShaderDistanceCutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            float DistanceToLineSegment(float2 p, float2 A, float2 B)
            {
                float2 AB = B - A;
                float2 AP = p - A;
                float denom = dot(AB, AB);
                float t = saturate(dot(AP, AB) * (denom > 1e-6 ? rcp(denom) : 0));
                float2 closest = A + t * AB;
                return distance(p, closest);
            }

            half4 GetHighlightColor(float3 positionOS, float3 positionWS, half4 baseColor)
            {
                float2 p = positionOS.xz;
                float minDist = 10000.0;
                uint crossingCount = 0;
                float cameraDist = distance(_WorldSpaceCameraPos, positionWS);
                if (cameraDist > _ShaderDistanceCutoff) return baseColor;

                for (int i = 0; i < _VertexCount; i++)
                {
                    float2 A = _PointArray[i].xz;
                    float2 B = _PointArray[(i + 1) % _VertexCount].xz;

                    float dist = DistanceToLineSegment(p, A, B);
                    minDist = min(dist, minDist);

                    if (((A.y > p.y) != (B.y > p.y)) && (p.x < (B.x - A.x) * (p.y - A.y) / (B.y - A.y + 1e-6) + A.x))
                    {
                        crossingCount++;
                    }
                }

                bool pointInPolygon = (crossingCount % 2 == 1);
                bool pointOnLine = (minDist < _LineWidth);
                
                float timeValue = frac(_Time.y * _CrossHatchSpeed);
                float hatch = step(0.5, _EnableCrosshatchEffect > 0.5 ? frac(positionOS.z * _CrossHatchWidth + timeValue) : 1.0);

                if (pointOnLine)
                    return _LineColor;
                else if (pointInPolygon)
                    return lerp(baseColor, _PolygonColor, hatch);
                
                return baseColor;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.positionOS = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                half4 finalColor = GetHighlightColor(input.positionOS, input.positionWS, texColor);

                return finalColor;
            }

            ENDHLSL
        }
    }
}