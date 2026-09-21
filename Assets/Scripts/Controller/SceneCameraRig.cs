using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hypocycloid.Reverie.Controller
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera), typeof(CinemachineBrain))]
    [DefaultExecutionOrder(1000)]
    public sealed class SceneCameraRig : MonoBehaviour
    {
        [SerializeField] CinemachineCamera view;
        CinemachineBrain brain;
        Camera output;
        bool ownsView;
        int cameraOverride = -1;

        public CinemachineCamera View => view;

        void OnEnable()
        {
            // A Brain clears its override stack on disable/domain reload.
            // Rebind even when Unity hot reload has retained our private field values.
            ReleaseOverride();
            Initialize();
        }

        void Initialize()
        {
            if (brain)
                return;
            output = GetComponent<Camera>();
            brain = GetComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            // Each output owns its view, including simultaneous offscreen cameras.
            brain.ChannelMask = (OutputChannels)0;
            if (!view)
            {
                var obj = new GameObject(name + " Cinemachine View");
                SceneManager.MoveGameObjectToScene(obj, gameObject.scene);
                obj.transform.SetPositionAndRotation(transform.position, transform.rotation);
                view = obj.AddComponent<CinemachineCamera>();
                ownsView = true;
            }
            view.Lens = LensSettings.FromCamera(output);
            view.ForceCameraPosition(transform.position, transform.rotation);
            cameraOverride = brain.SetCameraOverride(-1, 0, null, view, 1f, -1f);
        }

        public static void SetPose(Camera camera, Vector3 position, Quaternion rotation, float? fieldOfView = null)
        {
            var rig = camera.GetComponent<SceneCameraRig>();
            if (!rig)
                rig = camera.gameObject.AddComponent<SceneCameraRig>();
            rig.Initialize();
            var lens = rig.view.Lens;
            if (fieldOfView.HasValue)
                lens.FieldOfView = fieldOfView.Value;
            rig.view.Lens = lens;
            rig.view.ForceCameraPosition(position, rotation);
            rig.Evaluate();
        }

        void LateUpdate() => Evaluate();

        void Evaluate()
        {
            if (!brain || !view || !brain.isActiveAndEnabled)
                return;
            // Passive views have no damping. Refresh their cached state explicitly so seeks
            // and photographic solver probes can evaluate more than once in a render frame.
            view.InternalUpdateCameraState(Vector3.up, -1f);
            brain.ManualUpdate();
        }

        void ReleaseOverride()
        {
            if (brain && cameraOverride >= 0)
                brain.ReleaseCameraOverride(cameraOverride);
            cameraOverride = -1;
            brain = null;
        }

        void OnDisable() => ReleaseOverride();

        void OnDestroy()
        {
            ReleaseOverride();
            if (ownsView && view)
            {
                if (Application.isPlaying)
                    Destroy(view.gameObject);
                else
                    DestroyImmediate(view.gameObject);
            }
        }
    }
}
