using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Hypocycloid.Reverie.Controller
{
    public sealed class SceneLoadingBlurPass : ScriptableRenderPass
    {
        static readonly int BlurRadiusUV = Shader.PropertyToID("_BlurRadiusUV");
        readonly Material material;

        public SceneLoadingBlurPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
                return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var cameraDescriptor = cameraData.cameraTargetDescriptor;
            float radius = SceneLoadingBlur.CurrentRadius * cameraDescriptor.height / 1080f;
            material.SetVector(BlurRadiusUV, new Vector4(
                radius / cameraDescriptor.width, radius / cameraDescriptor.height, 0f, 0f));

            var source = resources.activeColorTexture;
            var descriptor = renderGraph.GetTextureDesc(source);
            descriptor.width = Mathf.Max(1, cameraDescriptor.width / 2);
            descriptor.height = Mathf.Max(1, cameraDescriptor.height / 2);
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.bindTextureMS = false;
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.clearBuffer = false;
            descriptor.filterMode = FilterMode.Bilinear;
            descriptor.name = "Loading Blur Horizontal";
            var horizontal = renderGraph.CreateTexture(descriptor);
            descriptor.name = "Loading Blur Vertical";
            var vertical = renderGraph.CreateTexture(descriptor);

            // Both blur passes run at half resolution; the final blit restores the camera buffer.
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, horizontal, material, 0),
                passName: "Loading Blur Horizontal");
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(horizontal, vertical, material, 1),
                passName: "Loading Blur Vertical");
            renderGraph.AddBlitPass(vertical, source, Vector2.one, Vector2.zero, passName: "Loading Blur Composite");
        }
    }
}
