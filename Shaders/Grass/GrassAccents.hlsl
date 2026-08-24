#ifndef SAGA_GRASS_ACCENTS_INCLUDED
#define SAGA_GRASS_ACCENTS_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Grass/GrassInput.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Noise.hlsl"

#define SAGA_GRASS_SALT_CLASS   0.0
#define SAGA_GRASS_SALT_SIZE   17.13
#define SAGA_GRASS_SALT_WIND   41.77

struct SagaGrassAccent
{
    float2 scale;
    half4  tint;
};

float SagaGrassRoll(float2 pivotXZ, float salt)
{
    float s = salt + _AccentSeed * 37.31;
    return SagaHash21(pivotXZ + float2(s, s * 1.61803399));
}

SagaGrassAccent SagaResolveGrassAccent(float2 pivotXZ)
{
    SagaGrassAccent a;
    a.scale = float2(1.0, 1.0);
    a.tint  = half4(0.0h, 0.0h, 0.0h, 0.0h);

    // Continuous jitter on every tuft, accent or not. Uniform on both axes so the sprite's
    // aspect ratio survives. This is the shader-side answer to the spawner's scaleJitter
    // sitting at {1,1}, and it costs no scene overrides and no re-spawn.
    [branch] if (_SizeJitter > 0.001)
    {
        float j = SagaGrassRoll(pivotXZ, SAGA_GRASS_SALT_SIZE) * 2.0 - 1.0;   // -1..1
        a.scale *= 1.0 + j * _SizeJitter;
    }

    [branch] if (_AccentCount >= 0.5)
    {
        float r = SagaGrassRoll(pivotXZ, SAGA_GRASS_SALT_CLASS);

        float c1 = _AccentChance1;
        float c2 = c1 + (_AccentCount >= 1.5 ? _AccentChance2 : 0.0);
        float c3 = c2 + (_AccentCount >= 2.5 ? _AccentChance3 : 0.0);

        if (r < c1)
        {
            a.scale *= float2(_AccentWidth1, _AccentHeight1);
            a.tint   = (half4)float4(_AccentColor1.rgb, _AccentTint1);
        }
        else if (r < c2)
        {
            a.scale *= float2(_AccentWidth2, _AccentHeight2);
            a.tint   = (half4)float4(_AccentColor2.rgb, _AccentTint2);
        }
        else if (r < c3)
        {
            a.scale *= float2(_AccentWidth3, _AccentHeight3);
            a.tint   = (half4)float4(_AccentColor3.rgb, _AccentTint3);
        }
    }

    return a;
}

#endif
