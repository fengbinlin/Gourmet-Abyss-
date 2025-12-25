Shader "Custom/URP/GrassInteractiveFinal"
{
    Properties
    {
        // 基础属性
        [MainTexture] _MainTex ("Albedo (RGB)", 2D) = "white" {}
        [MainColor] _Color ("Color", Color) = (1,1,1,1)
        
        // 风属性
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.15
        _WindSpeed ("Wind Speed", Float) = 1.5
        _WindFrequency ("Wind Frequency", Float) = 0.5
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0.5, 0) // 风的方向
        
        // 草属性
        _GrassHeight ("Grass Height", Float) = 1.0
        _GrassStiffness ("Grass Stiffness", Range(0.1, 3.0)) = 1.0
        _MaxBend ("Max Bend", Range(0, 1)) = 0.3
        
        // 交互属性
        _InteractionRadius ("Interaction Radius", Float) = 1.5
        _BounceSpeed ("Bounce Speed", Float) = 4.0
        
        // 像素化效果
        [Header(Pixelation)]
        [Toggle(_PIXELATION_ON)] _PixelationEnabled ("Enable Pixelation", Float) = 0
        _PixelSize ("Pixel Size", Range(1, 50)) = 10
        _PixelScale ("Pixel Scale", Range(0.1, 5)) = 1.0
    }
    
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // GPU实例化
            #pragma multi_compile_instancing
            
            // 光照
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            
            // 像素化
            #pragma shader_feature _PIXELATION_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            // 交互数据
            float4 _InteractionDataArray[8];
            int _InteractionCount;
            
            // 风参数
            float4 _WindParams;  // x=强度, y=速度, z=频率, w=时间
            float4 _WindDirection; // 风向
            
            // 材质属性
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _GrassHeight;
                float _GrassStiffness;
                float _MaxBend;
                float _InteractionRadius;
                float _BounceSpeed;
                
                // 像素化属性
                float _PixelSize;
                float _PixelScale;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            // 计算风动效果
            float3 ApplyWind(float3 worldPos, float heightFactor)
            {
                float windStrength = _WindParams.x;
                float windSpeed = _WindParams.y;
                float windFrequency = _WindParams.z;
                float windTime = _WindParams.w;
                
                // 风向向量
                float3 windDir = normalize(_WindDirection.xyz);
                
                // 基于世界位置的风波
                float wave1 = sin(windTime * windSpeed + worldPos.x * windFrequency + worldPos.z * 0.3) * 0.5;
                float wave2 = sin(windTime * windSpeed * 1.3 + worldPos.z * windFrequency * 0.7 + worldPos.x * 0.5) * 0.3;
                float wave3 = sin(windTime * windSpeed * 0.7 + (worldPos.x + worldPos.z) * 0.4) * 0.2;
                
                // 组合多个波
                float windWave = (wave1 + wave2 + wave3) / 3.0;
                
                // 计算风动偏移
                float3 windOffset = windDir * windWave;
                
                // 草的高度影响（顶部摆动更多）
                float heightEffect = heightFactor * heightFactor;
                
                // 应用风力
                windOffset *= windStrength * heightEffect;
                
                return windOffset;
            }
            
            // 计算交互效果
            float3 ApplyInteraction(float3 vertexPosOS, float heightFactor)
            {
                float3 totalBend = float3(0, 0, 0);
                
                for (int i = 0; i < _InteractionCount && i < 8; i++)
                {
                    float4 interactionData = _InteractionDataArray[i];
                    
                    // 提取数据
                    float3 interactionPos = interactionData.xyz;
                    float strength = interactionData.w;
                    
                    if (strength < 0.001) continue;
                    
                    // 计算距离
                    float3 offset = vertexPosOS - interactionPos;
                    offset.y = 0;  // 忽略垂直距离
                    float distance = length(offset);
                    
                    if (distance < _InteractionRadius)
                    {
                        // 距离衰减
                        float distanceFactor = 1.0 - saturate(distance / _InteractionRadius);
                        distanceFactor = distanceFactor * distanceFactor;  // 平方衰减
                        
                        // 计算弯曲方向
                        float3 bendDir = normalize(offset);
                        
                        // 高度影响
                        float heightEffect = saturate(vertexPosOS.y / _GrassHeight);
                        heightEffect = heightEffect * heightEffect;
                        
                        // 计算弯曲量
                        float bendAmount = strength * distanceFactor * heightEffect;
                        
                        // 限制最大弯曲
                        bendAmount = min(bendAmount, _MaxBend);
                        
                        // 添加弹性回弹
                        float bounceTime = _Time.y * _BounceSpeed;
                        float bounce = sin(bounceTime + distance * 3.0) * 0.1;
                        bendAmount += bounce * distanceFactor;
                        
                        // 计算最终弯曲
                        float3 bend = bendDir * bendAmount;
                        
                        // 添加轻微下压效果
                        bend.y = -bendAmount * 0.3;
                        
                        totalBend += bend;
                    }
                }
                
                return totalBend;
            }
            
            // 像素化顶点位置
            float4 ApplyPixelation(float4 positionCS, float pixelSize, float pixelScale)
            {
                #if _PIXELATION_ON
                    // 计算屏幕分辨率
                    float2 screenSize = _ScreenParams.xy;
                    
                    // 计算像素网格大小
                    float pixelGridSize = pixelSize * pixelScale;
                    
                    // 转换为屏幕坐标
                    float2 screenPos = positionCS.xy / positionCS.w;
                    screenPos *= screenSize;
                    
                    // 对齐到像素网格
                    screenPos = floor(screenPos / pixelGridSize) * pixelGridSize;
                    
                    // 转换回裁剪空间
                    screenPos /= screenSize;
                    screenPos *= positionCS.w;
                    
                    // 创建新的裁剪空间位置
                    float4 pixelatedPos = positionCS;
                    pixelatedPos.xy = screenPos;
                    
                    return pixelatedPos;
                #else
                    return positionCS;
                #endif
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                // 计算高度因子
                float heightFactor = saturate(input.positionOS.y / _GrassHeight);
                heightFactor = heightFactor * heightFactor;  // 非线性
                
                // 转换为世界空间
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                
                // 计算风动
                float3 windBend = ApplyWind(worldPos, heightFactor);
                
                // 计算交互
                float3 interactionBend = ApplyInteraction(input.positionOS.xyz, heightFactor);
                
                // 合并所有弯曲
                float3 totalBend = windBend + interactionBend;
                
                // 刚度影响
                totalBend *= (1.0 / _GrassStiffness);
                
                // 应用弯曲
                float3 finalPositionOS = input.positionOS.xyz + totalBend;
                
                // 转换到世界空间
                float3 positionWS = TransformObjectToWorld(finalPositionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                // 采样纹理
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 baseColor = texColor * _Color;
                
                // 简单光照
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 normalWS = normalize(input.normalWS);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                
                // 基础光照
                half3 color = baseColor.rgb * (mainLight.color * NdotL + half3(0.3, 0.3, 0.3));
                
                // 像素化颜色
                #if _PIXELATION_ON
                    float pixelGridSize = _PixelSize * _PixelScale;
                    if (pixelGridSize > 1.0)
                    {
                        // 获取屏幕坐标
                        float2 screenPos = input.positionCS.xy / input.positionCS.w;
                        screenPos *= _ScreenParams.xy;
                        
                        // 对齐颜色到像素网格
                        screenPos = floor(screenPos / pixelGridSize) * pixelGridSize;
                        
                        // 创建伪随机种子用于颜色变化
                        float random = sin(dot(screenPos, float2(12.9898, 78.233))) * 43758.5453;
                        random = frac(random);
                        
                        // 稍微调整颜色以增强像素化效果
                        color = round(color * 8.0) / 8.0;
                        
                        // 添加轻微的随机偏移避免颜色完全平坦
                        color += random * 0.02;
                        color = saturate(color);
                    }
                #endif
                
                return half4(color, baseColor.a);
            }
            ENDHLSL
        }
        
        // 阴影投射Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            
            #pragma multi_compile_instancing
            
            // 像素化阴影
            #pragma shader_feature _PIXELATION_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            // 交互数据
            float4 _InteractionDataArray[8];
            int _InteractionCount;
            
            // 风参数
            float4 _WindParams;
            float4 _WindDirection;
            
            // 材质属性
            CBUFFER_START(UnityPerMaterial)
                float _GrassHeight;
                float _GrassStiffness;
                float _MaxBend;
                float _InteractionRadius;
                float _BounceSpeed;
                float _PixelSize;
                float _PixelScale;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            // 计算风动
            float3 ApplyWind(float3 worldPos, float heightFactor)
            {
                float windStrength = _WindParams.x;
                float windSpeed = _WindParams.y;
                float windFrequency = _WindParams.z;
                float windTime = _WindParams.w;
                
                float3 windDir = normalize(_WindDirection.xyz);
                
                // 使用世界坐标计算风波
                float wave1 = sin(windTime * windSpeed + worldPos.x * windFrequency + worldPos.z * 0.3) * 0.5;
                float wave2 = sin(windTime * windSpeed * 1.3 + worldPos.z * windFrequency * 0.7 + worldPos.x * 0.5) * 0.3;
                float wave3 = sin(windTime * windSpeed * 0.7 + (worldPos.x + worldPos.z) * 0.4) * 0.2;
                
                float windWave = (wave1 + wave2 + wave3) / 3.0;
                float3 windOffset = windDir * windWave;
                float heightEffect = heightFactor * heightFactor;
                
                return windOffset * windStrength * heightEffect;
            }
            
            // 计算交互
            float3 ApplyInteraction(float3 vertexPosOS, float heightFactor)
            {
                float3 totalBend = float3(0, 0, 0);
                
                for (int i = 0; i < _InteractionCount && i < 8; i++)
                {
                    float4 interactionData = _InteractionDataArray[i];
                    float3 interactionPos = interactionData.xyz;
                    float strength = interactionData.w;
                    
                    if (strength < 0.001) continue;
                    
                    float3 offset = vertexPosOS - interactionPos;
                    offset.y = 0;
                    float distance = length(offset);
                    
                    if (distance < _InteractionRadius)
                    {
                        float distanceFactor = 1.0 - saturate(distance / _InteractionRadius);
                        distanceFactor = distanceFactor * distanceFactor;
                        
                        float heightEffect = saturate(vertexPosOS.y / _GrassHeight);
                        heightEffect = heightEffect * heightEffect;
                        
                        float bendAmount = strength * distanceFactor * heightEffect;
                        bendAmount = min(bendAmount, _MaxBend);
                        
                        float bounceTime = _Time.y * _BounceSpeed;
                        float bounce = sin(bounceTime + distance * 3.0) * 0.1;
                        bendAmount += bounce * distanceFactor;
                        
                        float3 bendDir = normalize(offset);
                        float3 bend = bendDir * bendAmount;
                        bend.y = -bendAmount * 0.3;
                        
                        totalBend += bend;
                    }
                }
                
                return totalBend;
            }
            
            // 像素化顶点位置
            float4 ApplyPixelation(float4 positionCS, float pixelSize, float pixelScale)
            {
                #if _PIXELATION_ON
                    float2 screenSize = _ScreenParams.xy;
                    float pixelGridSize = pixelSize * pixelScale;
                    
                    float2 screenPos = positionCS.xy / positionCS.w;
                    screenPos *= screenSize;
                    screenPos = floor(screenPos / pixelGridSize) * pixelGridSize;
                    screenPos /= screenSize;
                    screenPos *= positionCS.w;
                    
                    float4 pixelatedPos = positionCS;
                    pixelatedPos.xy = screenPos;
                    return pixelatedPos;
                #else
                    return positionCS;
                #endif
            }
            
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float heightFactor = saturate(input.positionOS.y / _GrassHeight);
                heightFactor = heightFactor * heightFactor;
                
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float3 windBend = ApplyWind(worldPos, heightFactor);
                float3 interactionBend = ApplyInteraction(input.positionOS.xyz, heightFactor);
                
                float3 totalBend = windBend + interactionBend;
                totalBend *= (1.0 / _GrassStiffness);
                
                float3 finalPositionOS = input.positionOS.xyz + totalBend;
                float3 positionWS = TransformObjectToWorld(finalPositionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                
                // 应用像素化到阴影
                output.positionCS = ApplyPixelation(output.positionCS, _PixelSize, _PixelScale);
                
                return output;
            }
            
            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
        
        // 深度法线Pass
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            
            ZWrite On
            ZTest LEqual
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            
            #pragma multi_compile_instancing
            
            // 像素化
            #pragma shader_feature _PIXELATION_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            // 交互数据
            float4 _InteractionDataArray[8];
            int _InteractionCount;
            
            // 风参数
            float4 _WindParams;
            float4 _WindDirection;
            
            // 材质属性
            CBUFFER_START(UnityPerMaterial)
                float _GrassHeight;
                float _GrassStiffness;
                float _MaxBend;
                float _InteractionRadius;
                float _BounceSpeed;
                float _PixelSize;
                float _PixelScale;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            // 计算风动
            float3 ApplyWind(float3 worldPos, float heightFactor)
            {
                float windStrength = _WindParams.x;
                float windSpeed = _WindParams.y;
                float windFrequency = _WindParams.z;
                float windTime = _WindParams.w;
                
                float3 windDir = normalize(_WindDirection.xyz);
                
                float wave1 = sin(windTime * windSpeed + worldPos.x * windFrequency + worldPos.z * 0.3) * 0.5;
                float wave2 = sin(windTime * windSpeed * 1.3 + worldPos.z * windFrequency * 0.7 + worldPos.x * 0.5) * 0.3;
                float wave3 = sin(windTime * windSpeed * 0.7 + (worldPos.x + worldPos.z) * 0.4) * 0.2;
                
                float windWave = (wave1 + wave2 + wave3) / 3.0;
                float3 windOffset = windDir * windWave;
                float heightEffect = heightFactor * heightFactor;
                
                return windOffset * windStrength * heightEffect;
            }
            
            // 计算交互
            float3 ApplyInteraction(float3 vertexPosOS, float heightFactor)
            {
                float3 totalBend = float3(0, 0, 0);
                
                for (int i = 0; i < _InteractionCount && i < 8; i++)
                {
                    float4 interactionData = _InteractionDataArray[i];
                    float3 interactionPos = interactionData.xyz;
                    float strength = interactionData.w;
                    
                    if (strength < 0.001) continue;
                    
                    float3 offset = vertexPosOS - interactionPos;
                    offset.y = 0;
                    float distance = length(offset);
                    
                    if (distance < _InteractionRadius)
                    {
                        float distanceFactor = 1.0 - saturate(distance / _InteractionRadius);
                        distanceFactor = distanceFactor * distanceFactor;
                        
                        float heightEffect = saturate(vertexPosOS.y / _GrassHeight);
                        heightEffect = heightEffect * heightEffect;
                        
                        float bendAmount = strength * distanceFactor * heightEffect;
                        bendAmount = min(bendAmount, _MaxBend);
                        
                        float bounceTime = _Time.y * _BounceSpeed;
                        float bounce = sin(bounceTime + distance * 3.0) * 0.1;
                        bendAmount += bounce * distanceFactor;
                        
                        float3 bendDir = normalize(offset);
                        float3 bend = bendDir * bendAmount;
                        bend.y = -bendAmount * 0.3;
                        
                        totalBend += bend;
                    }
                }
                
                return totalBend;
            }
            
            // 像素化顶点位置
            float4 ApplyPixelation(float4 positionCS, float pixelSize, float pixelScale)
            {
                #if _PIXELATION_ON
                    float2 screenSize = _ScreenParams.xy;
                    float pixelGridSize = pixelSize * pixelScale;
                    
                    float2 screenPos = positionCS.xy / positionCS.w;
                    screenPos *= screenSize;
                    screenPos = floor(screenPos / pixelGridSize) * pixelGridSize;
                    screenPos /= screenSize;
                    screenPos *= positionCS.w;
                    
                    float4 pixelatedPos = positionCS;
                    pixelatedPos.xy = screenPos;
                    return pixelatedPos;
                #else
                    return positionCS;
                #endif
            }
            
            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float heightFactor = saturate(input.positionOS.y / _GrassHeight);
                heightFactor = heightFactor * heightFactor;
                
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float3 windBend = ApplyWind(worldPos, heightFactor);
                float3 interactionBend = ApplyInteraction(input.positionOS.xyz, heightFactor);
                
                float3 totalBend = windBend + interactionBend;
                totalBend *= (1.0 / _GrassStiffness);
                
                float3 finalPositionOS = input.positionOS.xyz + totalBend;
                float3 positionWS = TransformObjectToWorld(finalPositionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                
                // 应用像素化
                output.positionCS = ApplyPixelation(output.positionCS, _PixelSize, _PixelScale);
                
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                return output;
            }
            
            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}