using Hypocycloid.Reverie.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hypocycloid.Reverie.Controller
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField]
        Transform target;

        [SerializeField]
        float targetHeight = -0.2f;

        [SerializeField, Min(0.5f)]
        float distance = 3.5f;

        [SerializeField, Min(0.25f)]
        float minDistance = 1f;

        [SerializeField]
        float pitch = 22f;

        [SerializeField]
        float yaw;

        [SerializeField, Range(0f, 80f)]
        float minPitch = 10f;

        [SerializeField, Range(0f, 80f)]
        float maxPitch = 65f;

        [SerializeField, Min(0f)]
        float lookSensitivity = 0.08f;

        [SerializeField, Min(0.01f)]
        float followSharpness = 14f;

        [SerializeField, Min(0.01f)]
        float rotationSharpness = 18f;

        [SerializeField, Min(0.01f)]
        float collisionRadius = 0.15f;

        [SerializeField, Min(0f)]
        float maxFocusOffset = 0.3f;

        [SerializeField]
        LayerMask collisionMask = ~0;

        [SerializeField]
        bool lookRequiresRightMouse = true;
        readonly RaycastHit[] hits = new RaycastHit[32];
        Vector3 focus;
        float currentPitch;
        float currentYaw;
        float currentDistance;
        bool initialized;

        public void SetTarget(Transform character)
        {
            target = character;
            initialized = false;
        }

        void OnEnable() => initialized = false;

        void LateUpdate()
        {
            if (Time.timeScale == 0f || target == null)
                return;
            Vector3 targetFocus = target.position + Vector3.up * targetHeight;
            if (!initialized)
            {
                focus = targetFocus;
                yaw = transform.eulerAngles.y;
                currentYaw = yaw;
                currentPitch = pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                currentDistance = Mathf.Max(minDistance, distance);
                initialized = true;
                Diagnostics.Log(
                    Diagnostics.Category.Player,
                    "CameraReady",
                    $"camera={name}; target={target.name}; distance={currentDistance}; pitch={currentPitch}",
                    this
                );
            }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Mouse mouse = Mouse.current;
            if (
                Application.isFocused
                && !PlayerInputGate.IsBlocked
                && mouse != null
                && (!lookRequiresRightMouse || mouse.rightButton.isPressed)
            )
            {
                Vector2 delta = Vector2.ClampMagnitude(mouse.delta.ReadValue(), 150f);
                yaw = Mathf.Repeat(yaw + delta.x * lookSensitivity, 360f);
                pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, minPitch, maxPitch);
            }
            focus = Vector3.Lerp(focus, targetFocus, 1f - Mathf.Exp(-followSharpness * dt));
            focus = targetFocus + Vector3.ClampMagnitude(focus - targetFocus, maxFocusOffset);
            float rotationT = 1f - Mathf.Exp(-rotationSharpness * dt);
            currentYaw = Mathf.LerpAngle(currentYaw, yaw, rotationT);
            currentPitch = Mathf.Lerp(currentPitch, pitch, rotationT);
            Quaternion orbit = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 direction = orbit * Vector3.back;
            float clearDistance = GetClearDistance(
                focus,
                direction,
                Mathf.Max(minDistance, distance)
            );
            currentDistance =
                clearDistance < currentDistance
                    ? clearDistance
                    : Mathf.Lerp(currentDistance, clearDistance, 1f - Mathf.Exp(-6f * dt));
            SceneCameraRig.SetPose(GetComponent<Camera>(), focus + direction * currentDistance, orbit);
        }

        float GetClearDistance(Vector3 origin, Vector3 direction, float maximum)
        {
            int count = Physics.SphereCastNonAlloc(
                origin,
                collisionRadius,
                direction,
                hits,
                maximum,
                collisionMask,
                QueryTriggerInteraction.Ignore
            );
            float nearest = maximum;
            for (int i = 0; i < count; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == target || hitTransform.IsChildOf(target))
                    continue;
                // A cast can report a zero-distance hit when the focus point is
                // inside a dense collision mesh. Ignore that stale overlap so the
                // camera never collapses into the target and then drifts away.
                if (hits[i].distance <= 0.001f)
                    continue;
                nearest = Mathf.Min(nearest, Mathf.Max(minDistance, hits[i].distance - 0.03f));
            }
            return Mathf.Max(minDistance, nearest);
        }
    }
}
