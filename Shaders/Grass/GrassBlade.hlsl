#include "Packages/com.saltbox.saga/Shaders/Grass/GrassBladeData.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Wind.hlsl"

float _SegmentCount;

StructuredBuffer<uint> _VisibleBladeIndices;

// set by SagaGrassInteractor.cs as globals, so outside UnityPerMaterial
float4 _GrassInteractorPositionRadius;
float _GrassInteractorStrength;

// horizontal shove away from the interactor, in metres
float3 GrassInteractorPush(GrassBlade blade)
{
    float3 interactorPosition = _GrassInteractorPositionRadius.xyz;
    float radius = max(_GrassInteractorPositionRadius.w, 0.001);
    float2 away = blade.rootPositionWS.xz - interactorPosition.xz;
    float distanceXZ = length(away);
    // eases to zero at the rim so blades there don't snap as the edge passes
    float falloff = 1.0 - smoothstep(0.0, radius, distanceXZ);
    // feet above the tips push nothing, so grass springs up under a jump
    float heightGate = 1.0 - saturate((interactorPosition.y - blade.rootPositionWS.y) / blade.height);
    float2 direction = away / max(distanceXZ, 1e-4);
    return float3(direction.x, 0, direction.y) * (falloff * heightGate * _GrassInteractorStrength * blade.height);
}

struct GrassVertex
{
    float3 positionWS;
    float3 normalWS;
    float3 tangentWS;
    float along;
};

GrassVertex EvaluateGrassVertex(uint vertexID, uint instanceID)
{
    GrassBlade blade = _Blades[_VisibleBladeIndices[instanceID]];
    float along = (vertexID / 2) / _SegmentCount;
    float side = (vertexID % 2) * 2.0 - 1.0;

    float3 up = float3(0, 1, 0);
    float3 facing = float3(sin(blade.facingAngle), 0, cos(blade.facingAngle));
    float3 sideDirection = cross(up, facing);

    float3 root = blade.rootPositionWS;
    float3 control = root + up * blade.height * 0.6;
    float3 tip = root + up * blade.height + facing * blade.height * blade.bend;
    // move the curve's end rather than its vertices, so the tangent and normal below bend with it
    tip = SagaApplyWind(tip, facing, root, 1.0, 1.0, 1.0);

    float3 push = GrassInteractorPush(blade);
    // length kept, so the push swings the tip down an arc and the blade lies flat instead of stretching
    float tipLength = length(tip - root);
    tip = root + normalize(tip + push - root) * tipLength;
    // the lower half leans too, or a pushed blade rises straight up and hooks over at the top
    float controlLength = length(control - root);
    control = root + normalize(control + push * 0.5 - root) * controlLength;

    float inverse = 1.0 - along;
    float3 spine = inverse * inverse * root + 2.0 * inverse * along * control + along * along * tip;
    // derivative of the curve: the direction the blade is heading at this height
    float3 spineTangent = normalize(2.0 * inverse * (control - root) + 2.0 * along * (tip - control));

    GrassVertex result;
    // linear taper to a point
    result.positionWS = spine + sideDirection * side * blade.width * 0.5 * inverse;
    result.normalWS = normalize(cross(sideDirection, spineTangent));
    result.tangentWS = sideDirection;
    result.along = along;
    return result;
}
