#ifndef SAGA_GRASS_BLADE_DATA_INCLUDED
#define SAGA_GRASS_BLADE_DATA_INCLUDED

struct GrassBlade
{
    float3 rootPositionWS;
    float facingAngle;
    float height;
    float width;
    float bend;
    float hash;
};

StructuredBuffer<GrassBlade> _Blades;

#endif
