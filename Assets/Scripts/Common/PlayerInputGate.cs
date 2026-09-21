using UnityEngine;

namespace Hypocycloid.Reverie.Common
{
    // UI owns input while the controller continues gravity, animation and foot IK.
    public static class PlayerInputGate
    {
        static Object owner;
        static bool acquired;
        static bool previousCursorVisible;
        static CursorLockMode previousCursorLock;

        public static bool IsBlocked
        {
            get
            {
                if (acquired && owner == null)
                    RestoreCursor();
                return acquired;
            }
        }

        public static bool TryAcquire(Object requester)
        {
            if (requester == null || (IsBlocked && owner != requester))
                return false;
            if (acquired)
                return true;
            owner = requester;
            acquired = true;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Diagnostics.Log(
                Diagnostics.Category.Interaction,
                "input_acquired",
                requester.name,
                requester
            );
            return true;
        }

        public static void Release(Object requester)
        {
            if (acquired && owner == requester)
                RestoreCursor();
        }

        static void RestoreCursor()
        {
            Diagnostics.Log(
                Diagnostics.Category.Interaction,
                "input_released",
                owner != null ? owner.name : "Destroyed owner",
                owner
            );
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            owner = null;
            acquired = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            owner = null;
            acquired = false;
        }
    }
}
