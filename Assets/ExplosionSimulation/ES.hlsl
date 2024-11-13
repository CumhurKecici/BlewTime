float GetDepth(float2 uv)
{
    return _SimulationDepthTexture.Sample(sampler_SimulationDepthTexture,uv).r;
}

float3 EdgeDetect(float x, float y, float Width, float Height,  float2 mainPixel) {
    float tleft  = GetDepth(mainPixel + float2(-x / Width, y / Height));
    float left   = GetDepth(mainPixel + float2(-x / Width, 0));
    float bleft  = GetDepth(mainPixel + float2(-x / Width,-y / Height));
    float top    = GetDepth(mainPixel + float2(0, y / Height));
    float bottom = GetDepth(mainPixel + float2(0, -y / Height));
    float tright = GetDepth(mainPixel + float2(x / Width, y / Height));
    float right  = GetDepth(mainPixel + float2(x / Width, 0));
    float bright = GetDepth(mainPixel + float2(x / Width, -y / Height));
    
    float gx = tleft  + 20.0*left + bleft - tright - 20.0*right - bright;
    float gy = -left  - 20.0*top - tright + bleft + 20.0*bottom + bright;
    
    float color = sqrt( (gx*gx) + (gy*gy) );
    return float3(color, color, color);
}

void EdgeDetect_float(float x, float y, float Width, float Height,  float2 mainPixel, out float3 Out)
{
    Out = EdgeDetect(x, y, Width, Height, mainPixel);
}


void Linear01DepthFromNear_float(float depth, float4 zBufferParam, out float Out)
{
    #if UNITY_REVERSED_Z
    Out = (1.0 - depth) / (zBufferParam.x * depth + zBufferParam.y);
    #else
    Out = depth / (zBufferParam.x * depth + zBufferParam.y);
    #endif
}

void Linear01Depth_float(float depth, out float Out)
{
    Out = 1.0 / (_ZBufferParams.x * depth + _ZBufferParams.y);
}