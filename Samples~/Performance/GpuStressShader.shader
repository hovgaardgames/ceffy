Shader "Hidden/GpuStress"
{
    Properties
    {
        _Iterations ("Iterations", Float) = 100
        _Color ("Color", Color) = (1,0,0,0.1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            float _Iterations;
            float4 _Color;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            // Heavy computation per pixel
            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float result = 0;
                
                // Heavy trigonometric and math operations
                for (int iter = 0; iter < (int)_Iterations; iter++)
                {
                    float angle = iter * 0.1 + _Time.y;
                    result += sin(uv.x * angle) * cos(uv.y * angle);
                    result += tan(atan(result * 0.01)) * 0.01;
                    result += sqrt(abs(sin(result + uv.x * uv.y * iter)));
                    result += pow(abs(sin(angle * uv.x)), 2.0) * pow(abs(cos(angle * uv.y)), 2.0);
                    
                    // Matrix-like operations
                    float2x2 rot = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
                    uv = mul(rot, uv - 0.5) + 0.5;
                    
                    // More heavy math
                    result += log(abs(result) + 1.0) * exp(-abs(result) * 0.001);
                }
                
                result = frac(result * 0.001);
                return float4(_Color.rgb * result + _Color.rgb, _Color.a);
            }
            ENDCG
        }
    }
}
