using UnityEngine;

namespace Hypocycloid.Reverie.Controller
{
    // Two opposing impulses, separated by a release, distinguish a shake from one bump or tilt.
    public sealed class ShakeGesture
    {
        Vector3 gravity;
        Vector3 firstImpulse;
        float firstTime;
        bool initialized;
        bool hasImpulse;
        bool released = true;

        public void Reset()
        {
            initialized = false;
            hasImpulse = false;
            released = true;
        }

        public bool Sample(Vector3 acceleration, float deltaTime, float now, float threshold, float window)
        {
            if (!initialized)
            {
                gravity = acceleration;
                initialized = true;
                return false;
            }
            gravity = Vector3.Lerp(gravity, acceleration, 1f - Mathf.Exp(-3f * Mathf.Min(deltaTime, 0.1f)));
            Vector3 impulse = acceleration - gravity;
            if (hasImpulse && now - firstTime > window)
                hasImpulse = false;
            if (impulse.magnitude < threshold * 0.5f)
                released = true;
            if (!released || impulse.magnitude < threshold)
                return false;
            released = false;
            if (hasImpulse && Vector3.Dot(firstImpulse.normalized, impulse.normalized) < -0.3f)
            {
                hasImpulse = false;
                return true;
            }
            firstImpulse = impulse;
            firstTime = now;
            hasImpulse = true;
            return false;
        }
    }
}
