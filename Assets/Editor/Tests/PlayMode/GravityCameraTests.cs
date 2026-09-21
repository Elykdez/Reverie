using System;
using System.Collections;
using Hypocycloid.Reverie.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Hypocycloid.Reverie.Tests
{
    // The handheld rig: device attitude aims the camera at the subject and takes the fly rig offline.
    public sealed class GravityCameraTests : ReverieSceneTest
    {
        // The rig low-passes the sensor, so each attitude is driven until the rig reaches the pose
        // rather than for a fixed frame count, which would depend on the editor's frame rate.
        const float SettleTimeout = 30f;

        static readonly Vector3 Flat = new Vector3(0f, 0f, -1f);
        static readonly Vector3 Upright = new Vector3(0f, -1f, 0f);

        GravitySensor sensor;
        GravityOrbitCamera orbit;
        FlyCameraController fly;
        bool previousCompensation;

        [SetUp]
        public void AttachAGravitySensor()
        {
            orbit = Camera.main.GetComponent<GravityOrbitCamera>();
            fly = Camera.main.GetComponent<FlyCameraController>();
            Assert.That(orbit, Is.Not.Null, "The main camera has no gravity orbit camera.");
            Assert.That(fly, Is.Not.Null, "The main camera has no fly camera controller.");

            previousCompensation = InputSystem.settings.compensateForScreenOrientation;
            // Compensation would rotate the injected readings by whatever orientation the editor reports.
            InputSystem.settings.compensateForScreenOrientation = false;
            sensor = InputSystem.AddDevice<GravitySensor>();
            InputSystem.EnableDevice(sensor);
            // The rig only looks for a sensor during a short window after it is enabled, and that
            // window closed long before this suite runs, so the component is toggled to reopen it.
            orbit.enabled = false;
            orbit.enabled = true;
        }

        [TearDown]
        public void DetachTheGravitySensor()
        {
            if (orbit != null)
                orbit.enabled = false;
            if (sensor != null)
                InputSystem.RemoveDevice(sensor);
            if (fly != null)
                fly.enabled = true;
            InputSystem.settings.compensateForScreenOrientation = previousCompensation;
            sensor = null;
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator DeviceAttitudeAimsTheCameraAtTheSubject()
        {
            yield return DriveUntil(
                Flat,
                () => orbit.HasGravitySource && orbit.Elevation > 60f,
                () => $"a flat device to look down at the subject (acquired={orbit.HasGravitySource}; "
                    + $"elevation={orbit.Elevation:F1} degrees)"
            );
            yield return DriveUntil(
                Upright,
                () => orbit.Elevation < 15f,
                () => $"an upright device to level the view (elevation={orbit.Elevation:F1} degrees)"
            );
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator TheGravityRigTakesTheFlyRigOffline()
        {
            yield return DriveUntil(
                Upright,
                () => orbit.HasGravitySource,
                () => "the gravity sensor to be acquired"
            );
            Assert.That(fly.enabled, Is.False, "The fly rig kept writing a competing pose.");
        }

        // Feeds one attitude every frame until the rig reports the expected pose.
        IEnumerator DriveUntil(Vector3 gravity, Func<bool> reached, Func<string> describe)
        {
            float deadline = Time.realtimeSinceStartup + SettleTimeout;
            while (!reached())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Timed out after {SettleTimeout:F0}s waiting for {describe()}.");
                InputSystem.QueueDeltaStateEvent(sensor.gravity, gravity);
                yield return null;
            }
        }
    }
}
