using UnityEngine;

namespace Survivor
{
    // Boss fight arena. When a boss appears:
    //   - a rectangular wall closes in around the hero (width / height etc. in BossSettings.asset, "Đấu trường")
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
        readonly BossRoster roster;
        BossSettings tuning => roster.Settings;
        BossStats stats => roster.Stats;
        readonly EffectSystem effects;
        readonly CameraShake shake;
        readonly SpriteCatalog sprites;

        Vector2 center;
        float age;
        float fadeOut = -1f;              // >= 0 while the wall fades away after the boss died
        Vector2 wallSizeAtEnd;

        public bool Active { get; private set; }
        public Vector2 Center => center;
        // Up now, or ended this very frame / just before (the boss died): its Center is still the fight's arena.
        public bool UpOrJustEnded => Active || (fadeOut >= 0f && fadeOut < 0.25f);
        public Vector2 Size => new Vector2(tuning.ArenaWidth, tuning.ArenaHeight);
        public Vector2 Half => Size * 0.5f;

        public BossArenaSystem(EnemySystem enemies, EnemySpawner spawner, Player player, BossRoster roster,
            EffectSystem effects, CameraShake shake, SpriteCatalog sprites)
        {
            this.sprites = sprites;
            this.enemies = enemies;
            this.spawner = spawner;
            this.player = player;
            this.roster = roster;
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
            var inside = Half.y - enemies.Radius[b] - 0.5f;
            enemies.Position[b] = center + new Vector2(0f, Mathf.Max(0f, inside * 0.7f));
            effects.Ring(enemies.Position[b], enemies.Radius[b] * 2.5f, new Color(1f, 0.3f, 0.2f), 0.5f);
            shake.Add(0.4f);
        }

        void End()
        {
            Active = false;
            spawner.Paused = false;
            fadeOut = 0f;
            wallSizeAtEnd = Size;
        }

        void KeepBossInside()
        {
            var b = enemies.BossIndex;
            var half = Half;
            var r = enemies.Radius[b];
            var lx = Mathf.Max(0f, half.x - r);
            var ly = Mathf.Max(0f, half.y - r);
            var p = enemies.Position[b];
            p.x = Mathf.Clamp(p.x, center.x - lx, center.x + lx);
            p.y = Mathf.Clamp(p.y, center.y - ly, center.y + ly);
            enemies.Position[b] = p;
        }

        // The hero's center can't pass the wall line. Touching the line (body overlapping it) hurts and pushes inward.
        void KeepHeroInside()
        {
            var half = Half;
            var r = player.Radius;
            var p = player.Position;
            p.x = Mathf.Clamp(p.x, center.x - half.x, center.x + half.x);
            p.y = Mathf.Clamp(p.y, center.y - half.y, center.y + half.y);

            var push = Vector2.zero;
            if (p.x - r < center.x - half.x) push.x += 1f;
            if (p.x + r > center.x + half.x) push.x -= 1f;
            if (p.y - r < center.y - half.y) push.y += 1f;
            if (p.y + r > center.y + half.y) push.y -= 1f;
            if (push != Vector2.zero)
            {
                player.TryHurt(stats.ArenaWallDamage); // has its own short invulnerability, so it ticks, not drains
                p += push.normalized * tuning.ArenaWallPush;
                effects.Burst(p - push.normalized * r, 0.35f, tuning.ArenaWallColor, 0.15f);
            }
            player.Position = p;
        }

        public void Draw(InstancedRenderer renderer)
        {
            Vector2 size;
            float alpha;
            if (Active)
            {
                var k = Mathf.Clamp01(age / CloseInTime);
                size = Size * Mathf.Lerp(1.4f, 1f, 1f - (1f - k) * (1f - k)); // ease-out close-in
                alpha = k;
            }
            else if (fadeOut >= 0f && fadeOut < FadeOutTime)
            {
                var k = fadeOut / FadeOutTime;
                size = wallSizeAtEnd * (1f + k * 0.15f);
                alpha = 1f - k;
            }
            else return;

            var color = tuning.ArenaWallColor;
            color.a *= alpha * (0.75f + 0.25f * Mathf.Sin(Time.time * 6f)); // slow pulse
            var t = tuning.ArenaWallThickness;
            var h = size * 0.5f;
            // With fence art the red line lies on the ground under the fence; without it, it is the wall itself.
            var fence = sprites.BarbedFence;
            var lineLayer = fence != null ? RenderLayer.Shadows : RenderLayer.Overlay;
            // Four sides (the top/bottom ones run past the corners so they close neatly).
            renderer.Rect(lineLayer, center + new Vector2(0f, h.y), new Vector2(size.x + t, t), color);
            renderer.Rect(lineLayer, center - new Vector2(0f, h.y), new Vector2(size.x + t, t), color);
            renderer.Rect(lineLayer, center + new Vector2(h.x, 0f), new Vector2(t, size.y - t), color);
            renderer.Rect(lineLayer, center - new Vector2(h.x, 0f), new Vector2(t, size.y - t), color);
            // Faint tint of the floor inside, so the arena reads as a zone.
            var fill = color;
            fill.a *= 0.08f;
            renderer.Rect(RenderLayer.Shadows, center, size, fill);

            if (fence != null) DrawFence(renderer, fence, size, alpha);
        }

        // Barbed-wire fence along the four sides: whole segments per side, plus a post closing each corner.
        // Top and bottom are seen from the front; left and right are the same art turned 90 degrees.
        void DrawFence(InstancedRenderer renderer, Sprite fence, Vector2 size, float alpha)
        {
            var brush = renderer.Brush(RenderLayer.Effects, fence);
            var height = tuning.ArenaFenceHeight;
            var tint = tuning.ArenaFenceTint;
            tint.a *= alpha;
            var h = size * 0.5f;
            var uv = brush.Uv;
            var postUv = new Vector4(uv.x, uv.y, uv.z * 36f / 256f, uv.w); // just the post (left 36 of 256 px of the art)
            var lift = height * 0.4f; // the posts stand on the wall line

            // Top and bottom: split the width into whole segments.
            var nx = Mathf.Max(1, Mathf.RoundToInt(size.x / tuning.ArenaFenceSegment));
            var segX = size.x / nx;
            for (var k = 0; k < nx; k++)
            {
                var along = -h.x + segX * (k + 0.5f);
                renderer.DrawRotated(brush, uv, center + new Vector2(along, h.y + lift), new Vector2(segX, height), 0f, tint);
                renderer.DrawRotated(brush, uv, center + new Vector2(along, -h.y + lift), new Vector2(segX, height), 0f, tint);
            }
            // Left and right: split the height into whole segments.
            var ny = Mathf.Max(1, Mathf.RoundToInt(size.y / tuning.ArenaFenceSegment));
            var segY = size.y / ny;
            for (var k = 0; k < ny; k++)
            {
                var along = -h.y + segY * (k + 0.5f);
                renderer.DrawRotated(brush, uv, center + new Vector2(-h.x, along), new Vector2(segY, height), Mathf.PI * 0.5f, tint);
                renderer.DrawRotated(brush, uv, center + new Vector2(h.x, along), new Vector2(segY, height), Mathf.PI * 0.5f, tint);
            }
            var postWidth = segX * 36f / 256f;
            renderer.DrawRotated(brush, postUv, center + new Vector2(h.x + postWidth * 0.5f, h.y + lift), new Vector2(postWidth, height), 0f, tint);
            renderer.DrawRotated(brush, postUv, center + new Vector2(h.x + postWidth * 0.5f, -h.y + lift), new Vector2(postWidth, height), 0f, tint);
        }
    }
}
