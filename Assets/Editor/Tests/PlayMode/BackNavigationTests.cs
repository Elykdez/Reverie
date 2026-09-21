using System.Collections;
using Hypocycloid.Reverie.Common;
using Hypocycloid.Reverie.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Hypocycloid.Reverie.Tests
{
    public sealed class BackNavigationTests : ReverieSceneTest
    {
        sealed class RecordingHandler : IBackHandler
        {
            public int Consumed;

            public bool TryGoBack()
            {
                ++Consumed;
                return true;
            }
        }

        [Test]
        public void BackIsNotConsumedWithoutAHandler() =>
            // Only the API path is exercised here. Pressing escape with nothing registered is
            // the quit path, which would tear down the test run.
            Assert.That(BackNavigation.TryGoBack(), Is.False, "Back was consumed with no handler registered.");

        [UnityTest]
        public IEnumerator EscapeReachesTheRegisteredHandler()
        {
            var back = Camera.main.GetComponent<BackButtonControl>();
            Assert.That(back, Is.Not.Null, "The main camera has no back button control.");

            var handler = new RecordingHandler();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputSettings.EditorInputBehaviorInPlayMode previousBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            try
            {
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                BackNavigation.Register(handler);
                // wasPressedThisFrame only holds for the input update that processed the event, so
                // the press is queued and consumed by the same frame the control reads it in.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;

                Assert.That(handler.Consumed, Is.GreaterThan(0), "Escape never reached the back control.");
            }
            finally
            {
                // Release the key before unregistering so a later frame cannot find an unhandled
                // press and quit play mode out from under the test run.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                BackNavigation.Unregister(handler);
                InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
