Shader "Custom/LineRenderer_Emissive_URP"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _BaseMap("Base Map", 2D) = "white" {}
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width", Range(0, 0.5)) = 0.12
        _OutlineSoftness("Outline Softness", Range(0.0001, 0.2)) = 0.02
        [HDR]_EmissionColor("Emission Color", Color) = (1,1,1,1)
        _EmissionStrength("Emission Strength", Range(0, 20)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _OutlineSoftness;
                float4 _EmissionColor;
                float  _EmissionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;      // LineRenderer 顶点色/渐变
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 baseCol = tex * _BaseColor * IN.color;

                half3 emissive = (_EmissionColor.rgb * _EmissionStrength) * baseCol.rgb;

                // LineRenderer 横向宽度通常在 UV.y：0..1（两侧边缘）
                half edgeDist = min(IN.uv.y, 1.0h - IN.uv.y); // 到最近边缘的距离（0=边缘）
                half outlineMask = 1.0h - smoothstep(_OutlineWidth, _OutlineWidth + _OutlineSoftness, edgeDist);

                half3 basePlusEmissive = baseCol.rgb + emissive;
                half3 rgb = lerp(basePlusEmissive, _OutlineColor.rgb, outlineMask * _OutlineColor.a);

                return half4(rgb, baseCol.a);
            }
            ENDHLSL
        }
    }
}
