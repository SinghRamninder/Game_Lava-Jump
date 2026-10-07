#ifndef LAVA_COMMON_INCLUDED
#define LAVA_COMMON_INCLUDED

// Shared lava pattern used by the floor and the chasing wave.
// Everything is driven by continuous coordinates (world or object space), never mesh UV tiling,
// so pooled floor chunks placed next to each other line up seamlessly.

float LavaHash12(float2 p)
{
    float3 p3 = frac(p.xyx * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 LavaHash22(float2 p)
{
    float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// Gradient noise, roughly 0..1
float LavaNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

    float a = dot(LavaHash22(i)                  * 2.0 - 1.0, f);
    float b = dot(LavaHash22(i + float2(1.0, 0.0)) * 2.0 - 1.0, f - float2(1.0, 0.0));
    float c = dot(LavaHash22(i + float2(0.0, 1.0)) * 2.0 - 1.0, f - float2(0.0, 1.0));
    float d = dot(LavaHash22(i + float2(1.0, 1.0)) * 2.0 - 1.0, f - float2(1.0, 1.0));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 0.9 + 0.5;
}

float LavaFbm(float2 p, int octaves)
{
    const float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
    float sum = 0.0;
    float amp = 0.5;
    float norm = 0.0;
    for (int i = 0; i < octaves; i++)
    {
        sum += amp * LavaNoise(p);
        norm += amp;
        p = mul(rot, p) * 2.03 + 17.1;
        amp *= 0.5;
    }
    return sum / norm;
}

// x = distance to nearest cell point, y = distance to the cell border (F2 - F1), z = cell id
float3 LavaVoronoi(float2 p, float time)
{
    float2 n = floor(p);
    float2 f = frac(p);
    float f1 = 8.0;
    float f2 = 8.0;
    float id = 0.0;

    [unroll]
    for (int j = -1; j <= 1; j++)
    {
        [unroll]
        for (int i = -1; i <= 1; i++)
        {
            float2 g = float2(i, j);
            float2 h = LavaHash22(n + g);
            float2 o = 0.5 + 0.38 * sin(time + 6.2831 * h);
            float2 r = g + o - f;
            float d = dot(r, r);
            if (d < f1)
            {
                f2 = f1;
                f1 = d;
                id = LavaHash12(n + g + 7.7);
            }
            else if (d < f2)
            {
                f2 = d;
            }
        }
    }

    f1 = sqrt(f1);
    f2 = sqrt(f2);
    return float3(f1, f2 - f1, id);
}

struct LavaSurface
{
    float temperature; // 0 = cold crust, 1 = white hot
    float crust;       // 0 = molten, 1 = solid crust plate
    float height;      // crust relief, used for bump lighting
};

// p: pattern-space coordinate. heatBias pushes the result hotter (> 0) or cooler (< 0).
LavaSurface SampleLava(float2 p, float time, float heatBias, float crustAmount, float crackWidth,
                       float warpStrength, float cellScale, float bubbleAmount)
{
    LavaSurface s;

    // Slow domain warp gives the churning, flowing look
    float2 warp;
    warp.x = LavaFbm(p * 0.18 + float2(0.0, time * 0.06), 3);
    warp.y = LavaFbm(p * 0.18 + float2(5.2 - time * 0.05, 1.3), 3);
    float2 pw = p + (warp - 0.5) * 2.0 * warpStrength;

    float heat = LavaFbm(pw * 0.22 + float2(time * 0.02, -time * 0.015), 4);
    heat = saturate((heat - 0.5) * 2.6 + 0.5 + heatBias);

    float detail = LavaFbm(pw * 1.7 + time * 0.05, 2);

    // Crust plates floating on top, broken up by glowing cracks (warped so cells are irregular)
    float2 cellP = pw * cellScale + (float2(detail, LavaNoise(pw * 0.9 + 4.3)) - 0.5) * 0.9;
    float3 vor = LavaVoronoi(cellP, time * 0.35);
    float crack = 1.0 - smoothstep(0.0, crackWidth, vor.y);
    float crust = 1.0 - smoothstep(crustAmount - 0.08, crustAmount + 0.08, heat);

    // Plates warm up slightly as they get close to the molten edge
    float nearMolten = saturate(heat / max(crustAmount, 1e-3));
    nearMolten *= nearMolten * nearMolten;
    float crackHeat = crack * lerp(0.36, 0.6, saturate(nearMolten + vor.z * 0.25));
    float tCrust = max(crackHeat, 0.02 + 0.05 * detail + 0.16 * nearMolten * (1.0 - crust * 0.8));

    // Molten areas: hotter towards the middle, with bubbles that swell and pop
    float tMolten = 0.48 + 0.34 * saturate((heat - crustAmount) / max(1.0 - crustAmount, 1e-3)) + (detail - 0.5) * 0.4;
    // Thin, darker cooling skin veins stretched by the flow
    float veins = 1.0 - abs(LavaFbm(pw * 0.75 + float2(time * 0.08, 0.0), 3) * 2.0 - 1.0);
    veins = pow(saturate(veins), 8.0);
    tMolten -= veins * 0.22;
    float3 bub = LavaVoronoi(pw * 0.8 + 31.0, 0.0);
    float phase = frac(time * 0.3 + bub.z * 7.0);
    float radius = phase * 0.35;
    float bubble = (1.0 - smoothstep(radius * 0.5, radius, bub.x)) * (1.0 - phase) * step(0.55, bub.z);
    tMolten += bubble * bubbleAmount * 0.4;

    s.temperature = saturate(lerp(tMolten, tCrust, crust));
    s.crust = crust * (1.0 - crack);
    s.height = crust * smoothstep(0.0, crackWidth * 2.0, vor.y) * (0.6 + 0.4 * detail) - bubble * bubbleAmount * 0.3 * (1.0 - crust);
    return s;
}

float3 LavaRamp(float t, float3 cDeep, float3 cMid, float3 cHot, float3 cCore)
{
    float3 col = cDeep * smoothstep(0.0, 0.3, t);
    col = lerp(col, cMid, smoothstep(0.25, 0.55, t));
    col = lerp(col, cHot, smoothstep(0.52, 0.82, t));
    col = lerp(col, cCore, smoothstep(0.84, 1.0, t));
    return col * (0.2 + 1.2 * t * t);
}

// Bump a normal with a scalar height using screen-space derivatives (Mikkelsen surface gradient).
// Works on any surface and needs no tangents, so it stays continuous across chunk borders.
float3 LavaPerturbNormal(float3 n, float3 positionWS, float height)
{
    float3 dpdx = ddx(positionWS);
    float3 dpdy = ddy(positionWS);
    float dhdx = ddx(height);
    float dhdy = ddy(height);
    float3 r1 = cross(dpdy, n);
    float3 r2 = cross(n, dpdx);
    float det = dot(dpdx, r1);
    float3 surfGrad = sign(det) * (dhdx * r1 + dhdy * r2);
    return normalize(abs(det) * n - surfGrad);
}

#endif
