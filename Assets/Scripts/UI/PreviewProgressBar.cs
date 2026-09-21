using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hypocycloid.Reverie.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider))]
    public sealed class PreviewProgressBar : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action ScrubStarted;
        public event Action ScrubEnded;
        public Slider Slider => GetComponent<Slider>();
        public bool IsDragging { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !Slider.IsInteractable())
                return;
            IsDragging = true;
            ScrubStarted?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                EndScrub();
        }

        void OnDisable() => EndScrub();

        void EndScrub()
        {
            if (!IsDragging)
                return;
            IsDragging = false;
            ScrubEnded?.Invoke();
        }
    }
}
