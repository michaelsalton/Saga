struct GrassVaryings
{
    float4 positionHCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    float along : TEXCOORD2;
    half fogFactor : TEXCOORD3;
};

GrassVaryings GrassVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
{
    GrassVertex grassVertex = EvaluateGrassVertex(vertexID, instanceID);

    GrassVaryings OUT;
    OUT.positionHCS = TransformWorldToHClip(grassVertex.positionWS);
    OUT.positionWS = grassVertex.positionWS;
    OUT.normalWS = half3(grassVertex.normalWS);
    OUT.along = grassVertex.along;
    OUT.fogFactor = half(ComputeFogFactor(OUT.positionHCS.z));
    return OUT;
}

half3 GrassTransmission(InputData inputData, half3 bladeNormalWS, half3 albedo)
{
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    half shadow = mainLight.shadowAttenuation;
    // Lambert for light striking the far side of the sheet and diffusing out through ours
    half backLambert = saturate(dot(-bladeNormalWS, mainLight.direction));
    // light passing through keeps heading roughly along its own direction, so it's brightest looking into the sun
    half forwardScatter = pow(saturate(dot(inputData.viewDirectionWS, -mainLight.direction)), half(_ForwardScatterPower)) * half(_ForwardScatter);
    return mainLight.color * (mainLight.distanceAttenuation * shadow) * albedo * half(_Translucency) * (backLambert + forwardScatter);
}

half4 GrassFrag(GrassVaryings IN, bool isFrontFace : SV_IsFrontFace) : SV_Target
{
    // one sheet, two sides: the back face needs the opposite normal or it lights as if turned away from the sun
    half3 bladeNormalWS = normalize(IN.normalWS) * (isFrontFace ? 1.0h : -1.0h);
    // pulls the randomly facing cards toward the ground they stand on so the field shades as one surface
    half3 normalWS = normalize(lerp(bladeNormalWS, half3(0, 1, 0), half(_GroundNormalBlend)));

    InputData inputData = (InputData)0;
    inputData.positionWS = IN.positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
    // the clustered light loop and screen-space shadows look pixels up by screen position
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
    inputData.shadowMask = half4(1, 1, 1, 1);
    // sky behind the blade leaks through it as well as reflecting off the front
    inputData.bakedGI = SampleSH(normalWS) + SampleSH(-bladeNormalWS) * half(_Translucency);

    half3 albedo = lerp(_RootColor.rgb, _BaseColor.rgb, IN.along);
    half3 color = SagaStandardLighting(inputData, bladeNormalWS, albedo, 0.0h, half(_Roughness), 1.0h);
    color += GrassTransmission(inputData, bladeNormalWS, albedo);
    return half4(MixFog(color, IN.fogFactor), 1.0h);
}
