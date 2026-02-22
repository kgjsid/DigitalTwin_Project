Shader "Custom/SimpleBlueNoiseDither"
{
    Properties
    {
        _Color ("Main Color (Alpha for Dither)", Color) = (1, 1, 1, 1)
        _BlueNoiseTex ("Blue Noise (256x256)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            float4 _Color;
            sampler2D _BlueNoiseTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 화면 좌표 추출 및 픽셀 좌표 변환
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float2 pixelPos = screenUV * _ScreenParams.xy;

                // 2. 블루노이즈 샘플링 (256x256 타일링)
                float noise = tex2D(_BlueNoiseTex, pixelPos / 256.0).r;

                // 3. 디더링 판정: 알파값이 노이즈보다 작으면 해당 픽셀 삭제
                clip(_Color.a - noise);

                return _Color;
            }
            ENDCG
        }
    }
}