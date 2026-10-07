// Alpha-blended particle for smoke / dust puffs and rings.
// With no texture assigned it draws a procedural soft round blob.
Shader "Lava/Soft Particle"
{
    Properties
    {
        _MainTex ("Texture (optional)", 2D) = "white" {}
        [Toggle] _UseTexture ("Use Texture", Float) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Softness ("Blob Softness", Range(0.01, 1)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "SoftParticle"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _UseTexture;
                half4 _Tint;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float d = length(i.uv * 2.0 - 1.0);
                half blob = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
                half alpha = _UseTexture > 0.5 ? tex.a : blob;
                half3 rgb = _UseTexture > 0.5 ? tex.rgb : half3(1, 1, 1);
                half4 col = half4(rgb * _Tint.rgb * i.color.rgb, alpha * _Tint.a * i.color.a);
                col.rgb = MixFog(col.rgb, i.fog);
                return col;
            }
            ENDHLSL
        }
    }
}
