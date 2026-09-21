using System.Collections;
using Hypocycloid.Reverie.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Hypocycloid.Reverie.Tests
{
    // Keyboard, mouse and touch driving the fly rig through real frames.
    //
    // Deltas and wasPressedThisFrame only survive the input update that processed the event, so
    // every test below queues an event and then lets the frame's own input update consume it.
    // Calling InputSystem.Update() by hand instead would clear the delta before the rig reads it.
    public sealed class CameraControlTests : ReverieSceneTest
    {
        const float MinimumPan = 0.05f;
        const float GestureTimeout = 2f;
        static readonly Vector2 GestureStep = new Vector2(20f, 6f);

        InputSettings.EditorInputBehaviorInPlayMode previousInputBehavior;
        Keyboard keyboard;
        Mouse mouse;
        Touchscreen touch;

        [SetUp]
        public void RouteDeviceInputToTheGameView()
        {
            previousInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [TearDown]
        public void ReleaseInjectedDevices()
        {
            if (keyboard != null)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            if (mouse != null)
                InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update();

            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            if (mouse != null)
                InputSystem.RemoveDevice(mouse);
            if (touch != null)
                InputSystem.RemoveDevice(touch);
            keyboard = null;
            mouse = null;
            touch = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousInputBehavior;
        }

        // The fly rig deliberately drops all input while the editor is unfocused or the gate is
        // closed, so a headless or background run cannot say anything about the control code.
        static void RequireInputIsAccepted()
        {
            if (!Application.isFocused || PlayerInputGate.IsBlocked)
                Assert.Ignore(
                    $"Input is gated off (focused={Application.isFocused}; blocked={PlayerInputGate.IsBlocked}); "
                    + "run these tests with the editor focused."
                );
        }

        Rigidbody CameraBody()
        {
            var body = Camera.main.GetComponentInParent<Rigidbody>();
            Assert.That(body, Is.Not.Null, "The camera rig has no rigidbody.");
            return body;
        }

        // The rig orbits the subject on its own, so every look assertion has to beat the rotation
        // the camera would have picked up over the same number of idle frames. The window is long
        // enough that the idle drift is measurable rather than rounding to zero.
        const int MeasureFrames = 10;

        static IEnumerator MeasureTurn(Transform camera, System.Action inject, System.Action<float> report)
        {
            Quaternion before = camera.rotation;
            for (int i = 0; i < MeasureFrames; i++)
            {
                inject?.Invoke();
                yield return null;
            }
            report(Quaternion.Angle(before, camera.rotation));
        }

        // A rig that does not move at all while idle means the scene is not being driven in this
        // editor state, so a look test would be measuring nothing. TheAutomaticOrbitMovesTheCamera
        // is the test that holds the rig to actually animating.
        static void RequireTheRigIsAnimating(float drift)
        {
            if (drift <= 0f)
                Assert.Ignore(
                    "The camera rig is not animating in this editor state (idle drift was 0), "
                    + "so device input cannot be measured. Run with the editor focused."
                );
        }

        void QueueTouch(int id, Vector2 position, TouchPhase phase) =>
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, position = position, phase = phase });

        [UnityTest]
        public IEnumerator HoldingForwardChangesTheRigVelocity()
        {
            RequireInputIsAccepted();
            Rigidbody body = CameraBody();
            keyboard = InputSystem.AddDevice<Keyboard>();

            Vector3 automatic = body.linearVelocity;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.That(
                (body.linearVelocity - automatic).magnitude,
                Is.GreaterThan(1f),
                "Holding W did not move the rig beyond its automatic orbit."
            );
        }

        [UnityTest]
        public IEnumerator RightDraggingTheMouseLooksAround()
        {
            RequireInputIsAccepted();
            Transform camera = Camera.main.transform;
            mouse = InputSystem.AddDevice<Mouse>();

            float drift = 0f;
            yield return MeasureTurn(camera, null, turn => drift = turn);
            RequireTheRigIsAnimating(drift);

            float dragged = 0f;
            yield return MeasureTurn(
                camera,
                () => InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { delta = new Vector2(50f, 10f) }.WithButton(MouseButton.Right)
                ),
                turn => dragged = turn
            );

            RequireInputIsAccepted();
            Assert.That(
                dragged,
                Is.GreaterThan(drift + 1f),
                $"Right-dragging the mouse did not rotate the camera (idle drift {drift:F2} degrees, "
                    + $"with drag {dragged:F2} degrees)."
            );
        }

        [UnityTest]
        public IEnumerator DraggingOneFingerLooksAround()
        {
            RequireInputIsAccepted();
            touch = InputSystem.AddDevice<Touchscreen>();
            Transform camera = Camera.main.transform;

            QueueTouch(1, new Vector2(200f, 200f), TouchPhase.Began);
            // The rig skips any frame where the finger count changed, by design.
            yield return null;

            float drift = 0f;
            yield return MeasureTurn(camera, () => QueueTouch(1, new Vector2(200f, 200f), TouchPhase.Stationary),
                turn => drift = turn);
            RequireTheRigIsAnimating(drift);

            float dragged = 0f;
            yield return MeasureTurn(camera, () => QueueTouch(1, new Vector2(260f, 200f), TouchPhase.Moved),
                turn => dragged = turn);

            RequireInputIsAccepted();
            Assert.That(
                dragged,
                Is.GreaterThan(drift + 1f),
                $"Dragging one finger did not rotate the camera (idle drift {drift:F2} degrees, "
                    + $"with drag {dragged:F2} degrees)."
            );
        }

        [UnityTest]
        public IEnumerator TwoFingerGestureMovesTheRig()
        {
            RequireInputIsAccepted();
            touch = InputSystem.AddDevice<Touchscreen>();
            Rigidbody body = CameraBody();

            var first = new Vector2(200f, 200f);
            var second = new Vector2(400f, 200f);
            QueueTouch(1, first, TouchPhase.Began);
            yield return null;
            QueueTouch(2, second, TouchPhase.Began);
            yield return null;

            Vector3 automatic = body.linearVelocity;
            // Update() clears the pan every frame and the rig only commits it in FixedUpdate, so a
            // single-frame gesture is usually erased before physics sees it. Both fingers therefore
            // keep moving, in the same frame as each other, until a fixed step picks the pan up.
            float moved = 0f;
            float deadline = Time.realtimeSinceStartup + GestureTimeout;
            while (Time.realtimeSinceStartup < deadline && moved <= MinimumPan)
            {
                first += GestureStep;
                second += GestureStep;
                QueueTouch(1, first, TouchPhase.Moved);
                QueueTouch(2, second, TouchPhase.Moved);
                yield return null;
                moved = Mathf.Max(moved, (body.linearVelocity - automatic).magnitude);
            }

            RequireInputIsAccepted();
            Assert.That(
                moved,
                Is.GreaterThan(MinimumPan),
                $"A two-finger pan/pinch did not move the rig (best {moved:F4})."
            );
        }
    }
}
