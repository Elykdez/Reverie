using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hypocycloid.Reverie.Controller
{
    public sealed class SceneLoadingBlurFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader blurShader;
        Material material;
        SceneLoadingBlurPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = blurShader ? CoreUtils.CreateEngineMaterial(blurShader) : null;
            pass = material ? new SceneLoadingBlurPass(material) : null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            // Keep editor previews and offscreen cameras unaffected.
            if (pass == null || SceneLoadingBlur.CurrentRadius <= 0f || camera != Camera.main ||
                camera.cameraType != CameraType.Game || camera.targetTexture)
                return;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }
    }
}
