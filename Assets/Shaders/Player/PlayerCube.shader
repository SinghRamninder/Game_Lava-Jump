// Toon-shaded white cube with a black border on every face edge and square eyes on the front (+Z) face.
// Border and eyes are drawn procedurally from object-space position, so they stay crisp at any distance,
// and the border keeps a constant world width while the cube is squashed and stretched.
Shader "Player/Player Cube"
{
    Properties
    {
        [Header(Body)]
        _BaseColor ("Lit Color", Color) = (0.97, 0.97, 0.98, 1)
        _ShadeColor ("Shade Color", Color) = (0.72, 0.75, 0.83, 1)
        _ShadeSoftness ("Shade Softness", Range(0.01, 1)) = 0.35
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.25
        _ExtraLightStrength ("Point Light Strength", Range(0, 2)) = 0.6

        [Header(Border)]
        _BorderColor ("Border Color", Color) = (0.03, 0.03, 0.035, 1)
        _BorderWidth ("Border Width (world units)", Range(0, 0.3)) = 0.07

        [Header(Eyes)]
        _EyeColor ("Eye Color", Color) = (0.03, 0.03, 0.035, 1)
        _EyeSize ("Eye Size (x, y)", Vector) = (0.14, 0.2, 0, 0)
        _EyeSpacing ("Eye Spacing", Range(0, 0.4)) = 0.17
        _EyeHeight ("Eye Height", Range(-0.4, 0.4)) = 0.08
        _EyeGlint ("Eye Glint", Range(0, 1)) = 1
        _Blink ("Blink (0 open, 1 closed)", Range(0, 1)) = 0
        _LookOffset ("Look Offset (x, y)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadeColor;
            half _ShadeSoftness;
            half _AmbientStrength;
            half _ExtraLightStrength;
            half4 _BorderColor;
            half _BorderWidth;
            half4 _EyeColor;
            float4 _EyeSize;
            half _EyeSpacing;
            half _EyeHeight;
            half _EyeGlint;
            half _Blink;
            float4 _LookOffset;
        CBUFFER_END

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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.positionOS = input.positionOS.xyz;
                o.normalOS = input.normalOS;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // Anti-aliased "inside the box" mask
            half BoxMask(float2 p, float2 halfSize)
            {
                float2 d = abs(p) - halfSize;
                float dist = max(d.x, d.y);
                float aa = max(fwidth(dist), 1e-4);
                return 1.0 - smoothstep(-aa, aa, dist);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // ---- Which face are we on, and where on it (object space, cube is -0.5..0.5) ----
                float3 an = abs(i.normalOS);
                float3 scale = float3(length(GetObjectToWorldMatrix()._m00_m10_m20),
                                      length(GetObjectToWorldMatrix()._m01_m11_m21),
                                      length(GetObjectToWorldMatrix()._m02_m12_m22));
                float2 faceP;      // in-plane object coordinates
                float2 faceScale;  // world size of one object unit along those axes
                if (an.x > an.y && an.x > an.z)      { faceP = i.positionOS.zy; faceScale = scale.zy; }
                else if (an.y > an.z)                { faceP = i.positionOS.xz; faceScale = scale.xz; }
                else                                 { faceP = i.positionOS.xy; faceScale = scale.xy; }

                // ---- Border: constant world width along every face edge ----
                float2 toEdge = (0.5 - abs(faceP)) * faceScale;
                float edgeDist = min(toEdge.x, toEdge.y);
                float aa = max(fwidth(edgeDist), 1e-4);
                half border = 1.0 - smoothstep(_BorderWidth - aa, _BorderWidth + aa, edgeDist);

                // ---- Eyes on the front face ----
                half eye = 0;
                half glint = 0;
                if (i.normalOS.z > 0.5)
                {
                    float2 p = i.positionOS.xy - float2(_LookOffset.x, _EyeHeight + _LookOffset.y);
                    float2 halfSize = _EyeSize.xy * 0.5;
                    halfSize.y *= saturate(1.0 - _Blink) * 0.9 + 0.1;   // closes to a thin line
                    float2 pl = p - float2(-_EyeSpacing, 0);
                    float2 pr = p - float2(_EyeSpacing, 0);
                    eye = max(BoxMask(pl, halfSize), BoxMask(pr, halfSize));

                    // Little square highlight in the upper corner of each eye
                    float2 g = float2(_EyeSize.x * 0.17, _EyeSize.y * 0.12);
                    float2 gOff = float2(_EyeSize.x * 0.18, _EyeSize.y * 0.22);
                    glint = max(BoxMask(pl - gOff, g), BoxMask(pr - gOff, g)) * _EyeGlint * step(_Blink, 0.4);
                }

                // ---- Toon lighting ----
                float3 normalWS = normalize(i.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;
                half lit = smoothstep(0.0, _ShadeSoftness, ndl);
                half3 body = lerp(_ShadeColor.rgb, _BaseColor.rgb, lit);
                body += SampleSH(normalWS) * _AmbientStrength * _BaseColor.rgb;

                // Warm light from point lights such as the lava glow
                half3 extra = 0;
            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    extra += light.color * light.distanceAttenuation * saturate(dot(normalWS, light.direction));
                LIGHT_LOOP_END
            #endif
                body += extra * _ExtraLightStrength * _BaseColor.rgb;

                half3 color = lerp(body, _EyeColor.rgb, eye);
                color = lerp(color, half3(1, 1, 1), glint);
                color = lerp(color, _BorderColor.rgb, border);
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
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

            half4 frag(Varyings i) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(i.normalWS), 0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
