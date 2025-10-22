using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class TintRendererFeature : ScriptableRendererFeature
{
    private TintPass m_TintPass;

    public Material m_Material;

    /// <inheritdoc/>
    public override void Create()
    {
        m_TintPass = new TintPass(m_Material);

        // Configures where the render pass should be injected.
        m_TintPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        m_TintPass.requiresIntermediateTexture = true;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_TintPass);
    }

    // Use this class to pass around settings from the feature to the pass

    class TintPass : ScriptableRenderPass
    {
        const string m_PassName = "TintPass";
        Material m_Material;

        public TintPass(Material material)
        {
            m_Material = material;
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // UniversalResourceData contains all the texture handles used by the renderer, including the active color and depth textures
            // The active color and depth textures are the main color and depth buffers that the camera renders into
            var resourceData = frameData.Get<UniversalResourceData>();

            // This should never happen since we set m_Pass.requiresIntermediateTexture = true;
            // Unless you set the render event to AfterRendering, where we only have the BackBuffer. 
            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError($"Skipping render pass. TintRendererFeature requires an intermediate ColorTexture, we can't use the BackBuffer as a texture input.");
                return;
            }

            // The destination texture is created here, 
            // the texture is created with the same dimensions as the active color texture
            var source = resourceData.activeColorTexture;
            
            var destinationDesc = source.GetDescriptor(renderGraph);
            destinationDesc.name = $"CameraColor-{m_PassName}";
            destinationDesc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, m_Material, 0);
            renderGraph.AddBlitPass(para, passName: m_PassName);

            // FrameData allows to get and set internal pipeline buffers. Here we update the CameraColorBuffer to the texture that we just wrote to in this pass. 
            // Because RenderGraph manages the pipeline resources and dependencies, following up passes will correctly use the right color buffer.
            // This optimization has some caveats. You have to be careful when the color buffer is persistent across frames and between different cameras, such as in camera stacking.
            //  In those cases you need to make sure your texture is an RTHandle and that you properly manage the lifecycle of it.
            resourceData.cameraColor = destination;
        }
    }
}
