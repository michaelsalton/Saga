#ifndef SAGA_GLASS_SPECULAR_INCLUDED
#define SAGA_GLASS_SPECULAR_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Glass/GlassInput.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/CloudShadows.hlsl"

half3 SagaGlassGlint(float3 normalWS, float3 positionWS)
{
    [branch] if (_GlintIntensity < 0.001)
        return half3(0.0h, 0.0h, 0.0h);

    half ndl = half(dot(normalWS, GetMainLight().direction));
    half t = saturate((ndl - half(_GlintCutoff)) * rcp(max(half(_GlintFalloff), 1e-4h)));
    half glint = pow(t, max(half(_GlintPower), 1e-3h));

    return glint * half(_GlintIntensity) * half3(_GlintColor.rgb) * SagaCloudShadow(positionWS);
}

#endif
