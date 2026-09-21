using UnityEngine;
using UnityEngine.UI;

namespace Hypocycloid.Reverie.UI
{
    // A Selectable's colour tint only reaches its one target graphic, so an icon drawn over a
    // button's plate kept full strength while the plate dimmed. This gives one more graphic the
    // same disabled colour. Selectable raises no event when it stops being interactable, whether
    // through its own flag or a parent CanvasGroup, so the state is polled.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("UI/Reverie/Disabled Tint")]
    public sealed class DisabledTint : MonoBehaviour
    {
        [SerializeField]
        Selectable selectable;

        Graphic graphic;
        bool disabled;

        void Awake() => graphic = GetComponent<Graphic>();

        void OnEnable() => Apply(true);

        void LateUpdate()
        {
            if (selectable.IsInteractable() == disabled)
                Apply(false);
        }

        void Apply(bool instant)
        {
            disabled = !selectable.IsInteractable();
            ColorBlock colors = selectable.colors;
            Color tint = disabled ? colors.disabledColor * colors.colorMultiplier : Color.white;
            graphic.CrossFadeColor(tint, instant ? 0f : colors.fadeDuration, true, true);
        }
    }
}
