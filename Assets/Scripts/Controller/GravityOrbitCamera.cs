using Hypocycloid.Reverie.Common;
using Gsplat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hypocycloid.Reverie.Controller
{
    // Handheld viewing: the device's gravity vector chooses where the viewer stands on a
    // sphere around the subject. Upright looks at the cat from its own level, flat looks
    // straight down at it, and rolling the device sideways sweeps the orbit.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)]
    public sealed class GravityOrbitCamera : MonoBehaviour
    {
        [SerializeField]
        GsplatRenderer subject;

        [SerializeField]
        Vector3 focusOffset = new Vector3(0f, 0.15f, 0f);

        [Tooltip("Half the size of what should fill the frame. The splat also carries the "
            + "ground it was scanned on, so this is the cat rather than the whole patch.")]
        [SerializeField, Min(0.1f)]
        float subjectRadius = 1.6f;

        [Tooltip("Fraction of the narrower viewport axis the subject should span.")]
        [SerializeField, Range(0.2f, 1f)]
        float framingFill = 0.9f;

        [SerializeField, Range(0.3f, 3f)]
        float zoom = 1f;

        [SerializeField, Range(0.3f, 1f)]
        float minZoom = 0.5f;

        [SerializeField, Range(1f, 3f)]
        float maxZoom = 2f;

        [SerializeField, Range(-45f, 45f)]
        float minElevation;

        [SerializeField, Range(0f, 89f)]
        float maxElevation = 85f;

        [Tooltip("Ambient orbit in degrees per second, continued from the desktop rig.")]
        [SerializeField]
        float baseOrbitSpeed = 4f;

        [Tooltip("Degrees per second at full tilt. Negative values invert which way tilting turns.")]
        [SerializeField]
        float maxTiltOrbitSpeed = 70f;

        [Tooltip("Roll below this many degrees is treated as holding still.")]
        [SerializeField, Range(0f, 30f)]
        float tiltDeadzone = 7f;

        [Tooltip("Roll at this many degrees orbits at full speed. Keep it under the " +
            "angle at which the OS auto-rotates the screen.")]
        [SerializeField, Range(10f, 80f)]
        float tiltRange = 40f;

        [SerializeField, Min(0.01f)]
        float attitudeSharpness = 8f;

        [SerializeField, Min(0.01f)]
        float poseSharpness = 10f;

        // Android can register its sensors a few frames after the first scene loads.
        const float AcquireWindow = 5f;

        Camera output;
        FlyCameraController fly;
        Sensor source;
        bool fromAccelerometer;
        float acquireDeadline;
        float nextAcquireAttempt;
        Vector3 gravity = Vector3.down;
        Vector3 focus;
        bool focusResolved;
        float yaw;
        float elevation;
        float currentRadius;
        int previousTouchCount;

        public bool HasGravitySource => source != null;
        public float Elevation => elevation;

        void OnEnable()
        {
            output = GetComponent<Camera>();
            fly = GetComponent<FlyCameraController>();
            source = null;
            acquireDeadline = Time.unscaledTime + AcquireWindow;
            nextAcquireAttempt = 0f;
            gravity = Vector3.down;
            yaw = transform.eulerAngles.y;
            elevation = Mathf.Clamp(0f, minElevation, maxElevation);
            currentRadius = FramingDistance();
            previousTouchCount = 0;
        }

        bool TryAcquireSource()
        {
            // Desktop and editor sessions never find one and keep the fly rig untouched.
            if (Time.unscaledTime > acquireDeadline || Time.unscaledTime < nextAcquireAttempt)
                return false;
            nextAcquireAttempt = Time.unscaledTime + 0.5f;
            if (GravitySensor.current != null)
            {
                source = GravitySensor.current;
                fromAccelerometer = false;
            }
            else if (Accelerometer.current != null)
            {
                source = Accelerometer.current;
                fromAccelerometer = true;
            }
            else
            {
                return false;
            }
            if (!source.enabled)
                InputSystem.EnableDevice(source);
            Diagnostics.Log(
                Diagnostics.Category.Player,
                "GravityCameraReady",
                $"source={source.name}; radius={currentRadius}; range=[{minElevation},{maxElevation}]",
                this
            );
            return true;
        }

        Vector3 ReadGravity() =>
            fromAccelerometer
                ? ((Accelerometer)source).acceleration.ReadValue()
                : ((GravitySensor)source).gravity.ReadValue();

        void LateUpdate()
        {
            if (Time.timeScale == 0f || PlayerInputGate.IsBlocked)
                return;
            if (source == null && !TryAcquireSource())
                return;
            // The fly rig would otherwise keep writing a competing pose every frame.
            if (fly && fly.enabled)
                fly.enabled = false;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Integrate(dt);
            ReadTouchInput();

            Quaternion orbit = Quaternion.Euler(elevation, yaw, 0f);
            SceneCameraRig.SetPose(output, ResolveFocus() + orbit * Vector3.back * currentRadius, orbit);
        }

        void Integrate(float dt)
        {
            Vector3 raw = ReadGravity();
            float magnitude = raw.magnitude;
            if (magnitude > 0.2f)
            {
                // The reading arrives in g or m/s² depending on the backing sensor, and a
                // raw accelerometer carries the player's hand motion on top of gravity.
                float smoothing = fromAccelerometer ? attitudeSharpness * 0.4f : attitudeSharpness;
                Vector3 blended = Vector3.Lerp(gravity, raw / magnitude, 1f - Mathf.Exp(-smoothing * dt));
                if (blended.sqrMagnitude > 0.0001f)
                    gravity = blended.normalized;
            }

            float targetElevation = Mathf.Clamp(
                Mathf.Atan2(-gravity.z, -gravity.y) * Mathf.Rad2Deg,
                minElevation,
                maxElevation
            );
            // Positive roll is the top edge tipped right, which peeks around that side.
            float roll = Mathf.Atan2(gravity.x, -gravity.y) * Mathf.Rad2Deg;
            // Roll around the gravity axis is unobservable once the device lies flat, so
            // hand its authority back to the ambient drift as the screen approaches level.
            float lateral = new Vector2(gravity.x, gravity.y).magnitude;
            float authority = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.5f, lateral));
            float tilt = Mathf.Clamp01(
                (Mathf.Abs(roll) - tiltDeadzone) / Mathf.Max(0.01f, tiltRange - tiltDeadzone)
            );
            float tiltSpeed = Mathf.Sign(roll) * tilt * tilt * maxTiltOrbitSpeed * authority;

            yaw = Mathf.Repeat(yaw + (baseOrbitSpeed + tiltSpeed) * dt, 360f);
            float poseT = 1f - Mathf.Exp(-poseSharpness * dt);
            elevation = Mathf.Lerp(elevation, targetElevation, poseT);
            currentRadius = Mathf.Lerp(currentRadius, FramingDistance(), poseT);
        }

        void ReadTouchInput()
        {
            var screen = Touchscreen.current;
            UnityEngine.InputSystem.Controls.TouchControl first = null, second = null;
            int count = 0;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (!touch.press.isPressed)
                        continue;
                    if (count == 0)
                        first = touch;
                    if (count == 1)
                        second = touch;
                    ++count;
                }
            }
            bool changed = count != previousTouchCount;
            previousTouchCount = count;
            // Ignore gesture transitions so adding or lifting a finger does not jump the view.
            if (count == 0 || changed)
                return;

            float pixels = Mathf.Max(1, Mathf.Min(Screen.width, Screen.height));
            Vector2 delta = first.delta.ReadValue();
            if (count == 1)
            {
                yaw = Mathf.Repeat(yaw + delta.x / pixels * 180f, 360f);
                return;
            }
            Vector2 otherDelta = second.delta.ReadValue();
            Vector2 separation = first.position.ReadValue() - second.position.ReadValue();
            float pinch = separation.magnitude - (separation - delta + otherDelta).magnitude;
            zoom = Mathf.Clamp(zoom + pinch / pixels * maxZoom, minZoom, maxZoom);
        }

        // A portrait phone sees a far narrower horizontal arc than the editor's wide Game
        // view, so a fixed distance frames completely differently on each. Solve for the
        // distance that makes the subject span the tighter of the two axes instead.
        float FramingDistance()
        {
            float tanHalfVertical = Mathf.Tan(output.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanHalf = Mathf.Min(tanHalfVertical, tanHalfVertical * output.aspect);
            return subjectRadius / Mathf.Max(0.01f, tanHalf * framingFill)
                * Mathf.Clamp(zoom, minZoom, maxZoom);
        }

        public void SetSubject(GsplatRenderer value, Vector3 centre)
        {
            subject = value;
            focus = centre;
            focusResolved = true;
        }

        Vector3 ResolveFocus()
        {
            if (!subject)
                return focus + focusOffset;
            if (!focusResolved)
            {
                // Streaming can land the asset a few frames after the rig starts.
                var asset = subject.GsplatAsset;
                focus = asset
                    ? subject.transform.TransformPoint(asset.Bounds.center)
                    : subject.transform.position;
                focusResolved = asset != null;
            }
            return focus + focusOffset;
        }
    }
}
