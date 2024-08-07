texture2D _ViewNormalsTexture;
sampler sampler_ViewNormalsTexture;

float _CurvatureValley;
float _CurvatureRidge;

float4 _SourceSize;

float3 SampleViewNormal(float2 uv)
{
    return _ViewNormalsTexture.Sample(sampler_ViewNormalsTexture, uv).rgb;
}

float CurvatureSoftClamp(float curvature, float control)
{
    if (curvature < 0.5 / control) {
        return curvature * (1.0 - curvature * control);
    }
    return 0.25 / control;
}

void CurvatureCompute(float2 uv, out float curvature)
{
    curvature = 0.0;

    float3 offset = float3(_SourceSize.zw, 0.0);
    float normal_up =  SampleViewNormal(uv + offset.zy).g;
    float normal_down = SampleViewNormal(uv - offset.zy).g;
    float normal_right = SampleViewNormal(uv + offset.xz).r;
    float normal_left = SampleViewNormal(uv - offset.xz).r;

    float normal_diff = (normal_up - normal_down) + (normal_right - normal_left);

    const float curvature_valley = 0.7 / max(sqrt(_CurvatureValley), 1e-4f);
    const float curvature_ridge = 0.2 / max(sqrt(_CurvatureRidge), 1e-4f);

    if (normal_diff < 0) {
        curvature = -2.0 * CurvatureSoftClamp(-normal_diff, curvature_valley);
    }
    else {
        curvature = 2.0 * CurvatureSoftClamp(normal_diff, curvature_ridge);
    }
}

void CurvatureCompute_float(float2 uv, out float curvature)
{
     CurvatureCompute(uv, curvature);
}

void CurvatureCompute_half(float2 uv, out float curvature)
{
    CurvatureCompute(uv, curvature);
}


