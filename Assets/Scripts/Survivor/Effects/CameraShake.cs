using UnityEngine;

namespace Survivor
{
    // Small random camera offset that fades out quickly. Weapons add to it, GameController applies it.
    public sealed class CameraShake
    {
        const float MaxStrength = 0.4f;
        const float Decay = 18f;

        float strength;

        public void Add(float amount) => strength = Mathf.Min(MaxStrength, strength + amount);

        // Returns this frame's camera offset.
        public Vector2 Tick(float dt)
        {
            strength *= Mathf.Exp(-Decay * dt);
            return strength < 0.001f ? Vector2.zero : Random.insideUnitCircle * strength;
        }
    }
}
