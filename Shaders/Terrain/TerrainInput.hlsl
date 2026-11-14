#ifndef SAGA_TERRAIN_INPUT_INCLUDED
#define SAGA_TERRAIN_INPUT_INCLUDED

// all three must exist before Standard's input and passes are included, since the macros are expanded inside them
#define SAGA_MODIFY_POSITION_WS(positionWS, IN) SagaTerrainPlaceVertex(positionWS, IN.positionOS, IN.normalOS, IN.uv)
#define SAGA_ALPHA_CLIP(uv) SagaTerrainClipHoles(uv)
// IN is Standard's Frag input, picked up by name; every SagaSurface field is overwritten so Standard's own sampling is dead code
#define SAGA_MODIFY_SURFACE(surf, positionWS) surf = SagaSampleTerrainSurface(IN.uv, normalize(IN.normalWS))

#include "Packages/com.saltbox.saga/Shaders/Standard/StandardInput.hlsl"

// written per terrain by the engine at draw time, kept out of UnityPerMaterial as URP TerrainLit does
CBUFFER_START(_Terrain)
    half _NormalScale0, _NormalScale1, _NormalScale2, _NormalScale3;
    half _Metallic0, _Metallic1, _Metallic2, _Metallic3;
    half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;
    half4 _DiffuseRemapScale0, _DiffuseRemapScale1, _DiffuseRemapScale2, _DiffuseRemapScale3;
    half4 _MaskMapRemapOffset0, _MaskMapRemapOffset1, _MaskMapRemapOffset2, _MaskMapRemapOffset3;
    half4 _MaskMapRemapScale0, _MaskMapRemapScale1, _MaskMapRemapScale2, _MaskMapRemapScale3;
    half _LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3;
    float4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
    float4 _Control_TexelSize;
#ifdef UNITY_INSTANCING_ENABLED
    float4 _TerrainHeightmapRecipSize;
#endif
    float4 _TerrainHeightmapScale;
CBUFFER_END

TEXTURE2D(_Control);
SAMPLER(sampler_Control);

// one sampler per texture kind; twelve layer samplers would blow the 16-sampler limit
TEXTURE2D(_Splat0);
SAMPLER(sampler_Splat0);
TEXTURE2D(_Splat1);
TEXTURE2D(_Splat2);
TEXTURE2D(_Splat3);

TEXTURE2D(_Normal0);
SAMPLER(sampler_Normal0);
TEXTURE2D(_Normal1);
TEXTURE2D(_Normal2);
TEXTURE2D(_Normal3);

TEXTURE2D(_Mask0);
SAMPLER(sampler_Mask0);
TEXTURE2D(_Mask1);
TEXTURE2D(_Mask2);
TEXTURE2D(_Mask3);

#ifdef UNITY_INSTANCING_ENABLED
TYPED_TEXTURE2D(float4, _TerrainHeightmapTexture);
TEXTURE2D(_TerrainNormalmapTexture);
#endif

UNITY_INSTANCING_BUFFER_START(Terrain)
    UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
UNITY_INSTANCING_BUFFER_END(Terrain)

#ifdef _ALPHATEST_ON
TEXTURE2D(_TerrainHolesTexture);
SAMPLER(sampler_TerrainHolesTexture);
#endif

void SagaTerrainClipHoles(float2 terrainUV)
{
#ifdef _ALPHATEST_ON
    half hole = SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, terrainUV).r;
    // compressed hole maps store "0" as roughly 1/2047
    clip(hole < 0.0005h ? -1.0h : 1.0h);
#endif
}

// instanced terrain draws a flat grid patch many times; the heightmap supplies the real position, normal and uv
void SagaTerrainPlaceVertex(inout float3 positionWS, inout float4 positionOS, inout float3 normalOS, inout float2 terrainUV)
{
#ifdef UNITY_INSTANCING_ENABLED
    float4 patch = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
    // patch.xy = where this patch starts in the heightmap, patch.z = texel stride for its lod
    float2 heightmapTexel = (positionOS.xy + patch.xy) * patch.z;
    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(heightmapTexel, 0)));

    positionOS.xz = heightmapTexel * _TerrainHeightmapScale.xz;
    positionOS.y = height * _TerrainHeightmapScale.y;
    normalOS = _TerrainNormalmapTexture.Load(int3(heightmapTexel, 0)).rgb * 2.0 - 1.0;
    terrainUV = heightmapTexel * _TerrainHeightmapRecipSize.zw;

    // Standard already transformed the patch-local position; redo it with the heightmap one
    positionWS = TransformObjectToWorld(positionOS.xyz);
#endif
}

#endif
