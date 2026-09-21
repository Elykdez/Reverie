using Hypocycloid.Reverie.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hypocycloid.Reverie.Controller
{
    // Android delivers its hardware back button as Escape through the Input System, so the
    // handheld button and the desktop key share one path. Deliberately not gated on
    // PlayerInputGate: backing out is most useful exactly while UI owns the input.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class BackButtonControl : MonoBehaviour
    {
        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (!Application.isFocused || keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;
            if (BackNavigation.TryGoBack())
                return;
            Diagnostics.Log(Diagnostics.Category.Interaction, "back_quit", "no handler consumed back", this);
            Quit();
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
