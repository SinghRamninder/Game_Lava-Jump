// Chasing lava wave.
// Expects the mesh built by Tools > Lava > Generate Lava Meshes:
//   uv0    = surface coordinates in world units (x = across the wave, y = distance along the profile, back -> front)
//   color  = r: front face, g: displacement mask, b: crest / lip
// Everything is in object space so the surface rolls along with the wave while it chases the player.
Shader "Lava/Lava Wave"
{
    Properties
    {
        [Header(Glow Colors)]
        [HDR] _ColorDeep ("Deep (cooling)", Color) = (0.45, 0.02, 0.0, 1)
        [HDR] _ColorMid ("Mid (red-orange)", Color) = (1.4, 0.16, 0.0, 1)
        [HDR] _ColorHot ("Hot (orange)", Color) = (2.0, 0.55, 0.04, 1)
        [HDR] _ColorCore ("Core (yellow)", Color) = (2.4, 1.5, 0.45, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 8)) = 1.8

        [Header(Crust)]
        _CrustColor ("Crust Color", Color) = (0.075, 0.05, 0.045, 1)
        _CrustSmoothness ("Crust Smoothness", Range(0, 1)) = 0.25
        _MoltenSmoothness ("Molten Smoothness", Range(0, 1)) = 0.75
        _CrustAmount ("Crust Amount", Range(0, 1)) = 0.5
        _CrackWidth ("Crack Width", Range(0.01, 0.5)) = 0.16
        _BumpHeight ("Crust Bump Height", Range(0, 0.5)) = 0.15

        [Header(Pattern)]
        _PatternScale ("Pattern Scale (per world unit)", Float) = 0.45
        _CellScale ("Crust Cell Scale", Float) = 1.1
        _WarpStrength ("Flow Warp", Range(0, 4)) = 1.2
        _BubbleAmount ("Bubbles", Range(0, 1)) = 0.4
        _FlowSpeed ("Churn Speed", Range(0, 3)) = 1.2
        _SurfaceFlow ("Roll Speed (over the crest)", Range(0, 10)) = 2.5
        _FlowStretch ("Flow Stretch", Range(0.2, 1)) = 0.6

        [Header(Heat)]
        _FrontHeat ("Front Face Heat", Range(0, 1)) = 0.3
        _LipHeat ("Crest Heat", Range(0, 1)) = 0.35
        _FloorHeight ("Floor Height (world Y)", Float) = 0
        _BaseGlowHeight ("Base Glow Height", Range(0.01, 5)) = 1.5
        _BaseHeat ("Base Heat", Range(0, 1)) = 0.45
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimIntensity ("Rim Glow", Range(0, 3)) = 0.8

        [Header(Surface Motion)]
        _DisplaceAmount ("Displacement", Range(0, 2)) = 0.45
        _DisplaceScale ("Displacement Scale", Range(0.01, 1)) = 0.22
        _LobeAmount ("Front Lobes", Range(0, 3)) = 1.2
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.3
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
            half _SurfaceFlow;
            half _FlowStretch;
            half _FrontHeat;
            half _LipHeat;
            float _FloorHeight;
            half _BaseGlowHeight;
            half _BaseHeat;
            half _RimPower;
            half _RimIntensity;
            half _DisplaceAmount;
            half _DisplaceScale;
            half _LobeAmount;
            half _PulseSpeed;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float WaveDisplacement(float2 uv, half4 mask)
        {
            float time = _Time.y;
            // Ripples carried along with the rolling surface
            float2 q = float2(uv.x, uv.y - time * _SurfaceFlow) * _DisplaceScale;
            float d = (LavaFbm(q, 3) - 0.5) * 2.0;
            // Big slow lobes that push the front in and out, plus a gentle breathing pulse
            float lobe = (LavaNoise(float2(uv.x * 0.07 + 3.1, time * 0.12)) - 0.5) * 2.0;
            lobe += sin(time * _PulseSpeed + uv.x * 0.18) * 0.25;
            d = d * _DisplaceAmount + lobe * _LobeAmount * mask.r;
            return d * mask.g;
        }

        // Displaces along the normal and rebuilds the normal from the displaced neighbours.
        void DisplaceWave(Attributes input, out float3 positionWS, out float3 normalWS)
        {
            float3 n = TransformObjectToWorldNormal(input.normalOS);
            float3 t = TransformObjectToWorldDir(input.tangentOS.xyz);
            float3 b = cross(n, t) * (input.tangentOS.w * GetOddNegativeScale());

            const float e = 0.25;
            float d0 = WaveDisplacement(input.uv, input.color);
            float du = WaveDisplacement(input.uv + float2(e, 0), input.color);
            float dv = WaveDisplacement(input.uv + float2(0, e), input.color);

            positionWS = TransformObjectToWorld(input.positionOS.xyz) + n * d0;
            normalWS = normalize(n - t * ((du - d0) / e) - b * ((dv - d0) / e));
        }
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
                float2 uv : TEXCOORD2;
                half4 color : TEXCOORD3;
                half4 fogFactorAndVertexLight : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS, normalWS;
                DisplaceWave(input, positionWS, normalWS);
                o.positionWS = positionWS;
                o.normalWS = normalWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                o.color = input.color;

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
                // Stretched along the flow so the lava looks like it is pouring over the crest
                float2 p = float2(i.uv.x, (i.uv.y - _Time.y * _SurfaceFlow) * _FlowStretch) * _PatternScale;

                float heightAboveFloor = i.positionWS.y - _FloorHeight;
                float baseGlow = 1.0 - saturate(heightAboveFloor / _BaseGlowHeight);
                float heatBias = i.color.r * _FrontHeat + i.color.b * _LipHeat + baseGlow * baseGlow * _BaseHeat;

                LavaSurface s = SampleLava(p, time, heatBias, _CrustAmount, _CrackWidth, _WarpStrength, _CellScale, _BubbleAmount);

                float3 geomNormal = normalize(i.normalWS);
                float3 normalWS = LavaPerturbNormal(geomNormal, i.positionWS, s.height * _BumpHeight);

                half3 emission = LavaRamp(s.temperature, _ColorDeep.rgb, _ColorMid.rgb, _ColorHot.rgb, _ColorCore.rgb);

                // Glowing silhouette so the wave reads clearly against the floor
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = pow(1.0 - saturate(dot(geomNormal, viewDir)), _RimPower) * _RimIntensity;
                emission += LavaRamp(0.85, _ColorDeep.rgb, _ColorMid.rgb, _ColorHot.rgb, _ColorCore.rgb) * rim * (1.0 - s.crust * 0.5);
                emission *= _EmissionIntensity;

                half3 albedo = _CrustColor.rgb * lerp(0.6, 1.0, s.crust);
                half smoothness = lerp(_MoltenSmoothness, _CrustSmoothness, s.crust);

                return LavaShade(i.positionWS, i.positionCS, normalWS, i.fogFactorAndVertexLight.x, i.fogFactorAndVertexLight.yzw,
                                 albedo, smoothness, emission, 0.0);
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
                float3 positionWS, normalWS;
                DisplaceWave(input, positionWS, normalWS);
                o.positionCS = TransformWorldToHClip(positionWS);
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
                float3 positionWS, normalWS;
                DisplaceWave(input, positionWS, normalWS);
                o.positionCS = TransformWorldToHClip(positionWS);
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
