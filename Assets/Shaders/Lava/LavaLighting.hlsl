#ifndef LAVA_LIGHTING_INCLUDED
#define LAVA_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Lights the crust with URP's PBR path (main light, shadows, Forward+ additional lights, ambient, fog)
// and adds the glowing lava emission on top.
half4 LavaShade(float3 positionWS, float4 positionCS, float3 normalWS, half fogFactor, half3 vertexLighting,
                half3 albedo, half smoothness, half3 emission, half shadowOnEmission)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.positionCS = positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), fogFactor);
    inputData.vertexLighting = vertexLighting;
    inputData.bakedGI = SampleSH(normalWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowMask = half4(1, 1, 1, 1);

    // Let objects above the lava (player, platforms) darken the glow a little so their shadows read
    Light mainLight = GetMainLight(inputData.shadowCoord, positionWS, inputData.shadowMask);
    half shadow = lerp(1.0h, mainLight.shadowAttenuation, shadowOnEmission);

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo;
    surfaceData.metallic = 0;
    surfaceData.specular = half3(0, 0, 0);
    surfaceData.smoothness = smoothness;
    surfaceData.occlusion = 1;
    surfaceData.emission = emission * shadow;
    surfaceData.alpha = 1;

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    return color;
}

#endif
