using UnityEngine;

namespace Survivor
{
    // Boss fight arena. When a boss appears:
    //   - a square wall closes in around the hero (size etc. in PlayerTuning, "Đấu boss")
    //   - the boss is brought inside, every other enemy shatters and vanishes (no rewards), normal spawning stops
    //   - the hero and the boss can't leave; touching the wall hurts the hero and pushes them back in
    // When the boss dies the wall disappears and spawning resumes (its experience drops as a trail, see GemSystem.SpawnTrail).
    public sealed class BossArenaSystem
    {
        const float CloseInTime = 0.45f;    // the wall shrinks from 1.4x to its size when it appears
        const float FadeOutTime = 0.35f;

        readonly EnemySystem enemies;
        readonly EnemySpawner spawner;
        readonly Player player;
        readonly PlayerTuning tuning;
        readonly EffectSystem effects;
        readonly CameraShake shake;
        readonly SpriteCatalog sprites;

        Vector2 center;
        float age;
        float fadeOut = -1f;              // >= 0 while the wall fades away after the boss died
        float wallSizeAtEnd;

        public bool Active { get; private set; }
        public Vector2 Center => center;
        public float HalfSize => tuning.BossArenaSize * 0.5f;

        public BossArenaSystem(EnemySystem enemies, EnemySpawner spawner, Player player, PlayerTuning tuning,
            EffectSystem effects, CameraShake shake, SpriteCatalog sprites)
        {
            this.sprites = sprites;
            this.enemies = enemies;
            this.spawner = spawner;
            this.player = player;
            this.tuning = tuning;
            this.effects = effects;
            this.shake = shake;
        }

        // Run right after enemies moved, so nobody is drawn outside the wall.
        public void Tick(float dt)
        {
            if (fadeOut >= 0f) fadeOut += dt;

            if (!Active)
            {
                if (enemies.BossIndex >= 0) Begin();
                return;
            }

            if (enemies.BossIndex < 0)
            {
                End();
                return;
            }

            age += dt;
            KeepBossInside();
            KeepHeroInside();
        }

        void Begin()
        {
            Active = true;
            age = 0f;
            fadeOut = -1f;
            center = player.Position;
            spawner.Paused = true;

            // Everyone but the boss vanishes (they shatter, but give nothing).
            enemies.ClearAllButBoss();

            // Bring the boss inside, in the upper part of the arena.
            var b = enemies.BossIndex;
            var inside = HalfSize - enemies.Radius[b] - 0.5f;
            enemies.Position[b] = center + new Vector2(0f, Mathf.Max(0f, inside * 0.7f));
            effects.Ring(enemies.Position[b], enemies.Radius[b] * 2.5f, new Color(1f, 0.3f, 0.2f), 0.5f);
            shake.Add(0.4f);
        }

        void End()
        {
            Active = false;
            spawner.Paused = false;
            fadeOut = 0f;
            wallSizeAtEnd = tuning.BossArenaSize;
        }

        void KeepBossInside()
        {
            var b = enemies.BossIndex;
            var limit = Mathf.Max(0f, HalfSize - enemies.Radius[b]);
            var p = enemies.Position[b];
            p.x = Mathf.Clamp(p.x, center.x - limit, center.x + limit);
            p.y = Mathf.Clamp(p.y, center.y - limit, center.y + limit);
            enemies.Position[b] = p;
        }

        // The hero's center can't pass the wall line. Touching the line (body overlapping it) hurts and pushes inward.
        void KeepHeroInside()
        {
            var half = HalfSize;
            var r = player.Radius;
            var p = player.Position;
            p.x = Mathf.Clamp(p.x, center.x - half, center.x + half);
            p.y = Mathf.Clamp(p.y, center.y - half, center.y + half);

            var push = Vector2.zero;
            if (p.x - r < center.x - half) push.x += 1f;
            if (p.x + r > center.x + half) push.x -= 1f;
            if (p.y - r < center.y - half) push.y += 1f;
            if (p.y + r > center.y + half) push.y -= 1f;
            if (push != Vector2.zero)
            {
                player.TryHurt(tuning.BossArenaWallDamage); // has its own short invulnerability, so it ticks, not drains
                p += push.normalized * tuning.BossArenaWallPush;
                effects.Burst(p - push.normalized * r, 0.35f, tuning.BossArenaWallColor, 0.15f);
            }
            player.Position = p;
        }

        public void Draw(InstancedRenderer renderer)
        {
            float size, alpha;
            if (Active)
            {
                var k = Mathf.Clamp01(age / CloseInTime);
                size = tuning.BossArenaSize * Mathf.Lerp(1.4f, 1f, 1f - (1f - k) * (1f - k)); // ease-out close-in
                alpha = k;
            }
            else if (fadeOut >= 0f && fadeOut < FadeOutTime)
            {
                var k = fadeOut / FadeOutTime;
                size = wallSizeAtEnd * (1f + k * 0.15f);
                alpha = 1f - k;
            }
            else return;

            var color = tuning.BossArenaWallColor;
            color.a *= alpha * (0.75f + 0.25f * Mathf.Sin(Time.time * 6f)); // slow pulse
            var t = tuning.BossArenaWallThickness;
            var h = size * 0.5f;
            // With fence art the red line lies on the ground under the fence; without it, it is the wall itself.
            var fence = sprites.BarbedFence;
            var lineLayer = fence != null ? RenderLayer.Shadows : RenderLayer.Overlay;
            // Four sides (the top/bottom ones run past the corners so they close neatly).
            renderer.Rect(lineLayer, center + new Vector2(0f, h), new Vector2(size + t, t), color);
            renderer.Rect(lineLayer, center - new Vector2(0f, h), new Vector2(size + t, t), color);
            renderer.Rect(lineLayer, center + new Vector2(h, 0f), new Vector2(t, size - t), color);
            renderer.Rect(lineLayer, center - new Vector2(h, 0f), new Vector2(t, size - t), color);
            // Faint tint of the floor inside, so the arena reads as a zone.
            var fill = color;
            fill.a *= 0.08f;
            renderer.Rect(RenderLayer.Shadows, center, new Vector2(size, size), fill);

            if (fence != null) DrawFence(renderer, fence, size, alpha);
        }

        // Barbed-wire fence along the four sides: whole segments per side, plus a post closing each corner.
        // Top and bottom are seen from the front; left and right are the same art turned 90 degrees.
        void DrawFence(InstancedRenderer renderer, Sprite fence, float size, float alpha)
        {
            var brush = renderer.Brush(RenderLayer.Effects, fence);
            var height = tuning.BossArenaFenceHeight;
            var n = Mathf.Max(1, Mathf.RoundToInt(size / tuning.BossArenaFenceSegment));
            var seg = size / n;
            var tint = tuning.BossArenaFenceTint;
            tint.a *= alpha;
            var h = size * 0.5f;
            // Just the post (left 36 of 256 px of the art), for the far end of each side.
            var uv = brush.Uv;
            var postUv = new Vector4(uv.x, uv.y, uv.z * 36f / 256f, uv.w);
            var postWidth = seg * 36f / 256f;
            var lift = height * 0.4f; // the posts stand on the wall line

            for (var k = 0; k < n; k++)
            {
                var along = -h + seg * (k + 0.5f);
                renderer.DrawRotated(brush, uv, center + new Vector2(along, h + lift), new Vector2(seg, height), 0f, tint);   // top
                renderer.DrawRotated(brush, uv, center + new Vector2(along, -h + lift), new Vector2(seg, height), 0f, tint);  // bottom
                renderer.DrawRotated(brush, uv, center + new Vector2(-h, along), new Vector2(seg, height), Mathf.PI * 0.5f, tint); // left
                renderer.DrawRotated(brush, uv, center + new Vector2(h, along), new Vector2(seg, height), Mathf.PI * 0.5f, tint);  // right
            }
            renderer.DrawRotated(brush, postUv, center + new Vector2(h + postWidth * 0.5f, h + lift), new Vector2(postWidth, height), 0f, tint);
            renderer.DrawRotated(brush, postUv, center + new Vector2(h + postWidth * 0.5f, -h + lift), new Vector2(postWidth, height), 0f, tint);
        }
    }
}
