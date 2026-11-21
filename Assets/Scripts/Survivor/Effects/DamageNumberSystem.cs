using System;
using UnityEngine;

namespace Survivor
{
    // Floating damage numbers. Capped, because area weapons can hit hundreds of enemies in one frame.
    public sealed class DamageNumberSystem
    {
        const int MaxAlive = 400;
        const float Lifetime = 0.6f;
        const float DigitHeight = 0.32f;

        readonly DebugSettings debug;

        int count;
        readonly Vector2[] position = new Vector2[MaxAlive];
        readonly int[] value = new int[MaxAlive];
        readonly float[] age = new float[MaxAlive];
        readonly bool[] big = new bool[MaxAlive];

        public DamageNumberSystem(DebugSettings debug) => this.debug = debug;

        public void Spawn(Vector2 at, float amount, bool isBig = false)
        {
            if (!debug.ShowDamageNumbers || count >= MaxAlive) return;
            var i = count++;
            position[i] = at + new Vector2(UnityEngine.Random.Range(-0.2f, 0.2f), 0.3f);
            value[i] = Mathf.Max(1, Mathf.RoundToInt(amount));
            age[i] = 0f;
            big[i] = isBig;
        }

        public void Tick(float dt)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                age[i] += dt;
                position[i] += new Vector2(0f, 1.2f * dt);
                if (age[i] < Lifetime) continue;
                var last = --count;
                position[i] = position[last];
                value[i] = value[last];
                age[i] = age[last];
                big[i] = big[last];
            }
        }

        public void Draw(InstancedRenderer renderer)
        {
            Span<int> digits = stackalloc int[10];
            var texture = DigitFont.Texture;
            for (var i = 0; i < count; i++)
            {
                var n = 0;
                var v = value[i];
                do
                {
                    digits[n++] = v % 10;
                    v /= 10;
                } while (v > 0 && n < digits.Length);

                var t = age[i] / Lifetime;
                var height = DigitHeight * (big[i] ? 1.6f : 1f) * (t < 0.15f ? 1f + (0.15f - t) * 3f : 1f); // small pop-in
                var width = height * DigitFont.Aspect * 0.8f; // letters overlap their outline a little
                var color = big[i] ? new Color(1f, 0.85f, 0.3f) : Color.white;
                color.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;

                var startX = position[i].x - (n - 1) * width / 2f;
                for (var k = 0; k < n; k++)
                {
                    var digit = digits[n - 1 - k];
                    renderer.TextureRect(RenderLayer.Text, texture, DigitFont.Uv(digit),
                        new Vector2(startX + k * width, position[i].y), new Vector2(height * DigitFont.Aspect, height), color);
                }
            }
        }
    }
}
