using DG.Tweening;
using DG.Tweening.Core.Easing;
using UnityEngine;

namespace Survivor
{
    // How a gem moves when the player picks it up: first it jumps back a little (anticipation),
    // then it flies into the player. Edit Assets/Resources/GemPickupSettings.asset while the game runs
    // in Play Mode to try different eases live (F4 drops a ring of gems to test).
    [CreateAssetMenu(menuName = "Survivor/Gem Pickup Settings", fileName = "GemPickupSettings")]
    public sealed class GemPickupSettings : ScriptableObject
    {
        [Header("1. Lùi lại (anticipation)")]
        [Tooltip("How far the gem jumps away from the player. 0 = no back-off.")]
        [Min(0f)] public float BackDistance = 0.6f;
        [Min(0f)] public float BackDuration = 0.15f;
        public Ease BackEase = Ease.OutQuad;

        [Header("2. Phóng tới người chơi")]
        [Min(0.01f)] public float FlyDuration = 0.3f;
        public Ease FlyEase = Ease.InBack;

        [Header("Ease tuning (same meaning as in DOTween)")]
        [Tooltip("Back eases: overshoot. Elastic eases: amplitude.")]
        public float OvershootOrAmplitude = 1.70158f;
        [Tooltip("Elastic eases: period (0 = DOTween default).")]
        public float Period;

        // t in 0..1 -> eased value (Back / Elastic eases can go outside 0..1, that is the overshoot).
        public float Back(float t) => EaseManager.Evaluate(BackEase, null, t, 1f, OvershootOrAmplitude, Period);
        public float Fly(float t) => EaseManager.Evaluate(FlyEase, null, t, 1f, OvershootOrAmplitude, Period);

        public static GemPickupSettings LoadOrDefault()
        {
            var settings = Resources.Load<GemPickupSettings>("GemPickupSettings");
            return settings != null ? settings : CreateInstance<GemPickupSettings>();
        }
    }
}
