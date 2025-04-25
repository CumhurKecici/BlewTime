Shader "ExplosionSimulation"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off
        Pass
        {
            Name "ViewNormals"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output strucutre (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl" 

            #pragma vertex Vert
            #pragma fragment frag

            TEXTURE2D_X(_SimulationDepthTexture);
            SAMPLER(sampler_SimulationDepthTexture);

            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            TEXTURE2D_X(_ViewNormalsTexture);
            SAMPLER(sampler_ViewNormalsTexture);

            
            bool DepthCheck(float2 uv)
            {
                float o_depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv).r;
                float s_depth = _SimulationDepthTexture.Sample(sampler_SimulationDepthTexture, uv).r;
                
                if(o_depth == s_depth)
                    return true;
                else
                    return false;
            }

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 color = float4(0,0,0,1);
                
                if( DepthCheck(input.texcoord))
                    color.rgb = _ViewNormalsTexture.Sample(sampler_ViewNormalsTexture, input.texcoord).rgb;

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Blur"

            HLSLPROGRAM
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                // The Blit.hlsl file provides the vertex shader (Vert),
                // input structure (Attributes) and output strucutre (Varyings)
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

                #pragma vertex Vert
                #pragma fragment GaussianBlur

                //TEXTURE2D_X(_BlitTexture);
                SAMPLER(sampler_BlitTexture);

                // GAUSSIAN BLUR SETTINGS {{{
                float Directions;   // = 16.0; // BLUR DIRECTIONS (Default 16.0 - More is better but slower)
                float Quality;      // = 3.0; // BLUR QUALITY (Default 4.0 - More is better but slower)
                float Size;         // = 4.0; // BLUR SIZE (Radius) (Default 8.0)
                // GAUSSIAN BLUR SETTINGS }}}
                
                //Ref: https://www.shadertoy.com/view/Xltfzj
                half4 GaussianBlur(Varyings input) : SV_Target
                {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                    float Pi = 6.28318530718; // Pi*2
    
                    // GAUSSIAN BLUR SETTINGS {{{
                    //float Directions = 16.0; // BLUR DIRECTIONS (Default 16.0 - More is better but slower)
                    //float Quality = 3.0; // BLUR QUALITY (Default 4.0 - More is better but slower)
                    //float Size = 4.0; // BLUR SIZE (Radius) (Default 8.0)
                    // GAUSSIAN BLUR SETTINGS }}}
                
                    float2 Radius = Size / _ScreenParams.xy;
                    
                    // Normalized pixel coordinates (from 0 to 1)
                    float2 uv = input.texcoord;// fragCoord / _ScreenParams.xy;
                    // Pixel colour
                    float4 color = _BlitTexture.Sample(sampler_BlitTexture, uv).r;
                    
                    // Blur calculations
                    for( float d = 0.0f; d < Pi; d += Pi / Directions)
                    {
                        for(float i=1.0f / Quality; i <= 1.0f; i += 1.0f / Quality)
                        {
                            color += _BlitTexture.Sample(sampler_BlitTexture, uv + float2(cos(d), sin(d)) * Radius * i);		
                        }
                    }
                    
                    // Output to screen
                    color /= Quality * Directions - 15.0f;

                    return color;
                }
            ENDHLSL
        }

        Pass
        {
            Name "Metaball"

            HLSLPROGRAM
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                // The Blit.hlsl file provides the vertex shader (Vert),
                // input structure (Attributes) and output strucutre (Varyings)
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

                #pragma vertex Vert
                #pragma fragment MetaballFrag

                //TEXTURE2D_X(_BlitTexture);
                SAMPLER(sampler_BlitTexture);

                float3 GetPositionWS(float2 uv, float depth)
                {
                    return GetAbsolutePositionWS(ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP));
                }

                float4 Posterize(float4 In, float4 Steps)
                {
                    return floor(In / (1 / Steps)) * (1 / Steps);
                }

                float NormalizeData(float max, float min, float value)
                {
                    return (value - min) / (max - min);
                }

                half4 MetaballFrag(Varyings input) : SV_Target
                {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                    float2 uv = input.texcoord;
                    float depth = _BlitTexture.Sample(sampler_BlitTexture, uv).r;
                    float3 camPos = GetPositionWS(uv, 0000000.0);
                    float3 position = GetPositionWS(uv, depth);    
                    
                    //camPos.xy = position.xy;
                    float distToCam = length(_WorldSpaceCameraPos - position) - 2.0;
                    //distToCam = distance(_WorldSpaceCameraPos, position);
                    
                    float minDist = 0;
                    float maxDist = 10;

                    float normalized = (distToCam - minDist) / (maxDist - minDist);

                    float4 color = lerp(1,0,normalized);

                    //color = Posterize(color, 4);
                    //float4 color = _BlitTexture.Sample(sampler_BlitTexture, input.texcoord).r;

                    return color;
                }
            ENDHLSL
        }
    }
}