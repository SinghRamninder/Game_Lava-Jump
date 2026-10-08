// Lit platform surface with procedural cracks that grow from an impact point.
// _CrackProgress (0..1) and _ImpactPoint are driven per instance by BreakingPlatform.cs.
// Cracks are computed from the ORIGINAL object-space position stored in UV2 (TEXCOORD2), so they stay
// glued to the surface when the mesh is split into chunks that move independently.
// Back faces render as glowing molten rock, so the open insides of broken chunks look solid and hot.
Shader "Platforms/Breaking Platform"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.22

        [Header(Cracks)]
        _CrackProgress ("Crack Progress", Range(0, 1)) = 0
        _ImpactPoint ("Impact Point (object space)", Vector) = (0, 1, 0, 0)
        _CrackSpread ("Crack Spread (object units)", Float) = 3.2
        _CrackScale ("Crack Scale", Float) = 0.85
        _CrackWidth ("Crack Width", Range(0.005, 0.2)) = 0.028
        _CrackDarkColor ("Crack Edge Color", Color) = (0.06, 0.04, 0.035, 1)
        [HDR] _CrackGlow ("Crack Glow", Color) = (2.2, 0.45, 0.06, 1)
        _GlowPulse ("Glow Pulse", Range(0, 1)) = 0.35

        [Header(Broken Inside)]
        [HDR] _InteriorColor ("Interior Glow", Color) = (1.6, 0.35, 0.05, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness;
            half _CrackProgress;
            float4 _ImpactPoint;
            float _CrackSpread;
            float _CrackScale;
            half _CrackWidth;
            half4 _CrackDarkColor;
            half4 _CrackGlow;
            half _GlowPulse;
            half4 _InteriorColor;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float3 crackPos : TEXCOORD2;
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

            #include "../Lava/LavaLighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 crackPos : TEXCOORD3;
                half4 fogFactorAndVertexLight : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            // x = distance to the cell border (F2 - F1) in 3D, so cracks wrap continuously around edges
            float CrackEdge(float3 p)
            {
                float3 n = floor(p);
                float3 f = frac(p);
                float f1 = 8.0, f2 = 8.0;
                [unroll] for (int k = -1; k <= 1; k++)
                [unroll] for (int j = -1; j <= 1; j++)
                [unroll] for (int i = -1; i <= 1; i++)
                {
                    float3 g = float3(i, j, k);
                    float3 r = g + Hash33(n + g) * 0.85 + 0.075 - f;
                    float d = dot(r, r);
                    if (d < f1) { f2 = f1; f1 = d; }
                    else if (d < f2) { f2 = d; }
                }
                return sqrt(f2) - sqrt(f1);
            }

            float ValueNoise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash33(i).x;
                float b = Hash33(i + float3(1, 0, 0)).x;
                float c = Hash33(i + float3(0, 1, 0)).x;
                float d = Hash33(i + float3(1, 1, 0)).x;
                float e = Hash33(i + float3(0, 0, 1)).x;
                float g = Hash33(i + float3(1, 0, 1)).x;
                float h = Hash33(i + float3(0, 1, 1)).x;
                float k = Hash33(i + float3(1, 1, 1)).x;
                return lerp(lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y), lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y), f.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.crackPos = input.crackPos;

                half3 vertexLight = half3(0, 0, 0);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                vertexLight = VertexLighting(o.positionWS, o.normalWS);
            #endif
                o.fogFactorAndVertexLight = half4(ComputeFogFactor(o.positionCS.z), vertexLight);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 normalWS = normalize(i.normalWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 emission = 0;
                half smoothness = _Smoothness;

                if (!IS_FRONT_VFACE(isFrontFace, true, false))
                {
                    // Inside of a broken chunk: hot molten rock
                    normalWS = -normalWS;
                    float n = ValueNoise3(i.crackPos * 3.0 + _Time.y * 0.5);
                    emission = _InteriorColor.rgb * (0.6 + 0.8 * n);
                    albedo = half3(0.04, 0.03, 0.03);
                }
                else if (_CrackProgress > 0.001)
                {
                    float3 p = i.crackPos;
                    float progress = _CrackProgress;

                    // Cracks spread outwards from the impact point with a ragged front
                    float jitter = (ValueNoise3(p * 1.7) - 0.5) * 0.9;
                    float dist = distance(p, _ImpactPoint.xyz) + jitter;
                    float front1 = progress * _CrackSpread * 1.2;
                    float reveal1 = smoothstep(front1, front1 - 0.35, dist);
                    float front2 = saturate(progress * 1.7 - 0.55) * _CrackSpread;
                    float reveal2 = smoothstep(front2, front2 - 0.3, dist);

                    // Big cracks, then finer branching cracks later on
                    float width = _CrackWidth * (0.5 + 0.6 * progress);
                    float e1 = CrackEdge(p * _CrackScale);
                    float e2 = CrackEdge(p * _CrackScale * 1.9 + 11.3);
                    float aa1 = fwidth(e1);
                    float aa2 = fwidth(e2);
                    float core = max((1.0 - smoothstep(width - aa1, width + aa1, e1)) * reveal1,
                                     (1.0 - smoothstep(width * 0.55 - aa2, width * 0.55 + aa2, e2)) * reveal2 * step(0.55, ValueNoise3(p * 1.3 + 5.0)));
                    float rim = max((1.0 - smoothstep(0.0, width * 3.0, e1)) * reveal1,
                                    (1.0 - smoothstep(0.0, width * 2.0, e2)) * reveal2 * 0.7 * step(0.55, ValueNoise3(p * 1.3 + 5.0)));

                    albedo = lerp(albedo, _CrackDarkColor.rgb, saturate(rim * 0.85 + core));
                    smoothness = lerp(smoothness, 0.05, rim);
                    float pulse = 1.0 - _GlowPulse + _GlowPulse * (0.5 + 0.5 * sin(_Time.y * 9.0 + dist * 3.0));
                    emission = _CrackGlow.rgb * core * pulse * smoothstep(0.0, 0.5, progress);
                }

                return LavaShade(i.positionWS, i.positionCS, normalWS, i.fogFactorAndVertexLight.x, i.fogFactorAndVertexLight.yzw,
                                 albedo, smoothness, emission, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return ApplyShadowClamping(positionCS);
            }

            half4 frag() : SV_Target
            {
                return 0;
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

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half frag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
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
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = NormalizeNormalPerPixel(i.normalWS);
                n = IS_FRONT_VFACE(isFrontFace, n, -n);
                return half4(n, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
