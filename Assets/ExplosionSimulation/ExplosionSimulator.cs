using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Linq;

[DisallowMultipleRendererFeature("Explosion Simulator")]
public class ExplosionSimulator : ScriptableRendererFeature
{
    public Shader m_shader;
    [SerializeField] private ExplosionSimulatorSettings m_settings = new ExplosionSimulatorSettings();
    private ExplosionSimulatorRenderPass m_explosionSimulator;
    private Material m_material;

    public override void Create()
    {
        if (m_shader == null)
            return;

        m_material = CoreUtils.CreateEngineMaterial(m_shader);

        if (m_explosionSimulator == null)
            m_explosionSimulator = new ExplosionSimulatorRenderPass(ref m_material, ref m_settings);

        // Configures where the render pass should be injected.
        m_explosionSimulator.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.camera == Camera.main)
            renderer.EnqueuePass(m_explosionSimulator);
    }

    protected override void Dispose(bool disposing)
    {
        m_explosionSimulator = null;
        CoreUtils.Destroy(m_material);

    }

    class ExplosionSimulatorRenderPass : ScriptableRenderPass
    {
        private ExplosionSimulatorSettings m_settings;
        private Material m_material;

        public ExplosionSimulatorRenderPass(ref Material material, ref ExplosionSimulatorSettings settings)
        {
            this.m_settings = settings;
            this.m_material = material;
        }

        // This class stores the data needed by the RenderGraph pass.
        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
        class ExplosionSimulatorPassData
        {
            internal TextureHandle src;
            internal RendererListHandle rendererListHandle;
        }

        class ExplosionSimulatorContext : ContextItem
        {
            internal TextureHandle depthTexture;
            internal TextureHandle viewNormalsTexture;
            internal TextureHandle blendTexture;
            internal TextureHandle metaballTexture;

            public override void Reset()
            {
                depthTexture = TextureHandle.nullHandle;
                //viewNormalsTexture = TextureHandle.nullHandle;
                //blendTexture = TextureHandle.nullHandle;
            }
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            const string passName = "Explosion Simulator";

            // This adds a raster render pass to the graph, specifying the name and the data type that will be passed to the ExecutePass function.
            using (var builder = renderGraph.AddRasterRenderPass(passName + " - Create Depth Texture", out ExplosionSimulatorPassData passData))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;

                var sortFlags = cameraData.defaultOpaqueSortFlags;
                RenderQueueRange renderQueueRange = RenderQueueRange.opaque;

                //For which objects going to be rendered
                FilteringSettings filterSettings = new FilteringSettings(renderQueueRange, LayerMask.GetMask(new string[] { "FX" }));

                //Only add needed render passes
                ShaderTagId[] forwardOnlyShaderTagIds = new ShaderTagId[] { new ShaderTagId("DepthNormalsOnly") };

                //Creating drawing settings for renderer list
                DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(forwardOnlyShaderTagIds.ToList(), renderingData, cameraData, lightData, sortFlags);

                //Creating rendererlist parameters
                var param = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
                //Set passData values for later access
                passData.rendererListHandle = renderGraph.CreateRendererList(param);

                RenderTextureDescriptor depthDesc = new RenderTextureDescriptor(cameraData.scaledWidth, cameraData.scaledHeight, RenderTextureFormat.Depth, cameraData.cameraTargetDescriptor.depthBufferBits);

                ExplosionSimulatorContext esContext = frameData.Create<ExplosionSimulatorContext>();
                esContext.depthTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthDesc, "_SimulationDepthTexture", true);
                esContext.viewNormalsTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_SimulationViewNormalsTexture", true);
                esContext.blendTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_SimulationNormalsTexture", true);
                esContext.metaballTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_SimulationMetaballTexture", true);
                //esContext.blendTexture4 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_SimulationNormalsTexture", true);

                builder.UseRendererList(passData.rendererListHandle);
                builder.SetRenderAttachmentDepth(esContext.depthTexture);
                builder.SetGlobalTextureAfterPass(esContext.depthTexture, Shader.PropertyToID("_SimulationDepthTexture"));

                builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => CreateDepthTexturePass(data, context));
            }

            using (var builder = renderGraph.AddRasterRenderPass(passName + " - View Normals", out ExplosionSimulatorPassData passData))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                ExplosionSimulatorContext esContext = frameData.Get<ExplosionSimulatorContext>();
                passData.src = esContext.depthTexture;

                builder.UseTexture(esContext.depthTexture);
                builder.SetRenderAttachment(esContext.viewNormalsTexture, 0);
                builder.SetGlobalTextureAfterPass(esContext.viewNormalsTexture, Shader.PropertyToID("_SimulationViewNormalsTexture"));

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => NormalsPass(data, context));
            }

            using (var builder = renderGraph.AddRasterRenderPass(passName + " - Blend", out ExplosionSimulatorPassData passData))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                ExplosionSimulatorContext esContext = frameData.Get<ExplosionSimulatorContext>();
                passData.src = esContext.viewNormalsTexture;

                builder.AllowGlobalStateModification(true);
                builder.UseTexture(esContext.viewNormalsTexture);
                builder.SetRenderAttachment(esContext.blendTexture, 0);
                builder.SetGlobalTextureAfterPass(esContext.blendTexture, Shader.PropertyToID("_SimulationNormalsTexture"));

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => BlendPass(data, context));
            }

            using (var builder = renderGraph.AddRasterRenderPass(passName + " - Metaball", out ExplosionSimulatorPassData passData))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                ExplosionSimulatorContext esContext = frameData.Get<ExplosionSimulatorContext>();
                passData.src = esContext.depthTexture;

                builder.AllowGlobalStateModification(true);
                builder.UseTexture(esContext.depthTexture);
                builder.SetRenderAttachment(esContext.metaballTexture, 0);
                builder.SetGlobalTextureAfterPass(esContext.metaballTexture, Shader.PropertyToID("_SimulationMetaballTexture"));

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => MetaballPass(data, context));
            }
        }

        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
        // It is used to execute draw commands.
        void CreateDepthTexturePass(ExplosionSimulatorPassData data, RasterGraphContext context)
        {
            context.cmd.ClearRenderTarget(true, true, Color.black);
            context.cmd.DrawRendererList(data.rendererListHandle);
        }

        void NormalsPass(ExplosionSimulatorPassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), m_material, 0);
        }

        void BlendPass(ExplosionSimulatorPassData data, RasterGraphContext context)
        {
            m_material.SetFloat("Directions", (float)m_settings.Directions);
            m_material.SetFloat("Quality", (float)m_settings.Quality);
            m_material.SetFloat("Size", m_settings.Size);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), m_material, 1);
        }

        void MetaballPass(ExplosionSimulatorPassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), m_material, 2);
        }
    }

}
