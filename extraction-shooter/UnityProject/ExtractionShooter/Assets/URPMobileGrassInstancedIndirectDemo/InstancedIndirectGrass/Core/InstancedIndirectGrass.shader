Shader "MobileDrawMeshInstancedIndirect/SingleGrass_Unlit_MultiVar"
{
    Properties
    {
        [Header(Colors)]
        _ColorA("Top Color A", Color) = (0.4, 0.8, 0.4, 1)
        _ColorB("Top Color B", Color) = (0.7, 0.7, 0.2, 1)
        _GroundColor("Ground Color", Color) = (0.1, 0.2, 0.1, 1)

        [Header(Color Mix Noise)]
        _MixNoiseScale("A/B Mix Scale", Float) = 10.0
        _MixNoiseOffset("A/B Mix Offset", Vector) = (0,0,0,0)

        [Header(Global Brightness Variation)]
        _GlobalVarScale("Global Var Scale", Float) = 0.5
        _GlobalVarStrength("Global Var Strength", Range(0, 1)) = 0.3

        [Header(Main Settings)]
        [Toggle(USE_BILLBOARD)] _UseBillboard("Use Billboard", Float) = 1
        [NoScaleOffset] _GrassTexture("Grass Texture", 2D) = "white" {}
        _TextureTiling("Texture Tiling", Vector) = (1,1,0,0)

        [Header(Grass Shape)]
        _GrassWidth("Grass Width", Float) = 1
        _GrassHeight("Grass Height", Float) = 1
        _TextureCutoff("Texture Alpha Cutoff", Range(0, 1)) = 0.1

        [Header(Noise Shape Control)]
        _HeightNoiseScale("Height Noise Scale", Float) = 0.3
        _HeightNoiseStrength("Height Noise Strength", Range(0, 1)) = 0.5
        _WidthNoiseScale("Width Noise Scale", Float) = 0.3
        _WidthNoiseStrength("Width Noise Strength", Range(0, 1)) = 0.5

        [Header(Bend Elasticity)]
        _Elasticity("Bend Elasticity", Float) = 0.3
        _BendSpeed("Bend Return Speed", Float) = 4.0

        [Header(Wind)]
        _WindAIntensity("Wind A Intensity", Float) = 1.77
        _WindAFrequency("Wind A Frequency", Float) = 4
        _WindATiling("Wind A Tiling", Vector) = (0.1,0.1,0,0)
        _WindAWrap("Wind A Wrap", Vector) = (0.5,0.5,0,0)

        _WindBIntensity("Wind B Intensity", Float) = 0.25
        _WindBFrequency("Wind B Frequency", Float) = 7.7
        _WindBTiling("Wind B Tiling", Vector) = (0.37,3,0,0)
        _WindBWrap("Wind B Wrap", Vector) = (0.5,0.5,0,0)

        _WindCIntensity("Wind C Intensity", Float) = 0.125
        _WindCFrequency("Wind C Frequency", Float) = 11.7
        _WindCTiling("Wind C Tiling", Vector) = (0.77,3,0,0)
        _WindCWrap("Wind C Wrap", Vector) = (0.5,0.5,0,0)

        [HideInInspector]_PivotPosWS("Pivot Pos WS", Vector) = (0,0,0,0)
        [HideInInspector]_BoundSize("Bound Size", Vector) = (1,1,0,0)
    }

    SubShader
    {
        Tags { "RenderType" = " Transparent" "RenderPipeline" = "UniversalRenderPipeline" "IgnoreProjector" = "True"}

        Pass
        {
            Cull Off 
            ZTest Less
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local USE_BILLBOARD
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 texcoord     : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half3 color        : COLOR;
                half  fogFactor    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float3 _PivotPosWS;
                float2 _BoundSize;

                float _GrassWidth;
                float _GrassHeight;
                float _TextureCutoff;

                float _HeightNoiseScale;
                float _HeightNoiseStrength;
                float _WidthNoiseScale;
                float _WidthNoiseStrength;

                float _Elasticity;
                float _BendSpeed;

                float _WindAIntensity;
                float _WindAFrequency;
                float2 _WindATiling;
                float2 _WindAWrap;

                float _WindBIntensity;
                float _WindBFrequency;
                float2 _WindBTiling;
                float2 _WindBWrap;

                float _WindCIntensity;
                float _WindCFrequency;
                float2 _WindCTiling;
                float2 _WindCWrap;

                half3 _ColorA;
                half3 _ColorB;
                float _MixNoiseScale;
                float2 _MixNoiseOffset;

                float _GlobalVarScale;
                float _GlobalVarStrength;

                float4 _TextureTiling;
                half3 _GroundColor;
            CBUFFER_END

            sampler2D _GrassBendingRT;
            sampler2D _GrassTexture;
            
            StructuredBuffer<float3> _AllInstancesTransformBuffer;
            StructuredBuffer<uint> _VisibleInstanceOnlyTransformIDBuffer;

            float random(float2 st) {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            float noise(float2 p) {
                float2 i = floor(p);
                float2 f = frac(p);
                
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                
                float2 u = f * f * (3.0 - 2.0 * f);
                
                return lerp(a, b, u.x) +
                        (c - a) * u.y * (1.0 - u.x) +
                        (d - b) * u.x * u.y;
            }

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                float3 perGrassPivotPosWS = _AllInstancesTransformBuffer[_VisibleInstanceOnlyTransformIDBuffer[instanceID]];
                // 获取相机世界位置
                float3 cameraPosWS = _WorldSpaceCameraPos;
                
                // 计算偏移
                int cameraXDiv = (int)((perGrassPivotPosWS.z-cameraPosWS.z) / 125);
                perGrassPivotPosWS.z -= ceil(cameraXDiv * 0.5) * 250;

                // 高度 & 宽度噪声控制
                // 草覆盖噪声（用于控制哪些地方长草，以及过渡）
                float coverNoise = noise(perGrassPivotPosWS.xz * _HeightNoiseScale);

                // 覆盖噪声阈值，低于这个就几乎没有草
                float noGrassThreshold = 0.3;   // 可调
                float fullGrassThreshold = 0.6; // 可调

                // 用 smoothstep 做过渡权重（0 = 无草, 1 = 草满高）
                float coverFactor = 0.0;
                if (coverNoise <= noGrassThreshold) {
                    // 完全无草区域
                    coverFactor = 0.0;
                } else if (coverNoise <= fullGrassThreshold) {
                    // 过渡区域：从0.5到1渐变
                    coverFactor = 0.5 + 0.5 * smoothstep(noGrassThreshold, fullGrassThreshold, coverNoise);
                } else {
                    // 完全有草区域
                    coverFactor = 1.0;
                }

                // 高度噪声（在草存在的情况下再变化）
                float heightNoiseVal = noise(perGrassPivotPosWS.xz * _HeightNoiseScale + 5.123);
                float heightRandomFactor = lerp(1.0, 1.0 + _HeightNoiseStrength, heightNoiseVal);

                // 最终草高度（覆盖权重 * 随机高度变化）
                float perGrassHeight = coverFactor * heightRandomFactor * _GrassHeight;

                float widthNoiseVal = noise((perGrassPivotPosWS.xz + 19.17) * _WidthNoiseScale);
                float widthRandom = lerp(0.5, 1.0 + _WidthNoiseStrength, widthNoiseVal);

                // 草 bending RT UV
                // float2 grassBendingUV = ((perGrassPivotPosWS.xz - _PivotPosWS.xz) / _BoundSize) * 0.5 + 0.5;
                // float stepped = tex2Dlod(_GrassBendingRT, float4(grassBendingUV, 0, 0)).x;

                // 对齐方向
                float3 rightWS, upWS, forwardWS;
                #ifdef USE_BILLBOARD
                    rightWS = UNITY_MATRIX_V[0].xyz;
                    upWS =  upWS = float3(0, 1, 0);;
                    forwardWS = -UNITY_MATRIX_V[2].xyz;
                #else
                    upWS = float3(0, 1, 0);
                    float randomAngle = sin(perGrassPivotPosWS.x * 12.9898 + perGrassPivotPosWS.z * 78.233) * 6.2831853; 
                    rightWS = normalize(float3(sin(randomAngle), 0, cos(randomAngle)));
                    forwardWS = cross(upWS, rightWS);
                #endif

                // 几何扩展（宽度变化）
                float3 positionOS = IN.positionOS.x * rightWS * _GrassWidth * perGrassHeight*widthRandom;
                positionOS += IN.positionOS.y * upWS;

                // // 果冻弹性压扁
                // float3 bendDir = forwardWS;
                // bendDir.xz *= 0.5;
                // bendDir.y = min(-0.5, bendDir.y);

                // 弹性动态偏移
                // float elasticOffset = sin(_Time.y * _BendSpeed + perGrassPivotPosWS.x * 0.3) * _Elasticity * (1.0 - stepped);
                // float bendControl = saturate(stepped + elasticOffset);

                // positionOS = lerp(
                //     positionOS.xyz + bendDir * positionOS.y / -bendDir.y,
                //     positionOS.xyz,
                //     bendControl * 0.95 + 0.05
                // );

                // 应用高度变化
                positionOS.y *= perGrassHeight;

                // 摄像机距离缩放
                float3 viewWS = _WorldSpaceCameraPos - perGrassPivotPosWS;
                float ViewWSLength = length(viewWS);
                #ifdef USE_BILLBOARD
                    //positionOS += rightWS * IN.positionOS.x * max(0, ViewWSLength * 0.0225);
                #endif

                float3 positionWS = positionOS + perGrassPivotPosWS;

                // 风动画
                // 时间量化：把时间分成固定的卡顿步长，比如 0.1秒一变
                float stepSize = 0.05; // 卡顿的时间间隔（可调）
                float quantizedTime = floor(_Time.y / stepSize) * stepSize;

                // 用 quantizedTime 替代 _Time.y
                float wind = 0;
                wind += (sin(quantizedTime * _WindAFrequency + perGrassPivotPosWS.x * _WindATiling.x + perGrassPivotPosWS.z * _WindATiling.y) * _WindAWrap.x + _WindAWrap.y) * _WindAIntensity;
                wind += (sin(quantizedTime * _WindBFrequency + perGrassPivotPosWS.x * _WindBTiling.x + perGrassPivotPosWS.z * _WindBTiling.y) * _WindBWrap.x + _WindBWrap.y) * _WindBIntensity;
                wind += (sin(quantizedTime * _WindCFrequency + perGrassPivotPosWS.x * _WindCTiling.x + perGrassPivotPosWS.z * _WindCTiling.y) * _WindCWrap.x + _WindCWrap.y) * _WindCIntensity;

                // // 也可以再对 wind 本身量化，形成像素跳动效果
                // float quantStrength = 0.05; // 移动幅度档位
                // wind = floor(wind / quantStrength) * quantStrength;

                wind *= IN.positionOS.y;
                float3 windOffset = rightWS * wind;
                positionWS.xyz += windOffset;
                
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = IN.texcoord * _TextureTiling.xy + _TextureTiling.zw;

                // ===== 颜色逻辑 =====
                float mixNoiseVal = noise(perGrassPivotPosWS.xz * _MixNoiseScale + _MixNoiseOffset);
                float mixFactor = smoothstep(0.2, 0.8, mixNoiseVal);
                half3 topColor = lerp(_ColorA, _ColorB, mixFactor);

                float globalVarNoise = noise(perGrassPivotPosWS.xz * _GlobalVarScale);
                float brightnessMultiplier = 1.0 - (globalVarNoise * _GlobalVarStrength);
                topColor *= brightnessMultiplier;

                half3 finalColor = lerp(_GroundColor, topColor, IN.positionOS.y);
                OUT.color = finalColor;

                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = tex2D(_GrassTexture, IN.uv);
                clip(texColor.a - _TextureCutoff);
                half3 finalColor = IN.color * texColor.rgb;
                finalColor = MixFog(finalColor, IN.fogFactor);
                return half4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}