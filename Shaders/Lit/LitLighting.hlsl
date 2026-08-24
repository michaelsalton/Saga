#ifndef SAGA_LIT_LIGHTING_INCLUDED
#define SAGA_LIT_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.saltbox.saga/Shaders/Lit/LitInput.hlsl"

#include "Packages/com.saltbox.saga/ShaderLibrary/PBR.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Quantize.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/CloudShadows.hlsl"

half SagaCelBand(half ndl, half shadow, half ao)
{
    half lit = saturate((ndl + _NdlWrap) * rcp(1.0h + _NdlWrap));
    half b   = max(_Bands, 2.0h);

    half occ = shadow * ao;
    half inQ = lit * lerp(1.0h, occ, _BandShadow);

    half level = SagaSoftQuantize(inQ * b, _BandSoftness, 0.0h);
    half band  = saturate(level * rcp(max(b - 1.0h, 1.0h)));

    return band * lerp(occ, 1.0h, _BandShadow);
}

half SagaToonSpecular(half3 N, half3 L, half3 V, half roughness)
{
    half3 H = normalize(L + V);
    half ndh = saturate(dot(N, H));
    half cutoff = lerp(_SpecCutoffSmooth, _SpecCutoffRough, roughness);
    half mask = smoothstep(cutoff - _SpecEdge, cutoff + _SpecEdge, ndh);
    half area    = max(1.0h - cutoff, 1e-3h);
    half refArea = max(1.0h - _SpecCutoffSmooth, 1e-3h);

    return mask * sqrt(refArea * rcp(area));
}

half3 SagaLitLighting(InputData inputData, half3 albedo, half metallic, half roughness, half ao)
{
    half3 N = inputData.normalWS;
    half3 V = inputData.viewDirectionWS;
    half oneMinusReflectivity = (1.0h - SAGA_DIELECTRIC_F0) * (1.0h - metallic);
    half3 diffuseAlbedo = albedo * oneMinusReflectivity;
    half3 f0 = lerp(SAGA_DIELECTRIC_F0 * _SpecColor.rgb, albedo, metallic);

    Light main = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    half ndl = dot(N, main.direction);

    half cloud  = SagaCloudShadow(inputData.positionWS);
    half shadow = lerp(1.0h, min(main.shadowAttenuation, cloud), saturate(ndl * 4.0h));

    half band = SagaCelBand(ndl, shadow, ao);

    half3 litCol    = diffuseAlbedo * main.color;
    half3 shadowCol = diffuseAlbedo * _ShadowTint.rgb;
    half3 col       = lerp(shadowCol, litCol, band);
    half3 gi = inputData.bakedGI;

    [branch] if (_GIBands >= 0.5h)
    {
        half l  = max(max(gi.r, max(gi.g, gi.b)), 0.0001h);
        half n  = l * rcp(max(_GIRange, 0.0001h));
        half nq = SagaSoftQuantize(n * _GIBands, _BandSoftness, 0.5h) * rcp(_GIBands) * _GIRange;
        gi *= nq * rcp(l);
    }

    col += gi * diffuseAlbedo * ao;
    col += gi * f0 * ao;

    half spec = SagaToonSpecular(N, main.direction, V, roughness);
    spec *= step(0.0h, ndl) * shadow;
    col += spec * f0 * main.color * _SpecIntensity;

#if defined(_ADDITIONAL_LIGHTS)
    uint count = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
        Light add = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
        half atten = add.distanceAttenuation * add.shadowAttenuation;
        half addNdl = dot(N, add.direction);
        half raw = saturate(addNdl) * atten;
        half q = SagaSoftQuantize(raw * _AddLightBands, _BandSoftness, 0.5h)
               * rcp(max(_AddLightBands, 1.0h));

        col += diffuseAlbedo * add.color * q * ao;

        half addSpec = SagaToonSpecular(N, add.direction, V, roughness) * atten * step(0.0h, addNdl);
        col += addSpec * f0 * add.color * _SpecIntensity;
    LIGHT_LOOP_END
#endif

    return col;
}

#endif
