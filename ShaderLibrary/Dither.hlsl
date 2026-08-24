#ifndef SAGA_DITHER_INCLUDED
#define SAGA_DITHER_INCLUDED

half SagaBayer2x2(half y, half x)
{
    return 3.0h * y + 2.0h * x - 4.0h * x * y;
}

half SagaBayer4x4(float2 pixel)
{
    float2 p  = fmod(floor(pixel), 4.0);   // {0,1,2,3}
    float2 hi = floor(p * 0.5);            // high bit {0,1}
    float2 lo = p - hi * 2.0;              // low  bit {0,1}

    half v = 4.0h * SagaBayer2x2((half)lo.y, (half)lo.x)
           +        SagaBayer2x2((half)hi.y, (half)hi.x);

    return (v + 0.5h) * (1.0h / 16.0h);
}

#endif
