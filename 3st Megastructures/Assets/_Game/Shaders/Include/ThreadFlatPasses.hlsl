// ThreadFlatPasses.hlsl - pass code of Megastructure/ThreadFlat (MASTER_PROMPT 14.2.2, 8.5-8.6) plus the
// main-light shadow helper shared by ThreadLine and ThreadEmissive.
// Owner: B01 (material shaders).
//
// Usage (from a .shader pass, after Core.hlsl, the UnityPerMaterial CBUFFER and ThreadCommon.hlsl):
//   #define THREAD_FLAT 1             (ThreadFlat only: its CBUFFER members are referenced here)
//   #define THREAD_PASS_FORWARD 1     or THREAD_PASS_SHADOW / THREAD_PASS_DEPTH / THREAD_PASS_DEPTHNORMALS
//   #include "Include/ThreadFlatPasses.hlsl"
// The forward and shadow passes include URP's Shadows.hlsl before this file.
//
// Every pass runs the same vertex deformation (cable relief fade, robe hem sway) and the same coverage fade: the
// distance fade (_FadeNear .. _FadeFar) times the far-below fade (_BelowFade, ThBelowFadeKeep of ThreadPatterns.hlsl).
// ForwardLit resolves it by alpha-to-coverage when the material sets _AlphaToMask = 1 and URP reports MSAA for the
// opaque pass (_AlphaToMaskAvailable); otherwise, and in the shadow / depth / normals passes, by the screen-space
// ordered dither (critique run06 B3: with MSAA 4x and no TAA the dither showed as a screen-door stipple on the rail
// posts; A2C coverage resolves to a smooth fade).
// Every float3/float4 literal carries the marker  pal:ok  (T-PAL, 14.7.8).
#ifndef THREAD_FLAT_PASSES_INCLUDED
#define THREAD_FLAT_PASSES_INCLUDED

#include "ThreadCommon.hlsl"
#include "ThreadPatterns.hlsl"
static const float TH_GROUND_SHADOW_FADE0 = 180.0;   // D-039
static const float TH_GROUND_SHADOW_FADE1 = 500.0;   // D-039
// Ground-shadow overlays also fade by the depression of the view ray below the horizontal (critique run07 C-05 / E8 /
// B9 / PAC-15): seen from eye height every world-space edge or fade collapses to 1-4 px near the horizon, so the far
// shadow edges (150-250 m) drew a second, ruler-straight horizon. The overlay alpha is multiplied by
// smoothstep(sin 0.15 deg, sin 3.0 deg, depression)^2: shadows within 32 m of a 1.7 m eye keep their full shape, and
// any edge within 18 px (1080p, FOV 62) below the horizon steps by at most 4 codes. Shadows seen from altitude
// (beat 5, terrace shadow D-044) lie at larger depressions and are unchanged except right at their horizon.
static const float TH_GROUND_SHADOW_DEP0 = 0.0026180;   // sin(0.15 deg)
static const float TH_GROUND_SHADOW_DEP1 = 0.0523360;   // sin(3.0 deg)

#if defined(THREAD_PASS_FORWARD)
// Aerial perspective of the cable far below the camera (critique run07 A5 (a)), shared by M05 / the collars (ThreadFlat
// _BelowHaze) and the ThreadLine ribbons beyond 20 km, so the tiers stay identical where they meet. From beat 6 on the
// cable fades toward the haze colour with tau = ln(1 + drop / 5 km): 50 percent at 5 km and 91 percent at 50 km below
// the camera, so it recedes toward the planet instead of stopping. The haze is the cloud-deck white while the sea is
// below (the deck fog the cable dissolves into) and turns to the Earth's blue haze lerp(CLOUD_TOP, ATMO_RIM, 0.5) as
// the deck drops away (A 25-200 km). The gate opens above the deck top (A 3-6 km, s 594.7-616.6, about 15 s); the cable
// a walkable pose shows below the deck edge (at most ~340 m down) changes by under 7 percent in all, so nothing pops.
#define TH_HAZE_DROP   5000.0     // m below the camera of 50 percent haze
#define TH_HAZE_A0     3000.0     // virtual altitude (m): the camera leaves the deck top ...
#define TH_HAZE_A1     6000.0     // ... the haze is fully on
#define TH_HAZE_EARTH0 25000.0    // haze colour: deck white until A = 25 km ...
#define TH_HAZE_EARTH1 200000.0   // ... Earth haze from 200 km (the deck lies below the ribbon ends from 203 km)
#define TH_HAZE_ATMO   0.5        // Earth haze = lerp(CLOUD_TOP, ATMO_RIM, 0.5)

float3 ThBelowHaze(float3 color, float3 positionWS)
{
    float A = _ThSky.y * 1000.0;   // _ThSky.y = A_virt in km
    float drop = max(_WorldSpaceCameraPos.y - positionWS.y, 0.0);
    float f = smoothstep(TH_HAZE_A0, TH_HAZE_A1, A) * drop / (drop + TH_HAZE_DROP);
    float3 haze = lerp(PAL_CLOUD_TOP, lerp(PAL_CLOUD_TOP, PAL_ATMO_RIM, TH_HAZE_ATMO), smoothstep(TH_HAZE_EARTH0, TH_HAZE_EARTH1, A));
    haze = min(haze, _ThFog.w);   // the shared fog colour cap (exposure)
    return lerp(color, haze, f);
}
#endif

// ------------------------------------------------------------------------------------------------
// Shared helpers (all three material shaders).
// ------------------------------------------------------------------------------------------------
#if defined(THREAD_PASS_FORWARD)
// Main-light (sun) shadow attenuation from URP's cascaded shadow map, looked up per pixel (cascade
// selection per pixel, 14.2.4: 4 cascades), with URP's shadow-distance fade (no hard cut-off line; the
// 3-argument GetMainLight overload does the same). 1 when the variant has no main-light shadows.
float ThMainLightShadow(float3 positionWS)
{
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    return MainLightShadow(shadowCoord, positionWS, half4(1.0, 1.0, 1.0, 1.0), _MainLightOcclusionProbes); // pal:ok (no shadow mask)
#else
    return 1.0;
#endif
}
#endif

#if defined(THREAD_FLAT)

// ------------------------------------------------------------------------------------------------
// Vertex input / deformation.
// ------------------------------------------------------------------------------------------------
// Vertex colour: R = AO (0.55..1), G = height shade (1 = none), B = sway weight, A = alpha (shadow mode).
// Cable: tangent.xyz = smooth outward cylinder normal, uv1.x = radial relief above r = 50 m.
// Deck:  uv1.xy = profile coordinates (x = lateral m from the deck centre line, y = height h below the deck top).
struct ThFlatAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float4 color      : COLOR;
    float4 uv1        : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Robe hem sway (14.5.2 line 664): a travelling wave along the world wind direction (the cloud drift
// azimuth, the only wind the spec names), period 11 s through _ThTime.y, so neighbouring flutes and robes
// move slightly out of phase. Amplitude _Sway (m) x vertex colour B.
#define TP_SWAY_WAVELENGTH 60.0

void ThFlatDeform(ThFlatAttributes v, out float3 positionWS, out float3 normalWS)
{
    float3 posOS = v.positionOS.xyz;
    float3 nOS = v.normalOS;

    // Cable relief fade (14.5.4 line 694): weight = 1 - smoothstep(140, 320, camera distance); the vertex
    // slides back onto the smooth r = 50 surface and its normal blends to the smooth cylinder normal, so every
    // LOD boundary (>= 330 m away) has identical shape and normals.
    if (_ReliefFade > 0.5)
    {
        float3 sOS = v.tangentOS.xyz;
        float sl2 = dot(sOS, sOS);
        if (sl2 > 0.25)
        {
            sOS *= rsqrt(sl2);
            float dist = distance(TransformObjectToWorld(posOS), _WorldSpaceCameraPos);
            float w = 1.0 - smoothstep(TC_CableReliefFade0, TC_CableReliefFade1, dist);
            posOS -= sOS * (v.uv1.x * (1.0 - w));
            nOS = lerp(sOS, nOS, w);
        }
    }

    positionWS = TransformObjectToWorld(posOS);
    normalWS = TransformObjectToWorldNormal(nOS);   // SafeNormalize inside

    if (_Sway > 0.0)
    {
        float az = radians(TC_CloudDriftAzimuthDeg);
        float2 wind = float2(sin(az), cos(az));
        float phase = _ThTime.y + dot(positionWS.xz, wind) * (TP_TWO_PI / TP_SWAY_WAVELENGTH);
        positionWS.xz += wind * (_Sway * saturate(v.color.b) * sin(phase));
    }
}

// ------------------------------------------------------------------------------------------------
// Coverage fade: distance (_FadeNear .. _FadeFar, off when _FadeFar <= 0) times far-below (_BelowFade = 1, A-r2-1).
// The keep factor is the same in every pass; the shadow / depth / normals passes resolve it with a screen-space
// ordered dither (in the shadow pass the pixel grid is the shadow map's, which the stable cascade fit keeps fixed, and
// URP sets _WorldSpaceCameraPos to the main camera before the cascades, so the shadow fades with the object);
// ForwardLit uses alpha-to-coverage when available.
// ------------------------------------------------------------------------------------------------
#define TP_FADE_A2C_MIN 0.002   // below this keep factor the A2C mask is empty anyway: discard (saves the shading)

bool ThFlatFadeOn() { return _FadeFar > 0.0 || _BelowFade > 0.5; }

float ThFlatFadeKeep(float3 positionWS)
{
    float keep = (_FadeFar > 0.0) ? ThDistanceKeep(positionWS, _FadeNear, _FadeFar) : 1.0;
    if (_BelowFade > 0.5) keep *= ThBelowFadeKeep(positionWS);
    return keep;
}

// Shadow / depth / normals passes.
void ThFlatDistanceFade(float2 pixel, float3 positionWS)
{
    if (ThFlatFadeOn())
        ThDitherClip(pixel, ThFlatFadeKeep(positionWS));
}

// ForwardLit: returns the output alpha (1 when the fade is off or dithered). With AlphaToMask on and MSAA in the
// opaque pass, alpha = keep becomes the sample coverage, so MSAA 4x resolves the fade to a smooth ramp; without
// MSAA, A2C would degrade to a hardware-dependent threshold, so the ordered dither stays as the fallback.
float ThFlatForwardFade(float2 pixel, float3 positionWS)
{
    float alpha = 1.0;
    if (ThFlatFadeOn())
    {
        float keep = ThFlatFadeKeep(positionWS);
        if (_AlphaToMask > 0.5 && _AlphaToMaskAvailable > 0.5)
        {
            clip(keep - TP_FADE_A2C_MIN);
            alpha = keep;
        }
        else
        {
            ThDitherClip(pixel, keep);
        }
    }
    return alpha;
}

// Ground-shadow overlay opacity: D-039 camera-distance fade times the depression fade (see TH_GROUND_SHADOW_DEP0).
float ThGroundShadowFade(float3 positionWS)
{
    float3 r = positionWS - _WorldSpaceCameraPos;
    float d = length(r);
    float far = 1.0 - smoothstep(TH_GROUND_SHADOW_FADE0, TH_GROUND_SHADOW_FADE1, d);
    float g = smoothstep(TH_GROUND_SHADOW_DEP0, TH_GROUND_SHADOW_DEP1, -r.y / max(d, 1e-4));
    return far * g * g;
}

// Shade tint of this material: _ShadeTint.xyz when _ShadeTint.w > 0.5 (per-material override, critique run06
// C-07 / C-08), else the generic TH_SHADE_TINT (14.2.2 step 2). Used for every derived shade albedo (the base when
// no explicit shade colour is set, and the pattern colours).
float3 ThFlatShadeTint()
{
    return (_ShadeTint.w > 0.5) ? _ShadeTint.xyz : TH_SHADE_TINT;
}

// The colossus ground-shadow mode is a blended overlay on the salt: it never writes depth, normals or a
// shadow map (it would otherwise occlude the salt it lies 3 cm above if depth priming were ever enabled).
void ThFlatDepthDiscard()
{
    if (_ShadowMode > 0.5) clip(-1.0);
}

// ================================================================================================
// ForwardLit
// ================================================================================================
#if defined(THREAD_PASS_FORWARD)

struct ThFlatVaryings
{
    float4 positionCS        : SV_POSITION;
    float3 positionWS        : TEXCOORD0;   // not centroid: pattern derivatives
    centroid float3 normalWS : TEXCOORD1;   // centroid: no extrapolated normals (rim spikes) under MSAA
    float4 color             : TEXCOORD2;
    float2 profile           : TEXCOORD3;   // uv1.xy (deck profile coordinates: lateral, height)
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ThFlatVaryings ThFlatForwardVert(ThFlatAttributes v)
{
    ThFlatVaryings o = (ThFlatVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 positionWS, normalWS;
    ThFlatDeform(v, positionWS, normalWS);
    o.positionCS = TransformWorldToHClip(positionWS);
    o.positionWS = positionWS;
    o.normalWS = normalWS;
    o.color = v.color;
    o.profile = v.uv1.xy;
    return o;
}

float4 ThFlatForwardFrag(ThFlatVaryings i, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    float3 positionWS = i.positionWS;
    // derivatives first, in uniform control flow (every pattern is filtered from these)
    float3 dpdx = ddx(positionWS);
    float3 dpdy = ddy(positionWS);
    float2 fwProfile = abs(ddx(i.profile)) + abs(ddy(i.profile));

    float fadeAlpha = ThFlatForwardFade(i.positionCS.xy, positionWS);

    float3 N = SafeNormalize(i.normalWS);
    N = IS_FRONT_VFACE(isFront, N, -N);

    // lit and shade albedo (D-009: the generic shade is albedo x shade tint unless a shade token is set)
    float3 shadeTint = ThFlatShadeTint();
    float3 albedo = _BaseColor.rgb;
    float3 shade = (_UseShadeColor > 0.5) ? _ShadeColor.rgb : albedo * shadeTint;

#if defined(_PATTERN_SALT)
    // M01: crack levels (5 percent; levels 2-4 stronger at altitude) and level-3 cell tones; the pattern continues
    // through the ground-shadow mode
    float crackMul = ThSaltCrackMul(positionWS, dpdx, dpdy);
    albedo *= crackMul;
    shade *= crackMul;
#elif defined(_PATTERN_STEPS)
    // M03: tread _BaseColor (+ _ThirdColor stripes), riser _SecondColor, by the normal
    float tread = ThTreadWeight(N);
    float stepFz = abs(dpdx.z) + abs(dpdy.z);
    float stepFy = abs(dpdx.y) + abs(dpdy.y);
    float stripe = ThStepStripe(positionWS.z, max(stepFz, stepFy / TC_StairSlope));
    float3 treadAlbedo = lerp(_BaseColor.rgb, _ThirdColor.rgb, stripe);
    float3 riserAlbedo = _SecondColor.rgb;
    albedo = lerp(riserAlbedo, treadAlbedo, tread);
    shade = albedo * shadeTint;
    float stepFilter = ThStepFilter(dpdx, dpdy);
#elif defined(_PATTERN_MANDALA)
    // M04: SALT_WHITE rings and radial lines on upward faces (at least 2-2.8 px wide beyond 200 m)
    float mandala = ThMandala(positionWS, dpdx, dpdy) * ThUpFacing(N);
    albedo = lerp(albedo, _SecondColor.rgb, mandala);
    shade = lerp(shade, _SecondColor.rgb * shadeTint, mandala);
#elif defined(_PATTERN_CABLE)
    // M05: CORAL bands, lay-line darkening (TEAL lit, TEAL_DEEP shade), _ThirdColor (CREAM) marker plates (A11)
    float fwPhase;
    float phase = ThCableLayPhase(positionWS, dpdx, dpdy, fwPhase);
    float layMul = 1.0 - ThCableLayDarken(phase, fwPhase);
    float band = ThCableBand(positionWS.y, abs(dpdx.y) + abs(dpdy.y));
    albedo = lerp(albedo, _SecondColor.rgb, band) * layMul;
    shade = lerp(shade, _SecondColor.rgb * shadeTint, band) * layMul;
    float marker = ThCableMarker(positionWS, dpdx, dpdy);
    albedo = lerp(albedo, _ThirdColor.rgb, marker);
    shade = lerp(shade, _ThirdColor.rgb * shadeTint, marker);
#elif defined(_PATTERN_DECK)
    // M06: CREAM edge stripes on the top, wrapping over the chamfer and the outer side face (B-r2-5)
    float deckStripe = ThDeckStripe(i.profile, fwProfile);
    albedo = lerp(albedo, _SecondColor.rgb, deckStripe);
    shade = lerp(shade, _SecondColor.rgb * shadeTint, deckStripe);
#endif

    // fog optical depths once per pixel: the rim fade and the final mix use the same tau
    float tauDist, tauCloud;
    ThreadFogTaus(positionWS, tauDist, tauCloud);

    ThreadSurface s;
    s.albedo = albedo;
    s.shadeAlbedo = shade;
    s.normalWS = N;
    s.positionWS = positionWS;
    s.ao = i.color.r;
    s.heightShade = i.color.g;
    s.shadowAtten = ThMainLightShadow(positionWS);
    s.rimCoral = _RimTint;
    s.rimScale = _RimScale;
    s.cableShadowOn = _CableShadowOn;
    s.forceShade = (_ShadowMode > 0.5) ? 1.0 : 0.0;
    // rim *= (1 - fog) here and the radiance is fogged again below: the rim fades as (1 - f)^2 on purpose
    // ("rim never survives in distant haze", 14.2.2 step 5).
    s.fogAmount = ThreadFogAmountFromTaus(tauDist, tauCloud);
    float3 radiance = ThreadShadeFlat(s);

#if defined(_PATTERN_STEPS)
    // Analytic tread filter: below 1.5 px per step, the mean radiance of a tread and a riser (each shaded with
    // its own canonical normal), so the far stairs neither shimmer nor change brightness.
    [branch] if (stepFilter > 1e-3)
    {
        ThreadSurface st = s;
        st.albedo = treadAlbedo;
        st.shadeAlbedo = treadAlbedo * shadeTint;
        st.normalWS = TP_STEP_TREAD_NORMAL;
        ThreadSurface sr = s;
        sr.albedo = riserAlbedo;
        sr.shadeAlbedo = riserAlbedo * shadeTint;
        sr.normalWS = TP_STEP_RISER_NORMAL;
        float3 meanRadiance = 0.5 * (ThreadShadeFlat(st) + ThreadShadeFlat(sr));
        radiance = lerp(radiance, meanRadiance, stepFilter);
    }
#endif

    radiance += _BaseColor.rgb * _Emission;
#if defined(_PATTERN_CABLE)
    radiance += _ThirdColor.rgb * (TP_MARKER_GLOW * saturate(_ThSky.x) * marker);   // marker lights at night (A11)
#endif
    float3 color = ThreadFogMix(radiance, tauDist, tauCloud);
    if (_BelowHaze > 0.5) color = ThBelowHaze(color, positionWS);   // cable far below the camera (A5)
    // ground-shadow mode: blend alpha from vertex A; otherwise the distance-fade coverage (1 unless A2C fades)
    // ground-shadow overlays fade with camera distance (180 -> 500 m, D-039) and with the depression of the view ray
    // (C-05 / E8 round 2), so their far edges, sub-pixel wide near the horizon, never draw a second horizon line
    float alpha = (_ShadowMode > 0.5) ? saturate(i.color.a) * ThGroundShadowFade(positionWS) : fadeAlpha;
    return float4(color, alpha); // pal:ok (colour + alpha)
}
#endif // THREAD_PASS_FORWARD

// ================================================================================================
// ShadowCaster
// ================================================================================================
#if defined(THREAD_PASS_SHADOW)

// Set by URP (ShadowUtils.SetupShadowCasterConstantBuffer); normally declared by URP's ShadowCasterPass.hlsl.
float3 _LightDirection;
float3 _LightPosition;

struct ThFlatShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

ThFlatShadowVaryings ThFlatShadowVert(ThFlatAttributes v)
{
    ThFlatShadowVaryings o = (ThFlatShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    float3 positionWS, normalWS;
    ThFlatDeform(v, positionWS, normalWS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirWS = SafeNormalize(_LightPosition - positionWS);
#else
    float3 lightDirWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirWS));
    o.positionCS = ApplyShadowClamping(positionCS);
    o.positionWS = positionWS;
    return o;
}

half4 ThFlatShadowFrag(ThFlatShadowVaryings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    ThFlatDepthDiscard();
    ThFlatDistanceFade(i.positionCS.xy, i.positionWS);
    return 0;
}
#endif // THREAD_PASS_SHADOW

// ================================================================================================
// DepthOnly
// ================================================================================================
#if defined(THREAD_PASS_DEPTH)

struct ThFlatDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ThFlatDepthVaryings ThFlatDepthVert(ThFlatAttributes v)
{
    ThFlatDepthVaryings o = (ThFlatDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 positionWS, normalWS;
    ThFlatDeform(v, positionWS, normalWS);
    o.positionCS = TransformWorldToHClip(positionWS);   // the same expression as ForwardLit
    o.positionWS = positionWS;
    return o;
}

half ThFlatDepthFrag(ThFlatDepthVaryings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    ThFlatDepthDiscard();
    ThFlatDistanceFade(i.positionCS.xy, i.positionWS);
    return i.positionCS.z;
}
#endif // THREAD_PASS_DEPTH

// ================================================================================================
// DepthNormals (URP's DepthNormals prepass writes _CameraDepthTexture and _CameraNormalsTexture)
// ================================================================================================
#if defined(THREAD_PASS_DEPTHNORMALS)

struct ThFlatDepthNormalsVaryings
{
    float4 positionCS        : SV_POSITION;
    float3 positionWS        : TEXCOORD0;
    centroid float3 normalWS : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ThFlatDepthNormalsVaryings ThFlatDepthNormalsVert(ThFlatAttributes v)
{
    ThFlatDepthNormalsVaryings o = (ThFlatDepthNormalsVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 positionWS, normalWS;
    ThFlatDeform(v, positionWS, normalWS);
    o.positionCS = TransformWorldToHClip(positionWS);
    o.positionWS = positionWS;
    o.normalWS = normalWS;
    return o;
}

void ThFlatDepthNormalsFrag(ThFlatDepthNormalsVaryings i, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
    )
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    ThFlatDepthDiscard();
    ThFlatDistanceFade(i.positionCS.xy, i.positionWS);
    float3 n = SafeNormalize(i.normalWS);
    n = IS_FRONT_VFACE(isFront, n, -n);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(n);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    outNormalWS = half4(PackFloat2To888(remappedOctNormalWS), 0.0); // pal:ok (packed normal)
#else
    outNormalWS = half4(n, 0.0); // pal:ok (normal)
#endif
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}
#endif // THREAD_PASS_DEPTHNORMALS

#endif // THREAD_FLAT

#endif // THREAD_FLAT_PASSES_INCLUDED
