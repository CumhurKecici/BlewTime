// Camera depth texture for access
TEXTURE2D_X(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

//Camera normals texture for access
TEXTURE2D_X(_CameraNormalsTexture);
SAMPLER(sampler_CameraNormalsTexture);

//Jitter texture for access
TEXTURE2D_X(_JitterTexture);

float4 _SourceSize;

//Sample Settings
float4 _AOSamples[512];
int _SampleCount;

//Cavity Params
float _CavityDistance;
float _CavityAttenuation;
float _CavityValley;
float _CavityRidge;

//Jitter Params
float _CavityJitterScale;

float SampleDepth(float2 uv)
{
    return _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv).r;
}

float3 SampleNormal(float2 uv)
{
    return _CameraNormalsTexture.Sample(sampler_CameraNormalsTexture, uv).rgb;
}

float3 SampleViewNormal(float2 uv)
{
    float3 normal = _CameraNormalsTexture.Sample(sampler_CameraNormalsTexture, uv).rgb;
    return mul(normal, UNITY_MATRIX_I_V);
}

float3 GetPositionWS(float2 uv, float depth)
{
    return GetAbsolutePositionWS(ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP));
}

float2 GetScreenCoordsFromVS(float3 positionVS)
{
    float4 positionCS = TransformWViewToHClip(positionVS);
    float4 screenPosition = ComputeScreenPos(positionCS);
    return screenPosition.xy / screenPosition.w;
}

// Trigonometric function utility
float2 CosSin(float theta)
{
    float sn, cs;
    sincos(theta, sn, cs);
    return float2(cs, sn);
}

// Pseudo random number generator with 2D coordinates
float UVRandom(float u, float v)
{
    float f = dot(float2(12.9898, 78.233), float2(u, v));
    return frac(43758.5453 * sin(f));
}

// Sample point picker
float3 PickSamplePoint(float2 uv, int index)
{
    // This was added to avoid a NVIDIA driver issue.
    float randAddon = uv.x * 1e-10;

    float2 positionSS = uv * _SourceSize.xy;
    float gn = InterleavedGradientNoise(positionSS, index);
    float u = frac(UVRandom(0.0, index + randAddon) + gn) * 2.0 - 1.0;
    float theta = (UVRandom(1.0, index + randAddon) + gn) * TWO_PI;
    return float3(CosSin(theta) * sqrt(1.0 - u * u), u);
}

float4 Occlusion_Blender (Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float cavities = 0.0;
    float edges = 0.0;
    float curvature = 0.0;
    
    float2 screenco = input.texcoord;

    float depth = SampleDepth(screenco);
    
    /* Early out if background and in front. */
    if (depth == 1.0 || depth == 0.0) {
        return 0;
    }    

    float3 positionWS = GetPositionWS(screenco, depth);
    float3 positionVS = TransformWorldToView(positionWS);

    float3 normal = SampleViewNormal(screenco);

    float4 noise = _JitterTexture.Sample(sampler_PointRepeat, (screenco * _SourceSize.xy) * rcp(_CavityJitterScale));

    /* find the offset in screen space by multiplying a point
    * in camera space at the depth of the point by the projection matrix. */
    float2 offset;
    float homcoord = UNITY_MATRIX_P[2][3] * positionVS.z + UNITY_MATRIX_P[3][3];
    offset.x = UNITY_MATRIX_P[0][0] * _CavityDistance / homcoord;
    offset.y = UNITY_MATRIX_P[1][1] * _CavityDistance / homcoord;
    /* convert from -1.0...1.0 range to 0.0..1.0 for easy use with texture coordinates */
    offset *= 0.5;

    /* NOTE: Putting noise usage here to put some ALU after texture fetch. */
    float2 rotX = noise.rg;
    float2 rotY = float2(-rotX.y, rotX.x);
    
    [loop]
    for (int i = 0; i <  int(_SampleCount); i++) {
      /* sample_coord.xy is sample direction (normalized).
       * sample_coord.z is sample distance from disk center. */
      float3 sample_coord = _AOSamples[i].xyz;
      /* Rotate with random direction to get jittered result. */
      float2 dir_jittered = float2(dot(sample_coord.xy, rotX), dot(sample_coord.xy, rotY));
      dir_jittered.xy *= sample_coord.z + noise.b;

      float2 uvcoords = screenco + dir_jittered * offset;
      /* Out of screen case. */
      if(any(abs(uvcoords - 0.5) > float2(0.5, 0.5))) {
        continue;
      }
      /* Sample depth. */
      float s_depth = SampleDepth(uvcoords);
      /* Handle Background case */
      bool is_background = (s_depth == 1.0);
      /* This trick provide good edge effect even if no neighbor is found. */
      s_depth = (is_background) ? depth : s_depth;
      float3 s_pos =  TransformWorldToView(GetPositionWS(uvcoords, s_depth));//  ViewSpacePosAtScreenUV(uvcoords); //ViewSpacePosAtScreenUV
  
      if (is_background) {
        s_pos.z -= _CavityDistance;
      }
  
      float3 dir = positionVS - s_pos;
      float len = length(dir);
      float f_cavities = -dot(dir, normal);
      float f_edge = -f_cavities;
      float f_bias = 0.05 * len + 0.0001;
      
      float attenuation = 1.0 / (len * (1.0 + len * len * _CavityAttenuation));
  
      /* use minor bias here to avoid self shadowing */
      if (f_cavities > -f_bias) {
        cavities += f_cavities * attenuation;
      }

      if (f_edge > f_bias) {
        edges += f_edge * attenuation;
      }
    }

    cavities *= rcp(_SampleCount);
    edges *= rcp(_SampleCount);

    /* don't let cavity wash out the surface appearance */
    cavities = clamp(cavities * _CavityValley, 0.0, 1.0);
    edges = edges * _CavityRidge;

    return float4(cavities, edges, 0.0, 0.0);
}

float DistanceFromPlane(float3 p, float4 plane)
{
    return dot(float4(p, 1.0), plane);
}

float4 Occlusion_High(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float cavities = 0;
    float edges = 0;

    float2 uv = input.texcoord;

    float depth = SampleDepth(uv);

    /* Early out if background and in front. */
    if (depth == 1.0 || depth == 0.0) {
        return 0;
    }    

    float3 positionWS = GetPositionWS(uv, depth);
    float3 positionVS = TransformWorldToView(positionWS);

    //float3 positionOS = TransformWorldToObject(positionWS);
    float4 positionCS = TransformWorldToHClip(positionWS);

    float3 viewNormal = SampleViewNormal(uv);

    float falloff = rcp(distance(_WorldSpaceCameraPos, positionVS));

    [loop]
    for (int i = 0; i < int(_SampleCount); i++)
    {
        // Adjusting sammple point offset
        float3 samplePoint = _JitterTexture.Sample(sampler_LinearRepeat, (uv * _SourceSize.xy) * rcp(_CavityJitterScale)); 
        samplePoint = PickSamplePoint(uv, i); 
        //samplePoint +=_AOSamples[i].xyz;
        //samplePoint = clamp(samplePoint, float3(-1.0,-1.0,-1.0), float3(1.0,1.0,1.0));
        samplePoint *= sqrt((i + 1.0) * rcp(_SampleCount));

        // Rotating offset by view normal
        // Positive view normals enhance edges
        // Negative view normals enhance cavities
        float3 samplePoint_cavity = clamp(faceforward(samplePoint, -viewNormal, samplePoint), float3(-1.0,-1.0,-1.0), float3(1.0,1.0,1.0)); 
        float3 samplePoint_edge = faceforward(samplePoint, viewNormal, samplePoint); 

        // Add offset to current view positions
        float3 s_position_cavity = positionVS + samplePoint_cavity * _CavityDistance;
        float3 s_position_edge = positionVS + samplePoint_edge * _CavityDistance;

        // Uvcoords for sample points
        float2 uv_s_cavity = GetScreenCoordsFromVS(s_position_cavity);
        float2 uv_s_edge = GetScreenCoordsFromVS(s_position_edge);
        
        // Calculate occlusion of cavities. If uv is not out of screen.
        if(any(abs(uv_s_cavity - 0.5) < float2(0.5, 0.5)))
        {
            // Sample depth.
            float s_depth = SampleDepth(uv_s_cavity);
            // Handle Background case
            bool is_background = (s_depth == 1.0);
            // This trick provide good edge effect even if no neighbor is found.
            s_depth = (is_background) ? depth : s_depth;

            float3 s_positionWS = GetPositionWS(uv_s_cavity, s_depth);
            float3 vpos = TransformWorldToView(s_positionWS);

            if (is_background) {
                vpos.z -= _CavityDistance;
            }
        
            float3 dir = vpos - positionVS;
            float len = length(dir);
            float f_cavities = dot(dir, viewNormal);
            float f_bias = 0.05 * len + 0.0001;
        
            float attenuation = 1.0 / (len * (1.0 + len * len * _CavityAttenuation));

            if(f_cavities > -f_bias)
                cavities += f_cavities * attenuation;

        }

        // Calculate occlusion of edges. If uv is not out of screen.
        if(any(abs(uv_s_edge - 0.5) < float2(0.5, 0.5)))
        {
            // Sample depth.
            float s_depth = SampleDepth(uv_s_edge);
            // Handle Background case
            bool is_background = (s_depth == 1.0);
            // This trick provide good edge effect even if no neighbor is found.
            s_depth = (is_background) ? depth : s_depth;
            
            float3 s_positionWS = GetPositionWS(uv_s_edge, s_depth);
            float3 vpos = TransformWorldToView(s_positionWS);

            if (is_background) {
                vpos.z -= _CavityDistance;
            }
        
            float3 dir = vpos - positionVS;
            float len =  length(dir);
            float f_edges = -dot(dir, viewNormal); // Negative dot value for edge effect
            float f_bias = 0.05 * len + 0.0001;
        
            float attenuation = 1.0 / (len * (1.0 + len * len * _CavityAttenuation));

            if(f_edges > f_bias)
                edges += f_edges * attenuation;// * falloff;
        }
    }

    cavities *= rcp(_SampleCount);
    edges *= rcp(_SampleCount);


    /* don't let cavity wash out the surface appearance */
    cavities = clamp(cavities * _CavityValley, 0.0, 1.0);
    edges = edges * _CavityRidge;

    return float4(cavities, edges, 0.0, 0.0);
}

