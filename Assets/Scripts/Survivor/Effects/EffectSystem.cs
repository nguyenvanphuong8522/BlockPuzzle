using System;
using UnityEngine;

namespace Survivor
{
    // Short visual effects (explosions, lightning). Purely cosmetic.
    public sealed class EffectSystem
    {
        enum Kind { Ring, Burst, Bolt, Smoke }

        int count;
        Kind[] kind = new Kind[64];
        Vector2[] position = new Vector2[64];
        float[] size = new float[64];
        float[] age = new float[64];
        float[] duration = new float[64];
        Color[] color = new Color[64];

        // Expanding ring outline.
        public void Ring(Vector2 at, float radius, Color tint, float time = 0.3f) => Add(Kind.Ring, at, radius, tint, time);

        // Expanding filled circle that fades out.
        public void Burst(Vector2 at, float radius, Color tint, float time = 0.25f) => Add(Kind.Burst, at, radius, tint, time);

        // Grey puff that grows, drifts up and fades (gun smoke).
        public void Smoke(Vector2 at, float radius, float time = 0.35f) => Add(Kind.Smoke, at, radius, new Color(0.85f, 0.85f, 0.85f, 0.55f), time);
        public void Smoke(Vector2 at, float radius, float time, Color tint) => Add(Kind.Smoke, at, radius, tint, time);

        // Vertical lightning strike landing on a point.
        public void Bolt(Vector2 at, Color tint, float time = 0.2f) => Add(Kind.Bolt, at, 6f, tint, time);

        void Add(Kind k, Vector2 at, float s, Color tint, float time)
        {
            if (count == position.Length) Grow();
            var i = count++;
            kind[i] = k;
            position[i] = at;
            size[i] = s;
            age[i] = 0f;
            duration[i] = time;
            color[i] = tint;
        }

        public void Tick(float dt)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                age[i] += dt;
                if (age[i] < duration[i]) continue;
                var last = --count;
                kind[i] = kind[last];
                position[i] = position[last];
                size[i] = size[last];
                age[i] = age[last];
                duration[i] = duration[last];
                color[i] = color[last];
            }
        }

        public void Draw(InstancedRenderer renderer)
        {
            for (var i = 0; i < count; i++)
            {
                var t = Mathf.Clamp01(age[i] / duration[i]);
                var c = color[i];
                c.a *= 1f - t;
                switch (kind[i])
                {
                    case Kind.Ring:
                        renderer.Ring(RenderLayer.Effects, position[i], Mathf.Lerp(size[i] * 0.3f, size[i], t), 0.15f, c);
                        break;
                    case Kind.Burst:
                        c.a *= 0.5f;
                        renderer.Circle(RenderLayer.Effects, position[i], Mathf.Lerp(size[i] * 0.3f, size[i], t), c);
                        break;
                    case Kind.Smoke:
                        renderer.Circle(RenderLayer.Effects, position[i] + new Vector2(0f, t * 0.35f), Mathf.Lerp(size[i] * 0.4f, size[i], t), c);
                        break;
                    case Kind.Bolt:
                        renderer.Rect(RenderLayer.Effects, position[i] + new Vector2(0f, size[i] / 2f),
                            new Vector2(0.18f, size[i]), c);
                        renderer.Circle(RenderLayer.Effects, position[i], 0.6f, c);
                        break;
                }
            }
        }

        void Grow()
        {
            var s = position.Length * 2;
            Array.Resize(ref kind, s);
            Array.Resize(ref position, s);
            Array.Resize(ref size, s);
            Array.Resize(ref age, s);
            Array.Resize(ref duration, s);
            Array.Resize(ref color, s);
        }
    }
}
