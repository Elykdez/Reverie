using UnityEngine;

namespace Hypocycloid.Reverie.Controller
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-1000)]
    public sealed class SceneExplorationController : MonoBehaviour
    {
        void Awake()
        {
            var fly = GetComponent<FlyCameraController>();
            if (!fly)
                fly = gameObject.AddComponent<FlyCameraController>();
            fly.enabled = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
