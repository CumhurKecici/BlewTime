Shader "Custom/AmbientOcclusion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off
        // ------------------------------------------------------------------
        // Occlusion
        // ------------------------------------------------------------------

        // 0 - Low
        Pass
        {
            Name "AO_Occlusion_Low"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output strucutre (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "AO.hlsl"

            #pragma vertex Vert
            #pragma fragment Occlusion_Blender
            
            ENDHLSL
        }

        // 1 - High
        Pass
        {
            Name "AO_Occlusion_High"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output strucutre (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "AO.hlsl"

            #pragma vertex Vert
            #pragma fragment Occlusion_High
            
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Bilateral Blur
        // ------------------------------------------------------------------

        // 2 - Horizontal
        Pass
        {
            Name "AO_Bilateral_HorizontalBlur"

            HLSLPROGRAM
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                // The Blit.hlsl file provides the vertex shader (Vert),
                // input structure (Attributes) and output strucutre (Varyings)
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
                #include "Blur.hlsl"

                #pragma vertex Vert
                #pragma fragment HorizontalBlur
            ENDHLSL
        }

        // 3 - Vertical
        Pass
        {
            Name "AO_Bilateral_VerticalBlur"

            HLSLPROGRAM
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                // The Blit.hlsl file provides the vertex shader (Vert),
                // input structure (Attributes) and output strucutre (Varyings)
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
                #include "Blur.hlsl"

                #pragma vertex Vert
                #pragma fragment VerticalBlur
            ENDHLSL
        }

        // 4 - Final
        Pass
        {
            Name "AO_Bilateral_FinalBlur"

            HLSLPROGRAM
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                // The Blit.hlsl file provides the vertex shader (Vert),
                // input structure (Attributes) and output strucutre (Varyings)
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
                #include "Blur.hlsl"

                #pragma vertex Vert
                #pragma fragment FinalBlur
            ENDHLSL
        }

        // 5 - Curvature
        Pass
        {
            Name "Curvature"

            HLSLPROGRAM
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                // The Blit.hlsl file provides the vertex shader (Vert),
                // input structure (Attributes) and output strucutre (Varyings)
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

                #pragma vertex Vert
                #pragma fragment ViewNormalsFrag

                TEXTURE2D_X(_CameraNormalsTexture);
                SAMPLER(sampler_CameraNormalsTexture);

                float4 Remap(float4 In)
                {
                    float2 InMinMax = float2(-1, 1);
                    float2 OutMinMax = float2(0, 1);
                    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
                }

                half4 ViewNormalsFrag (Varyings input) : SV_Target
                {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                    float4 color = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_CameraNormalsTexture, input.texcoord);
                    color.rgb = mul(color, (float3x3) UNITY_MATRIX_I_V);
                    color.a = 1;
                    color = Remap(color);
                    return color;
                }
            ENDHLSL
        }
    }
}
