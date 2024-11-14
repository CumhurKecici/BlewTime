using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Linq;

[DisallowMultipleRendererFeature("Explosion Simulator")]
public class ExplosionSimulator : ScriptableRendererFeature
{
    public Shader m_shader;
    private ExplosionSimulatorRenderPass m_explosionSimulator;
    private Material m_material;

    public override void Create()
    {
        if (m_shader == null)
            return;

        m_material = CoreUtils.CreateEngineMaterial(m_shader);

        if (m_explosionSimulator == null)
            m_explosionSimulator = new ExplosionSimulatorRenderPass(ref m_material);

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
        private Material m_material;

        public ExplosionSimulatorRenderPass(ref Material material)
        {
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
            internal TextureHandle blendTexture;
            internal TextureHandle blendTexture2;
            internal TextureHandle blendTexture3;
            internal TextureHandle blendTexture4;

            public override void Reset()
            {
                depthTexture = TextureHandle.nullHandle;
                blendTexture = TextureHandle.nullHandle;
                blendTexture2 = TextureHandle.nullHandle;
                blendTexture3 = TextureHandle.nullHandle;
                blendTexture4 = TextureHandle.nullHandle;
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
                //RenderTextureDescriptor depthDesc = cameraData.cameraTargetDescriptor;
                //depthDesc.msaaSamples = 1;


                ExplosionSimulatorContext esContext = frameData.Create<ExplosionSimulatorContext>();
                esContext.depthTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthDesc, "_SimulationDepthTexture", true);
                esContext.blendTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_SimulationNormalsTexture1", true);
                esContext.blendTexture2 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_SimulationNormalsTexture2", true);
                esContext.blendTexture3 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_SimulationNormalsTexture3", true);
                esContext.blendTexture4 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_SimulationNormalsTexture", true);

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
                builder.SetRenderAttachment(esContext.blendTexture, 0);
                builder.SetGlobalTextureAfterPass(esContext.blendTexture, Shader.PropertyToID("_SimulationNormalsTexture1"));

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => NormalsPass(data, context));
            }

            using (var builder = renderGraph.AddRasterRenderPass(passName + " - Blend 1", out ExplosionSimulatorPassData passData))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                ExplosionSimulatorContext esContext = frameData.Get<ExplosionSimulatorContext>();
                passData.src = esContext.blendTexture;

                builder.UseTexture(esContext.blendTexture);
                builder.SetRenderAttachment(esContext.blendTexture2, 0);
                builder.SetGlobalTextureAfterPass(esContext.blendTexture2, Shader.PropertyToID("_SimulationNormalsTexture"));

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => BlendPass(data, context));
            }

            /*using (var builder = renderGraph.AddRasterRenderPass(passName + " - Blend 2", out ExplosionSimulatorPassData passData))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                ExplosionSimulatorContext esContext = frameData.Get<ExplosionSimulatorContext>();
                passData.src = esContext.blendTexture2;

                builder.UseTexture(esContext.blendTexture2);
                builder.SetRenderAttachment(esContext.blendTexture3, 0);
                //builder.SetGlobalTextureAfterPass(esContext.blendTexture3, Shader.PropertyToID("_SimulationNormalsTexture3"));

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => BlendPass(data, context));
            }

            using (var builder = renderGraph.AddRasterRenderPass(passName + " - Blend 3", out ExplosionSimulatorPassData passData))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                ExplosionSimulatorContext esContext = frameData.Get<ExplosionSimulatorContext>();
                passData.src = esContext.blendTexture3;

                builder.UseTexture(esContext.blendTexture3);
                builder.SetRenderAttachment(esContext.blendTexture4, 0);
                builder.SetGlobalTextureAfterPass(esContext.blendTexture4, Shader.PropertyToID("_SimulationNormalsTexture"));

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => BlendPass(data, context));
            }*/

            /* using (var builder = renderGraph.AddRasterRenderPass(passName + " - Blend 4", out ExplosionSimulatorPassData passData))
             {
                 UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                 ExplosionSimulatorContext esContext = frameData.Get<ExplosionSimulatorContext>();
                 passData.src = esContext.blendTexture4;

                 //builder.UseTexture(esContext.depthTexture);
                 builder.AllowGlobalStateModification(true);
                 builder.SetRenderAttachment(esContext.blendTexture5, 0);
                 builder.SetGlobalTextureAfterPass(esContext.blendTexture5, Shader.PropertyToID("_SimulationNormalsTexture5"));

                 // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                 builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => BlendPass(data, context));
             }

             using (var builder = renderGraph.AddRasterRenderPass(passName + " - Blend 5", out ExplosionSimulatorPassData passData))
             {
                 UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                 ExplosionSimulatorContext esContext = frameData.Get<ExplosionSimulatorContext>();
                 passData.src = esContext.blendTexture5;

                 //builder.UseTexture(esContext.depthTexture);
                 builder.AllowGlobalStateModification(true);
                 builder.SetRenderAttachment(esContext.blendTexture6, 0);
                 builder.SetGlobalTextureAfterPass(esContext.blendTexture6, Shader.PropertyToID("_SimulationNormalsTexture6"));

                 // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                 builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => BlendPass(data, context));
             }

             using (var builder = renderGraph.AddRasterRenderPass(passName + " - Blend 6", out ExplosionSimulatorPassData passData))
             {
                 UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                 ExplosionSimulatorContext esContext = frameData.Get<ExplosionSimulatorContext>();
                 passData.src = esContext.blendTexture6;

                 //builder.UseTexture(esContext.depthTexture);
                 builder.AllowGlobalStateModification(true);
                 builder.SetRenderAttachment(esContext.blendTexture7, 0);
                 builder.SetGlobalTextureAfterPass(esContext.blendTexture7, Shader.PropertyToID("_SimulationNormalsTexture7"));

                 // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                 builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => BlendPass(data, context));
             }

             using (var builder = renderGraph.AddRasterRenderPass(passName + " - Blend 7", out ExplosionSimulatorPassData passData))
             {
                 UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                 ExplosionSimulatorContext esContext = frameData.Get<ExplosionSimulatorContext>();
                 passData.src = esContext.blendTexture7;

                 //builder.UseTexture(esContext.depthTexture);
                 builder.AllowGlobalStateModification(true);
                 builder.SetRenderAttachment(esContext.blendTexture8, 0);
                 builder.SetGlobalTextureAfterPass(esContext.blendTexture8, Shader.PropertyToID("_SimulationNormalsTexture8"));

                 // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                 builder.SetRenderFunc((ExplosionSimulatorPassData data, RasterGraphContext context) => BlendPass(data, context));
             }*/

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
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), m_material, 1);
        }

        // NOTE: This method is part of the compatibility rendering path, please use the Render Graph API above instead.
        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {

        }
    }

}
