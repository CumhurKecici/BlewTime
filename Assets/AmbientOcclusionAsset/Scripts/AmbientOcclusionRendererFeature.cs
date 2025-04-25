using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using System;

[DisallowMultipleRendererFeature("Ambient Occlusion")]
public class AmbientOcclusionRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private AmbientOcclusionSettings m_settings = new AmbientOcclusionSettings();
    private AmbientOcclusionRenderPass m_renderPass;
    private Material m_material;

    public override void Create()
    {
        m_material = CoreUtils.CreateEngineMaterial(Shader.Find("Custom/AmbientOcclusion"));

        //Creating pass
        if (m_renderPass == null)
        {
            m_renderPass = new AmbientOcclusionRenderPass(ref m_material, ref m_settings);
            m_renderPass.ConfigureInput(ScriptableRenderPassInput.Normal);
        }

        // Configures where the render pass should be injected.
        m_renderPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_renderPass);
    }

    protected override void Dispose(bool disposing)
    {
        m_renderPass = null;
        CoreUtils.Destroy(m_material);
    }

    class AmbientOcclusionRenderPass : ScriptableRenderPass
    {
        private AmbientOcclusionSettings m_settings;
        private Material m_material;

        private List<Texture2D> m_blueNoises;
        private Texture2D m_jitterTexture;
        private int m_jitterSampleCount;
        private List<Vector4> m_aoSamples;

        public AmbientOcclusionRenderPass(ref Material material, ref AmbientOcclusionSettings settings)
        {
            this.m_settings = settings;
            this.m_material = material;
            m_blueNoises = Resources.LoadAll<Texture2D>("Textures/BlueNoise256/").ToList();
        }

        // This class stores the data needed by the RenderGraph pass.
        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
        class AmbientOcclusionPassData
        {
            internal TextureHandle src;
        }

        class AmbientOcclusionPassContext : ContextItem
        {
            internal TextureHandle aoCalculationTexture;
            internal TextureHandle aoBlurHorizontalTexture;
            internal TextureHandle aoBlurVerticalTexture;
            internal TextureHandle ambientOcclusionTexture;

            public override void Reset()
            {
                aoCalculationTexture = TextureHandle.nullHandle;
            }
        }

        private void SetMaterialProperties(ref Material material)
        {
            //Sample Settings
            material.SetInt("_SampleCount", m_settings.Sample);
            GenerateSamples(m_settings.Sample);
            material.SetVectorArray("_AOSamples", m_aoSamples);

            //Jitter Settings
            material.SetFloat("_CavityJitterScale", m_settings.JitterScale);
            GenerateJitterTx(m_settings.Sample, m_settings.JitterScale);
            material.SetTexture("_JitterTexture", m_jitterTexture);

            //Cavity Settings
            material.SetFloat("_CavityDistance", m_settings.CavityDistance);
            material.SetFloat("_CavityAttenuation", m_settings.CavityAttenuation);
            material.SetFloat("_CavityRidge", m_settings.CavityRidge);
            material.SetFloat("_CavityValley", m_settings.CavityValley);
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            const string passName = "Ambient Occlusion Pass";

            RenderTextureDescriptor descriptor;

            using (var builder = renderGraph.AddRasterRenderPass(passName + " - Calculation", out AmbientOcclusionPassData passData))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                descriptor = cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                //Assigns source image
                passData.src = resourceData.activeColorTexture;

                //Context item to share data between passes
                AmbientOcclusionPassContext aoContext = frameData.GetOrCreate<AmbientOcclusionPassContext>();

                //Creating textures requires for pass
                aoContext.aoCalculationTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_AO_Calculation", true);
                aoContext.aoBlurHorizontalTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_Blur_Horizontal", true);
                aoContext.aoBlurVerticalTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_Blur_Vertical", true);
                aoContext.ambientOcclusionTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_AmbientOcclusionTexture", true);

                builder.AllowGlobalStateModification(true);

                //Sets destination texture
                builder.SetRenderAttachment(aoContext.aoCalculationTexture, 0);

                // Assigns the CalculationPass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((AmbientOcclusionPassData data, RasterGraphContext context) => CalculationPass(data, context));
            }

            using (var builder = renderGraph.AddRasterRenderPass(passName + " - Horizontal Blur", out AmbientOcclusionPassData passData))
            {
                AmbientOcclusionPassContext aoContext = frameData.Get<AmbientOcclusionPassContext>();
                passData.src = aoContext.aoCalculationTexture;
                builder.UseTexture(aoContext.aoCalculationTexture);

                builder.SetRenderAttachment(aoContext.aoBlurHorizontalTexture, 0);

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((AmbientOcclusionPassData data, RasterGraphContext context) => BlurHorizontalPass(data, context));
            }

            using (var builder = renderGraph.AddRasterRenderPass(passName + " - Vertical Blur", out AmbientOcclusionPassData passData))
            {
                AmbientOcclusionPassContext aoContext = frameData.Get<AmbientOcclusionPassContext>();
                passData.src = aoContext.aoBlurHorizontalTexture;

                builder.UseTexture(aoContext.aoBlurHorizontalTexture);
                builder.SetRenderAttachment(aoContext.aoBlurVerticalTexture, 0);

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((AmbientOcclusionPassData data, RasterGraphContext context) => BlurVerticalPass(data, context));
            }

            using (var builder = renderGraph.AddRasterRenderPass(passName + " - Final Blur", out AmbientOcclusionPassData passData))
            {
                AmbientOcclusionPassContext aoContext = frameData.Get<AmbientOcclusionPassContext>();
                passData.src = aoContext.aoBlurVerticalTexture;
                builder.UseTexture(aoContext.aoBlurVerticalTexture);

                builder.SetRenderAttachment(aoContext.ambientOcclusionTexture, 0);
                builder.SetGlobalTextureAfterPass(aoContext.ambientOcclusionTexture, Shader.PropertyToID("_AmbientOcclusionTexture"));

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((AmbientOcclusionPassData data, RasterGraphContext context) => BlurFinalPass(data, context));
            }

            using (var builder = renderGraph.AddRasterRenderPass(passName + " - ViewNormals", out AmbientOcclusionPassData passData))
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                passData.src = resourceData.activeColorTexture;

                TextureHandle viewNormals = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_ViewNormalsTexture", true);

                builder.SetRenderAttachment(viewNormals, 0);
                builder.SetGlobalTextureAfterPass(viewNormals, Shader.PropertyToID("_ViewNormalsTexture"));

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((AmbientOcclusionPassData data, RasterGraphContext context) => ViewNormalsPass(data, context));
            }
        }

        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
        // It is used to execute draw commands.
        void CalculationPass(AmbientOcclusionPassData data, RasterGraphContext context)
        {
            //CurvatureSettings
            context.cmd.SetGlobalFloat("_CurvatureRidge", m_settings.CurvatureRidge);
            context.cmd.SetGlobalFloat("_CurvatureValley", m_settings.CurvatureValley);
            context.cmd.SetGlobalVector("_SourceSize", new Vector4(Screen.width, Screen.height, math.rcp(Screen.width), math.rcp(Screen.height)));
            SetMaterialProperties(ref m_material);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), m_material, (int)m_settings.Method);
        }

        void BlurHorizontalPass(AmbientOcclusionPassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), m_material, 2);
        }

        void BlurVerticalPass(AmbientOcclusionPassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), m_material, 3);
        }

        void BlurFinalPass(AmbientOcclusionPassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), m_material, 4);
        }

        void ViewNormalsPass(AmbientOcclusionPassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), m_material, 5);
        }

        private void GenerateSamples(int ssao_samples)
        {
            if (m_aoSamples != null && m_aoSamples.Count == ssao_samples)
                return;

            if (m_aoSamples == null)
                m_aoSamples = new List<Vector4>();
            else
                m_aoSamples.Clear();

            // Calculation taken from blender source code.
            // Ref: https://github.com/blender/blender/blob/main/source/blender/draw/engines/workbench/workbench_effect_cavity.cc
            var rnd = Unity.Mathematics.Random.CreateFromIndex((uint)Time.deltaTime);
            float iteration_samples_inv = math.rcp(ssao_samples);
            /* Create disk samples using Hammersley distribution */
            for (int i = 0; i < ssao_samples; i++)
            {
                float it_add = (i / ssao_samples) * 0.499f;
                float r = math.fmod((i + 0.5f + it_add) * iteration_samples_inv, 1.0f);
                //Hammerslay1D value
                double dphi = math.reversebits((uint)i);
                float phi = Convert.ToSingle(dphi) * 2.0f * math.PI + it_add;
                float4 samples_buf = float4.zero;
                samples_buf.x = math.cos(phi);
                samples_buf.y = math.sin(phi);
                /* This deliberately distribute more samples
                 * at the center of the disk (and thus the shadow). */
                samples_buf.z = r;
                m_aoSamples.Add(samples_buf);
            }
        }

        private void GenerateJitterTx(int total_samples, int size)
        {
            if (m_jitterTexture != null && m_jitterSampleCount == total_samples && m_jitterTexture.texelSize == new Vector2(size, size))
                return;

            if (m_jitterTexture == null)
                m_jitterTexture = new Texture2D(size, size);
            else
                m_jitterTexture.Reinitialize(size, size);

            m_jitterSampleCount = total_samples;

            // Calculation taken from blender source code.
            // Ref: https://github.com/blender/blender/blob/main/source/blender/draw/engines/workbench/workbench_resources.cc

            float total_samples_inv = math.rcp(total_samples);

            var rnd = Unity.Mathematics.Random.CreateFromIndex((uint)Time.deltaTime);

            // Create blue noise jitter texture
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    var pixel = new float3(0);

                    float phi = m_blueNoises[0].GetPixel(x, y).a;
                    //phi = rnd.NextUInt(0, 1); //math.reversebits(rnd.NextUInt());
                    // This rotate the sample per pixels
                    pixel.x = math.cos(phi);
                    pixel.y = math.sin(phi);
                    // This offset the sample along its direction axis (reduce banding)
                    float bn = m_blueNoises[1].GetPixel(x, y).a - 0.5f;
                    bn = math.clamp(bn, -0.499f, 0.499f);
                    pixel.z = bn * total_samples_inv;

                    m_jitterTexture.SetPixel(x, y, new Color(pixel.x, pixel.y, pixel.z));
                }
            }

            m_jitterTexture.Apply();
        }

    }

}
