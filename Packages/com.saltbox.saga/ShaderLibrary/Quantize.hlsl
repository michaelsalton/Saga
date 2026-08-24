#ifndef SAGA_QUANTIZE_INCLUDED
#define SAGA_QUANTIZE_INCLUDED

half SagaSoftQuantize(half s, half softness, half bias)
{
    half x = s + bias;

    [branch] if (softness < 0.002h)
        return floor(x);

    x -= softness * 0.5h;
    half i = floor(x);
    return i + smoothstep(1.0h - softness, 1.0h, x - i);
}

#endif
