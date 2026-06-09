Shader "Custom/SpriteOutline_URP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [Range(0, 8)] _OutlineWidth ("Outline Width (px)", Float) = 2
        [Range(0, 1)] _AlphaThreshold ("Alpha Threshold", Range(0, 1)) = 0.05

        [Header(Rainbow Outline)]
        _RainbowSpeed ("Flow Speed", Float) = 1.2
        _RainbowScale ("Color Cycles", Float) = 1.5
        [Range(0, 1)] _RainbowSaturation ("Saturation", Range(0, 1)) = 1
        [HDR] _RainbowBrightness ("Brightness", Color) = (2, 2, 2, 1)
        _ShimmerSpeed ("Shimmer Speed", Float) = 3
        [Range(0, 1)] _ShimmerStrength ("Shimmer Strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "SpriteOutline"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _OutlineWidth;
                half _AlphaThreshold;
                float _RainbowSpeed;
                float _RainbowScale;
                half _RainbowSaturation;
                half4 _RainbowBrightness;
                float _ShimmerSpeed;
                half _ShimmerStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half4 color        : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half SampleSpriteAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half3 HsvToRgb(half h, half s, half v)
            {
                half hue = frac(h);
                half r = abs(hue * 6.0h - 3.0h) - 1.0h;
                half g = 2.0h - abs(hue * 6.0h - 2.0h);
                half b = 2.0h - abs(hue * 6.0h - 4.0h);
                half3 rgb = saturate(half3(r, g, b));
                return ((rgb - 1.0h) * s + 1.0h) * v;
            }

            half3 GetRainbowColor(float2 uv, float time)
            {
                float2 centered = uv - 0.5;
                float angleHue = atan2(centered.y, centered.x) * (0.15915494); // 1 / (2*pi)
                float flowHue = (uv.x - uv.y) * _RainbowScale;
                float hue = frac(angleHue + flowHue + time * _RainbowSpeed);

                half shimmer = 1.0h - _ShimmerStrength
                    + _ShimmerStrength * (0.5h + 0.5h * sin(time * _ShimmerSpeed + hue * 6.2831853h * 2.0h));
                half3 rgb = HsvToRgb(hue, _RainbowSaturation, shimmer);
                return rgb * _RainbowBrightness.rgb;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                if (sprite.a > _AlphaThreshold)
                    return sprite;

                float2 texel = _MainTex_TexelSize.xy;
                half neighborAlpha = 0;
                int width = (int)clamp(round(_OutlineWidth), 0, 8);

                [loop]
                for (int i = 1; i <= width; i++)
                {
                    float2 offset = texel * (float)i;
                    neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.uv + float2(offset.x, 0)));
                    neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.uv + float2(-offset.x, 0)));
                    neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.uv + float2(0, offset.y)));
                    neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.uv + float2(0, -offset.y)));
                    neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.uv + float2(offset.x, offset.y)));
                    neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.uv + float2(offset.x, -offset.y)));
                    neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.uv + float2(-offset.x, offset.y)));
                    neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.uv + float2(-offset.x, -offset.y)));
                }

                if (neighborAlpha > _AlphaThreshold)
                {
                    float time = _Time.y;
                    half3 rainbow = GetRainbowColor(input.uv, time);
                    half alpha = neighborAlpha * input.color.a * _RainbowBrightness.a;
                    return half4(rainbow, alpha);
                }

                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
