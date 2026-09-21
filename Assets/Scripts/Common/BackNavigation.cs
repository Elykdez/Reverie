using System.Collections.Generic;
using UnityEngine;

namespace Hypocycloid.Reverie.Common
{
    // Anything that can absorb a back request implements this and registers below.
    public interface IBackHandler
    {
        // Return true once the request is consumed, which keeps the application running.
        bool TryGoBack();
    }

    public static class BackNavigation
    {
        static readonly List<IBackHandler> handlers = new List<IBackHandler>();

        public static void Register(IBackHandler handler)
        {
            if (handler != null && !handlers.Contains(handler))
                handlers.Add(handler);
        }

        public static void Unregister(IBackHandler handler) => handlers.Remove(handler);

        public static bool TryGoBack()
        {
            // Newest registration answers first, so nested state unwinds in entry order.
            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                IBackHandler handler = handlers[i];
                if (handler is Object component && component == null)
                {
                    handlers.RemoveAt(i);
                    continue;
                }
                if (handler.TryGoBack())
                {
                    Diagnostics.Log(
                        Diagnostics.Category.Interaction,
                        "back_consumed",
                        handler.GetType().Name,
                        handler as Object
                    );
                    return true;
                }
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => handlers.Clear();
    }
}
