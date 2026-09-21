using UnityEngine;

namespace Hypocycloid.Reverie.Common
{
    [CreateAssetMenu(menuName = "Reverie/Diagnostics Settings", fileName = "DiagnosticsSettings")]
    public sealed class DiagnosticsSettings : ScriptableObject
    {
        public bool enableLogs = true;
        public bool interaction = true;
        public bool rendering = true;
        public bool player = true;
        public bool scene = true;
    }
}
