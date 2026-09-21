using System.Threading;
using Hypocycloid.Reverie.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hypocycloid.Reverie.Controller
{
    // Android 13+ uses its Back callback; older Android and desktop use Escape.
    // Desktop Escape lets open UI consume the request first.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class BackButtonControl : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        sealed class AndroidBackCallback : AndroidJavaProxy
        {
            readonly BackButtonControl owner;

            public AndroidBackCallback(BackButtonControl owner)
                : base("android.window.OnBackInvokedCallback") => this.owner = owner;

            public void onBackInvoked() => Interlocked.Exchange(ref owner.backRequested, 1);
        }

        AndroidJavaObject backDispatcher;
        AndroidBackCallback backCallback;
        int backRequested;

        void OnEnable()
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            if (version.GetStatic<int>("SDK_INT") < 33)
                return;
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            backDispatcher = activity.Call<AndroidJavaObject>("getOnBackInvokedDispatcher");
            backCallback = new AndroidBackCallback(this);
            // PRIORITY_OVERLAY receives Back before Unity's default activity callback.
            backDispatcher.Call("registerOnBackInvokedCallback", 1000000, backCallback);
        }

        void OnDisable()
        {
            if (backDispatcher == null)
                return;
            backDispatcher.Call("unregisterOnBackInvokedCallback", backCallback);
            backDispatcher.Dispose();
            backDispatcher = null;
            backCallback = null;
        }
#endif

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            bool pressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#if UNITY_ANDROID && !UNITY_EDITOR
            pressed |= Interlocked.Exchange(ref backRequested, 0) != 0;
#endif
            if (!Application.isFocused || !pressed)
                return;
            if (Application.platform != RuntimePlatform.Android && BackNavigation.TryGoBack())
                return;
            Diagnostics.Log(Diagnostics.Category.Interaction, "back_quit", "back requested app exit", this);
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
