// Procedural volcanic sky: dark ash-filled zenith, a hot orange glow along the horizon from the lava,
// slowly drifting smoke lit from below, a hazy red sun and a few drifting embers.
Shader "Skybox/Volcanic Sky"
{
    Properties
    {
        [Header(Gradient)]
        _ZenithColor ("Zenith", Color) = (0.035, 0.025, 0.035, 1)
        _MidColor ("Mid Sky", Color) = (0.16, 0.055, 0.045, 1)
        _HorizonColor ("Horizon", Color) = (0.62, 0.2, 0.06, 1)
        _GroundColor ("Below Horizon", Color) = (0.07, 0.035, 0.03, 1)
        [HDR] _GlowColor ("Horizon Glow", Color) = (1.3, 0.38, 0.08, 1)
        _GlowFalloff ("Glow Falloff", Range(1, 40)) = 9
        _Exposure ("Exposure", Range(0, 3)) = 1

        [Header(Smoke)]
        _SmokeColor ("Smoke Color", Color) = (0.07, 0.045, 0.045, 1)
        _SmokeUnderglow ("Smoke Underglow", Color) = (0.55, 0.16, 0.05, 1)
        _SmokeCoverage ("Smoke Coverage", Range(0, 1)) = 0.55
        _SmokeScale ("Smoke Scale", Range(0.1, 5)) = 1.2
        _SmokeSpeed ("Smoke Speed", Range(0, 1)) = 0.03

        [Header(Sun)]
        [HDR] _SunColor ("Sun Color", Color) = (1.6, 0.35, 0.1, 1)
        _SunSize ("Sun Size", Range(0.001, 0.2)) = 0.035
        _SunHaze ("Sun Haze", Range(0, 2)) = 0.6

        [Header(Embers)]
        [HDR] _EmberColor ("Ember Color", Color) = (2.5, 0.8, 0.2, 1)
        _EmberDensity ("Ember Density", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Lava/LavaCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _MidColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half4 _GlowColor;
                half _GlowFalloff;
                half _Exposure;
                half4 _SmokeColor;
                half4 _SmokeUnderglow;
                half _SmokeCoverage;
                half _SmokeScale;
                half _SmokeSpeed;
                half4 _SunColor;
                half _SunSize;
                half _SunHaze;
                half4 _EmberColor;
                half _EmberDensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.direction = input.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float h = d.y;
                float time = _Time.y;

                // Base gradient
                half3 sky = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.22, h));
                sky = lerp(sky, _ZenithColor.rgb, smoothstep(0.18, 0.85, h));
                half3 ground = lerp(_HorizonColor.rgb * 0.6, _GroundColor.rgb, smoothstep(0.0, 0.12, -h));
                half3 col = h >= 0 ? sky : ground;

                // Smoke layer projected onto a dome
                float2 uv = d.xz / (abs(h) + 0.18) * _SmokeScale;
                float2 drift = float2(time * _SmokeSpeed, time * _SmokeSpeed * 0.35);
                float smoke = LavaFbm(uv + drift, 5);
                smoke = LavaFbm(uv * 1.6 + (smoke - 0.5) * 1.5 - drift * 1.3, 4);
                float density = smoothstep(1.0 - _SmokeCoverage - 0.15, 1.0 - _SmokeCoverage + 0.25, smoke);
                density *= smoothstep(-0.02, 0.12, h);
                half underglow = saturate(1.0 - h * 2.2) * (1.0 - density * 0.4);
                half3 smokeCol = lerp(_SmokeColor.rgb, _SmokeUnderglow.rgb, underglow * underglow);
                col = lerp(col, smokeCol, density * 0.9);

                // Hot glow hugging the horizon (from the lava below)
                col += _GlowColor.rgb * exp(-abs(h) * _GlowFalloff) * (1.0 - density * 0.5);

                // Hazy red sun behind the smoke
                float3 sunDir = _MainLightPosition.xyz;
                float sunDot = saturate(dot(d, sunDir));
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.7, sunDot);
                float haze = pow(sunDot, 24.0) * _SunHaze;
                col += _SunColor.rgb * (disc * (1.0 - density * 0.8) + haze * 0.35);

                // Sparse embers drifting up through the sky
                if (h > 0.0 && _EmberDensity > 0)
                {
                    float2 eUv = float2(atan2(d.x, d.z) * 9.0, h * 18.0 - time * 0.08);
                    float2 cell = floor(eUv);
                    float2 f = frac(eUv);
                    float2 rnd = LavaHash22(cell);
                    float alive = step(1.0 - _EmberDensity * 0.25, LavaHash12(cell + 3.1));
                    float dist = length(f - (0.2 + rnd * 0.6));
                    float twinkle = 0.6 + 0.4 * sin(time * (2.0 + rnd.x * 3.0) + rnd.y * 6.28);
                    col += _EmberColor.rgb * alive * twinkle * (1.0 - smoothstep(0.0, 0.035, dist)) * smoothstep(0.0, 0.3, h);
                }

                return half4(col * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
