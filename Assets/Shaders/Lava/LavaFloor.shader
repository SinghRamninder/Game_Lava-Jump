// Lava pool floor.
// The pattern and the surface swell are computed from WORLD position, so any number of pooled
// chunks can be spawned / recycled next to each other and the lava continues seamlessly across them.
Shader "Lava/Lava Floor"
{
    Properties
    {
        [Header(Glow Colors)]
        [HDR] _ColorDeep ("Deep (cooling)", Color) = (0.45, 0.02, 0.0, 1)
        [HDR] _ColorMid ("Mid (red-orange)", Color) = (1.4, 0.16, 0.0, 1)
        [HDR] _ColorHot ("Hot (orange)", Color) = (2.0, 0.55, 0.04, 1)
        [HDR] _ColorCore ("Core (yellow)", Color) = (2.4, 1.5, 0.45, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 8)) = 1.6

        [Header(Crust)]
        _CrustColor ("Crust Color", Color) = (0.075, 0.05, 0.045, 1)
        _CrustSmoothness ("Crust Smoothness", Range(0, 1)) = 0.2
        _MoltenSmoothness ("Molten Smoothness", Range(0, 1)) = 0.75
        _CrustAmount ("Crust Amount", Range(0, 1)) = 0.5
        _CrackWidth ("Crack Width", Range(0.01, 0.5)) = 0.14
        _BumpHeight ("Crust Bump Height", Range(0, 0.5)) = 0.12

        [Header(Pattern)]
        _PatternScale ("Pattern Scale (per world unit)", Float) = 0.4
        _CellScale ("Crust Cell Scale", Float) = 1.1
        _WarpStrength ("Flow Warp", Range(0, 4)) = 1.6
        _BubbleAmount ("Bubbles", Range(0, 1)) = 0.6
        _FlowSpeed ("Flow Speed", Range(0, 3)) = 1
        _FlowDirection ("Flow Direction (XZ)", Vector) = (0.1, 0.3, 0, 0)

        [Header(Surface Swell)]
        _WaveHeight ("Swell Height", Range(0, 1)) = 0.25
        _WaveScale ("Swell Scale", Range(0.01, 1)) = 0.12
        _WaveSpeed ("Swell Speed", Range(0, 3)) = 0.8

        [Header(Shading)]
        _ShadowOnEmission ("Shadows On Glow", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "LavaCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ColorDeep;
            half4 _ColorMid;
            half4 _ColorHot;
            half4 _ColorCore;
            half _EmissionIntensity;
            half4 _CrustColor;
            half _CrustSmoothness;
            half _MoltenSmoothness;
            half _CrustAmount;
            half _CrackWidth;
            half _BumpHeight;
            float _PatternScale;
            float _CellScale;
            half _WarpStrength;
            half _BubbleAmount;
            half _FlowSpeed;
            float4 _FlowDirection;
            half _WaveHeight;
            half _WaveScale;
            half _WaveSpeed;
            half _ShadowOnEmission;
        CBUFFER_END

        float LavaSwell(float2 xz, float time)
        {
            float2 p = xz * _WaveScale;
            float h = LavaNoise(p + float2(time * 0.15, time * 0.1)) - 0.5;
            h += (LavaNoise(p * 2.6 - float2(time * 0.22, -time * 0.13)) - 0.5) * 0.5;
            h += sin(xz.x * 0.35 + time * 0.8 + sin(xz.y * 0.21 + time * 0.5)) * 0.12;
            return h;
        }

        // Displaces a world position along +Y and returns the matching normal.
        float3 DisplaceFloor(float3 positionWS, out float3 normalWS)
        {
            float time = _Time.y * _WaveSpeed;
            const float e = 0.3;
            float h  = LavaSwell(positionWS.xz, time);
            float hx = LavaSwell(positionWS.xz + float2(e, 0), time);
            float hz = LavaSwell(positionWS.xz + float2(0, e), time);
            normalWS = normalize(float3(-(hx - h) / e * _WaveHeight, 1.0, -(hz - h) / e * _WaveHeight));
            positionWS.y += h * _WaveHeight;
            return positionWS;
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "LavaLighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 fogFactorAndVertexLight : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 normalWS;
                float3 positionWS = DisplaceFloor(TransformObjectToWorld(input.positionOS.xyz), normalWS);
                o.positionWS = positionWS;
                o.normalWS = normalWS;
                o.positionCS = TransformWorldToHClip(positionWS);

                half3 vertexLight = half3(0, 0, 0);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                vertexLight = VertexLighting(positionWS, normalWS);
            #endif
                o.fogFactorAndVertexLight = half4(ComputeFogFactor(o.positionCS.z), vertexLight);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float time = _Time.y * _FlowSpeed;
                float2 p = i.positionWS.xz * _PatternScale + _FlowDirection.xy * time;
                LavaSurface s = SampleLava(p, time, 0.0, _CrustAmount, _CrackWidth, _WarpStrength, _CellScale, _BubbleAmount);

                float3 normalWS = LavaPerturbNormal(normalize(i.normalWS), i.positionWS, s.height * _BumpHeight);

                half3 emission = LavaRamp(s.temperature, _ColorDeep.rgb, _ColorMid.rgb, _ColorHot.rgb, _ColorCore.rgb) * _EmissionIntensity;
                half3 albedo = _CrustColor.rgb * lerp(0.6, 1.0, s.crust);
                half smoothness = lerp(_MoltenSmoothness, _CrustSmoothness, s.crust);

                return LavaShade(i.positionWS, i.positionCS, normalWS, i.fogFactorAndVertexLight.x, i.fogFactorAndVertexLight.yzw,
                                 albedo, smoothness, emission, _ShadowOnEmission);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 normalWS;
                o.positionCS = TransformWorldToHClip(DisplaceFloor(TransformObjectToWorld(input.positionOS.xyz), normalWS));
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 normalWS;
                o.positionCS = TransformWorldToHClip(DisplaceFloor(TransformObjectToWorld(input.positionOS.xyz), normalWS));
                o.normalWS = normalWS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
