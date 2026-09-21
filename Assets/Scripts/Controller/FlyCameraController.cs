using Hypocycloid.Reverie.Common;
using Gsplat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hypocycloid.Reverie.Controller
{
    [DisallowMultipleComponent]
    public sealed class FlyCameraController : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        float moveSpeed = 4f;

        [SerializeField, Min(1f)]
        float sprintMultiplier = 3f;

        [SerializeField, Min(0f)]
        float lookSensitivity = 0.08f;

        [SerializeField]
        bool lookRequiresRightMouse = true;

        [SerializeField]
        float minPitch = -89f;

        [SerializeField]
        float maxPitch = 89f;

        [SerializeField, Min(0.01f)]
        float unitRadius = 0.2f;

        [SerializeField]
        Rigidbody unitBody;

        [Tooltip("Capture to circle continuously. Leave empty for manual flight only.")]
        [SerializeField] GsplatRenderer orbitSubject;
        [SerializeField, Min(1f)] float orbitPeriod = 90f;
        Vector3 orbitCentre;
        bool explicitOrbitCentre;
        SphereCollider unitCollider;
        Vector3 movementVelocity;
        float yaw;
        float pitch;
        int previousTouchCount;

        public void SetOrbitSubject(GsplatRenderer subject, Vector3 centre)
        {
            orbitSubject = subject;
            orbitCentre = centre;
            explicitOrbitCentre = true;
        }

        void OnEnable()
        {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = NormalizeAngle(euler.x);
            if (!explicitOrbitCentre && orbitSubject && orbitSubject.GsplatAsset)
                orbitCentre = orbitSubject.transform.TransformPoint(orbitSubject.GsplatAsset.Bounds.center);
            if (!unitBody)
            {
                var unit = new GameObject("Fly Camera Unit");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(unit, gameObject.scene);
                unit.layer = LayerMask.NameToLayer("Ignore Raycast");
                unit.transform.SetParent(transform.parent, false);
                unit.transform.position = transform.position;
                unitCollider = unit.AddComponent<SphereCollider>();
                unitCollider.radius = unitRadius;
                unitBody = unit.AddComponent<Rigidbody>();
                unitBody.useGravity = false;
                unitBody.constraints = RigidbodyConstraints.FreezeRotation;
                unitBody.interpolation = RigidbodyInterpolation.Interpolate;
                // Move the sphere through physics; moving the camera Transform would bypass collisions.
                transform.SetParent(unit.transform, true);
            }
            unitCollider = unitBody.GetComponent<SphereCollider>();
            unitCollider.radius = unitRadius;
            unitCollider.enabled = true;
            unitBody.isKinematic = false;
            unitBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            movementVelocity = Vector3.zero;
            previousTouchCount = 0;
        }

        void OnDisable()
        {
            movementVelocity = Vector3.zero;
            if (!unitBody)
                return;
            if (!unitBody.isKinematic)
                unitBody.linearVelocity = Vector3.zero;
            unitBody.isKinematic = true;
            unitCollider.enabled = false;
        }

        void FixedUpdate()
        {
            unitBody.linearVelocity =
                Application.isFocused && !PlayerInputGate.IsBlocked
                    ? movementVelocity
                    : Vector3.zero;
            if (orbitSubject)
            {
                // Exact circular step, composed with manual flight through the same body.
                Vector3 offset = unitBody.position - orbitCentre;
                Vector3 next = Quaternion.AngleAxis(360f * Time.fixedDeltaTime / orbitPeriod, Vector3.up) * offset;
                unitBody.linearVelocity += (next - offset) / Time.fixedDeltaTime;
            }
        }

        void Update()
        {
            movementVelocity = Vector3.zero;
            if (Time.timeScale == 0f || !Application.isFocused || PlayerInputGate.IsBlocked)
            {
                previousTouchCount = 0;
                return;
            }

            if (ReadTouchInput())
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Vector2 planar = Vector2.zero;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                    planar.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                    planar.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                    planar.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                    planar.x -= 1f;

                float vertical = 0f;
                if (keyboard.spaceKey.isPressed)
                    vertical += 1f;
                if (
                    keyboard.leftCtrlKey.isPressed
                    || keyboard.rightCtrlKey.isPressed
                    || keyboard.cKey.isPressed
                )
                    vertical -= 1f;

                Vector3 direction =
                    transform.forward * planar.y + transform.right * planar.x + Vector3.up * vertical;
                if (direction.sqrMagnitude > 1f)
                    direction.Normalize();

                float speed = moveSpeed;
                if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                    speed *= sprintMultiplier;
                movementVelocity = direction * speed;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;
            movementVelocity += transform.forward * mouse.scroll.ReadValue().y * 0.01f * moveSpeed;
            if (lookRequiresRightMouse && !mouse.rightButton.isPressed)
                return;

            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, minPitch, maxPitch);
            SceneCameraRig.SetPose(GetComponent<Camera>(), transform.position, Quaternion.Euler(pitch, yaw, 0f));
        }

        bool ReadTouchInput()
        {
            var screen = Touchscreen.current;
            UnityEngine.InputSystem.Controls.TouchControl first = null, second = null;
            int count = 0;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    if (count == 0) first = touch;
                    if (count == 1) second = touch;
                    ++count;
                }
            }
            bool changed = count != previousTouchCount;
            previousTouchCount = count;
            if (count == 0) return false;
            // Ignore gesture transitions so adding/removing a finger does not jump the view.
            if (changed) return true;
            float pixels = Mathf.Max(1, Mathf.Min(Screen.width, Screen.height));
            Vector2 delta = first.delta.ReadValue();
            if (count == 1)
            {
                yaw += delta.x / pixels * 180f;
                pitch = Mathf.Clamp(pitch - delta.y / pixels * 180f, minPitch, maxPitch);
            }
            else
            {
                Vector2 otherDelta = second.delta.ReadValue();
                Vector2 separation = first.position.ReadValue() - second.position.ReadValue();
                float pinch = separation.magnitude - (separation - delta + otherDelta).magnitude;
                Vector2 pan = (delta + otherDelta) * 0.5f;
                movementVelocity = (-transform.right * pan.x - transform.up * pan.y
                    + transform.forward * pinch) * moveSpeed / (pixels * Mathf.Max(Time.deltaTime, 0.001f));
            }
            return true;
        }

        void LateUpdate()
        {
            if (orbitSubject)
                yaw += 360f * Time.deltaTime / orbitPeriod;
            // The physical sphere translates the camera parent between render frames.
            SceneCameraRig.SetPose(GetComponent<Camera>(), transform.position, Quaternion.Euler(pitch, yaw, 0f));
        }

        static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
