// Domain-warped fBM noise colored with a two-color gradient.
// Ported from the Shadertoy "Base warp fBM" (https://www.shadertoy.com/view/tdG3Rd),
// based on Inigo Quilez's article: https://iquilezles.org/articles/warp/
// Plain HLSL so it can be included from both URP (HLSLPROGRAM) and UI (CGPROGRAM) shaders.
//
// Quality is chosen with the _QUALITY_HIGH / _QUALITY_MEDIUM / _QUALITY_LOW keywords:
//   High   - 6 octaves, 3 warp levels (original look)
//   Medium - 4 octaves, 3 warp levels (drops the two faintest octaves, ~67% cost)
//   Low    - 3 octaves, 2 warp levels (smoother, less swirly, ~33% cost)
// If no keyword is set, High is used.

#ifndef MINDTHEGAP_WARP_FBM_INCLUDED
#define MINDTHEGAP_WARP_FBM_INCLUDED

// ---------- 2D version (UI) ----------

float WarpFBM_Rand(float2 n)
{
    return frac(sin(dot(n, float2(12.9898, 4.1414))) * 43758.5453);
}

float WarpFBM_Noise(float2 p)
{
    float2 ip = floor(p);
    float2 u = frac(p);
    u = u * u * (3.0 - 2.0 * u);

    float res = lerp(
        lerp(WarpFBM_Rand(ip), WarpFBM_Rand(ip + float2(1.0, 0.0)), u.x),
        lerp(WarpFBM_Rand(ip + float2(0.0, 1.0)), WarpFBM_Rand(ip + float2(1.0, 1.0)), u.x),
        u.y);

    return res * res;
}

// Rotation between octaves (same as the GLSL mat2(0.8, 0.6, -0.6, 0.8)).
static const float2x2 WarpFBM_Rotation2 = float2x2(0.80, 0.60, -0.60, 0.80);

float WarpFBM_Fbm(float2 p, float t)
{
    float f = 0.0;

    // Skipped octaves still advance p, so the remaining octaves keep
    // the same frequencies as the High version and the look matches.
    f += 0.500000 * WarpFBM_Noise(p + t); p = mul(p, WarpFBM_Rotation2) * 2.02;
    #if !defined(_QUALITY_MEDIUM) && !defined(_QUALITY_LOW)
    f += 0.031250 * WarpFBM_Noise(p);
    #endif
    p = mul(p, WarpFBM_Rotation2) * 2.01;
    f += 0.250000 * WarpFBM_Noise(p);     p = mul(p, WarpFBM_Rotation2) * 2.03;
    f += 0.125000 * WarpFBM_Noise(p);     p = mul(p, WarpFBM_Rotation2) * 2.01;

    #if defined(_QUALITY_LOW)
    return f / 0.875;
    #elif defined(_QUALITY_MEDIUM)
    f += 0.062500 * WarpFBM_Noise(p);
    return f / 0.9375;
    #else
    f += 0.062500 * WarpFBM_Noise(p);     p = mul(p, WarpFBM_Rotation2) * 2.04;
    f += 0.015625 * WarpFBM_Noise(p + sin(t));
    return f / 0.96875;
    #endif
}

// Returns a 0..1 shade value.
float WarpFBM_Pattern(float2 p, float t)
{
    #if defined(_QUALITY_LOW)
    return WarpFBM_Fbm(p + WarpFBM_Fbm(p, t), t);
    #else
    return WarpFBM_Fbm(p + WarpFBM_Fbm(p + WarpFBM_Fbm(p, t), t), t);
    #endif
}

// ---------- 3D version (meshes, seamless in object space) ----------

float WarpFBM_Rand(float3 n)
{
    return frac(sin(dot(n, float3(12.9898, 4.1414, 78.233))) * 43758.5453);
}

float WarpFBM_Noise(float3 p)
{
    float3 ip = floor(p);
    float3 u = frac(p);
    u = u * u * (3.0 - 2.0 * u);

    float bottom = lerp(
        lerp(WarpFBM_Rand(ip),                        WarpFBM_Rand(ip + float3(1.0, 0.0, 0.0)), u.x),
        lerp(WarpFBM_Rand(ip + float3(0.0, 1.0, 0.0)), WarpFBM_Rand(ip + float3(1.0, 1.0, 0.0)), u.x),
        u.y);

    float top = lerp(
        lerp(WarpFBM_Rand(ip + float3(0.0, 0.0, 1.0)), WarpFBM_Rand(ip + float3(1.0, 0.0, 1.0)), u.x),
        lerp(WarpFBM_Rand(ip + float3(0.0, 1.0, 1.0)), WarpFBM_Rand(ip + float3(1.0, 1.0, 1.0)), u.x),
        u.y);

    float res = lerp(bottom, top, u.z);
    return res * res;
}

// 3D rotation between octaves (from Inigo Quilez's 3D fBM examples).
static const float3x3 WarpFBM_Rotation3 = float3x3(
     0.00,  0.80,  0.60,
    -0.80,  0.36, -0.48,
    -0.60, -0.48,  0.64);

float WarpFBM_Fbm(float3 p, float t)
{
    float f = 0.0;

    f += 0.500000 * WarpFBM_Noise(p + t); p = mul(p, WarpFBM_Rotation3) * 2.02;
    #if !defined(_QUALITY_MEDIUM) && !defined(_QUALITY_LOW)
    f += 0.031250 * WarpFBM_Noise(p);
    #endif
    p = mul(p, WarpFBM_Rotation3) * 2.01;
    f += 0.250000 * WarpFBM_Noise(p);     p = mul(p, WarpFBM_Rotation3) * 2.03;
    f += 0.125000 * WarpFBM_Noise(p);     p = mul(p, WarpFBM_Rotation3) * 2.01;

    #if defined(_QUALITY_LOW)
    return f / 0.875;
    #elif defined(_QUALITY_MEDIUM)
    f += 0.062500 * WarpFBM_Noise(p);
    return f / 0.9375;
    #else
    f += 0.062500 * WarpFBM_Noise(p);     p = mul(p, WarpFBM_Rotation3) * 2.04;
    f += 0.015625 * WarpFBM_Noise(p + sin(t));
    return f / 0.96875;
    #endif
}

float WarpFBM_Pattern(float3 p, float t)
{
    #if defined(_QUALITY_LOW)
    return WarpFBM_Fbm(p + WarpFBM_Fbm(p, t), t);
    #else
    return WarpFBM_Fbm(p + WarpFBM_Fbm(p + WarpFBM_Fbm(p, t), t), t);
    #endif
}

// ---------- Coloring ----------

// Maps the 0..1 shade to a gradient from colorA (dark areas) to colorB (bright areas).
float3 WarpFBM_Color(float shade, float3 colorA, float3 colorB)
{
    return lerp(colorA, colorB, saturate(shade));
}

#endif
