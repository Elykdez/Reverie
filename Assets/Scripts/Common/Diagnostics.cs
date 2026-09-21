using UnityEngine;

namespace Hypocycloid.Reverie.Common
{
    public static class Diagnostics
    {
        public enum Category
        {
            Interaction = 1,
            Rendering = 2,
            Player = 3,
            Scene = 4,
        }

        const string SettingsResourceName = "DebugSettings";

        public static DiagnosticsSettings Settings { get; private set; }

        public static void Configure(DiagnosticsSettings settings) => Settings = settings;

        // Without this every category reads as disabled and nothing reaches logcat.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadSettings() =>
            Settings = Resources.Load<DiagnosticsSettings>(SettingsResourceName);

        public static bool IsEnabled(Category category)
        {
            if (Settings == null || !Settings.enableLogs)
                return false;
            switch (category)
            {
                case Category.Interaction:
                    return Settings.interaction;
                case Category.Rendering:
                    return Settings.rendering;
                case Category.Player:
                    return Settings.player;
                case Category.Scene:
                    return Settings.scene;
                default:
                    return false;
            }
        }

        public static void Log(
            Category category,
            string eventName,
            string detail,
            Object context = null
        )
        {
            if (IsEnabled(category))
                Debug.Log($"[Reverie/{category}] {eventName} | {detail}", context);
        }

        public static void Warning(
            Category category,
            string eventName,
            string detail,
            Object context = null
        )
        {
            if (IsEnabled(category))
                Debug.LogWarning($"[Reverie/{category}] {eventName} | {detail}", context);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Settings = null;
    }
}
