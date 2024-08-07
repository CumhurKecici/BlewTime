SAMPLER(sampler_BlitTexture);

float4 _SourceSize;

half3 GetPackedNormal(half4 p)
{
    return p.gba * half(2.0) - half(1.0);
}

half CompareNormal(half3 d1, half3 d2)
{
    half kGeometryCoeff = half(0.8);
    return smoothstep(kGeometryCoeff, half(1.0), dot(d1, d2));
}

half GetPackedCavity(half4 p)
{
    return p.r;
}

half GetPackedEdges(half4 p)
{
    return p.g;
}

// Geometry-aware separable bilateral filter
half4 Blur(const float2 uv, const float2 delta) : SV_Target
{
    half4 p0 =  _BlitTexture.Sample(sampler_BlitTexture, uv                       );
    half4 p1a = _BlitTexture.Sample(sampler_BlitTexture, uv - delta * 1.3846153846);
    half4 p1b = _BlitTexture.Sample(sampler_BlitTexture, uv + delta * 1.3846153846);
    half4 p2a = _BlitTexture.Sample(sampler_BlitTexture, uv - delta * 3.2307692308);
    half4 p2b = _BlitTexture.Sample(sampler_BlitTexture, uv + delta * 3.2307692308);

    half3 n0 = GetPackedNormal(p0);

    half w0  =                                           half(0.2270270270);
    half w1a = CompareNormal(n0, GetPackedNormal(p1a)) * half(0.3162162162);
    half w1b = CompareNormal(n0, GetPackedNormal(p1b)) * half(0.3162162162);
    half w2a = CompareNormal(n0, GetPackedNormal(p2a)) * half(0.0702702703);
    half w2b = CompareNormal(n0, GetPackedNormal(p2b)) * half(0.0702702703);

    half cavity = half(0.0);
    cavity += GetPackedCavity(p0)  * w0;
    cavity += GetPackedCavity(p1a) * w1a;
    cavity += GetPackedCavity(p1b) * w1b;
    cavity += GetPackedCavity(p2a) * w2a;
    cavity += GetPackedCavity(p2b) * w2b;
    cavity *= rcp(w0 + w1a + w1b + w2a + w2b);

    half edges = half(0.0);
    edges += GetPackedEdges(p0)  * w0;
    edges += GetPackedEdges(p1a) * w1a;
    edges += GetPackedEdges(p1b) * w1b;
    edges += GetPackedEdges(p2a) * w2a;
    edges += GetPackedEdges(p2b) * w2b;
    edges *= rcp(w0 + w1a + w1b + w2a + w2b);

    return float4(cavity, edges, 0, 1);
}

// Geometry-aware bilateral filter (single pass/small kernel)
half4 BlurSmall(const float2 uv, const float2 delta)
{
    half4 p0 = _BlitTexture.Sample(sampler_BlitTexture, uv                            );
    half4 p1 = _BlitTexture.Sample(sampler_BlitTexture, uv + float2(-delta.x, -delta.y));
    half4 p2 = _BlitTexture.Sample(sampler_BlitTexture, uv + float2( delta.x, -delta.y));
    half4 p3 = _BlitTexture.Sample(sampler_BlitTexture, uv + float2(-delta.x,  delta.y));
    half4 p4 = _BlitTexture.Sample(sampler_BlitTexture, uv + float2( delta.x,  delta.y));

    half3 n0 = GetPackedNormal(p0);

    half w0 = half(1.0);
    half w1 = CompareNormal(n0, GetPackedNormal(p1));
    half w2 = CompareNormal(n0, GetPackedNormal(p2));
    half w3 = CompareNormal(n0, GetPackedNormal(p3));
    half w4 = CompareNormal(n0, GetPackedNormal(p4));

    half cavity = half(0.0);
    cavity += GetPackedCavity(p0) * w0;
    cavity += GetPackedCavity(p1) * w1;
    cavity += GetPackedCavity(p2) * w2;
    cavity += GetPackedCavity(p3) * w3;
    cavity += GetPackedCavity(p4) * w4;
    cavity *= rcp(w0 + w1 + w2 + w3 + w4);

    half edges = half(0.0);
    edges += GetPackedEdges(p0) * w0;
    edges += GetPackedEdges(p1) * w1;
    edges += GetPackedEdges(p2) * w2;
    edges += GetPackedEdges(p3) * w3;
    edges += GetPackedEdges(p4) * w4;
    edges *= rcp(w0 + w1 + w2 + w3 + w4);

    return float4(cavity, edges, 0, 0);
}


half4 HorizontalBlur(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 _SourceSize = _ScreenParams;
    const float2 uv = input.texcoord;
    const float2 delta = float2(_SourceSize.z - 1.0, 0);
    return Blur(uv, delta);
}

half4 VerticalBlur(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 _SourceSize = _ScreenParams;
    const float2 uv = input.texcoord;
    const float2 delta = float2(0, _SourceSize.z - 1.0);
    return Blur(uv, delta);
}

half4 FinalBlur(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 _SourceSize = _ScreenParams;
    const float2 uv = input.texcoord;
    const float2 delta = _SourceSize.zw - float2(1, 1);
    return half(1.0) - BlurSmall(uv, delta);
}

