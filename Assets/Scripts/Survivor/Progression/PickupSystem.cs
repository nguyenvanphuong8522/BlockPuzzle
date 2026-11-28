using System;
using MessagePipe;
using UnityEngine;

namespace Survivor
{
    public enum PickupKind { Heal, Magnet, Chest, BossChest } // BossChest = Chest, bigger, with a light pillar and an edge pointer

    // Pickups on the floor: heal (restores HP), magnet (pulls every gem), chest (free upgrade).
    // RewardDropSystem decides what enemies drop (EnemyDropSettings).
    // Unlike gems they are not pulled by the magnet: the player has to come close (Pickup Radius). Then they play
    // the same animation as the gems (GemPickupSettings): jump back a little, then fly into the hero, and only
    // count as picked up when they arrive.
    public sealed class PickupSystem : IDisposable
    {
        const byte Resting = 0, BackOff = 1, Flying = 2;

        readonly GameConfig config;
        readonly Player player;
        readonly GemSystem gems;
        readonly PlayerProgress progress;
        readonly EffectSystem effects;
        readonly SpriteCatalog sprites;
        readonly AudioService audio;
        readonly EnemyDropSettings drops;   // sizes on the ground, pickup radius
        readonly GemPickupSettings anim;     // back-off + fly animation, shared with the gems

        int count;
        PickupKind[] kind = new PickupKind[32];
        Vector2[] position = new Vector2[32];
        float[] age = new float[32];
        byte[] state = new byte[32];
        float[] timer = new float[32];
        Vector2[] start = new Vector2[32];
        Vector2[] away = new Vector2[32];

        public int Count => count;

        public PickupSystem(GameConfig config, Player player, GemSystem gems, PlayerProgress progress,
            EffectSystem effects, SpriteCatalog sprites, AudioService audio, EnemyDropSettings drops,
            GemPickupSettings anim)
        {
            this.anim = anim;
            this.drops = drops;
            this.config = config;
            this.player = player;
            this.gems = gems;
            this.progress = progress;
            this.effects = effects;
            this.sprites = sprites;
            this.audio = audio;
        }

        public void Spawn(PickupKind k, Vector2 at)
        {
            if (count == position.Length)
            {
                var size = count * 2;
                Array.Resize(ref kind, size);
                Array.Resize(ref position, size);
                Array.Resize(ref age, size);
                Array.Resize(ref state, size);
                Array.Resize(ref timer, size);
                Array.Resize(ref start, size);
                Array.Resize(ref away, size);
            }
            var i = count++;
            kind[i] = k;
            position[i] = at;
            age[i] = 0f;
            state[i] = Resting;
        }

        public void Tick(float dt)
        {
            var playerPos = player.Position;
            var reach = player.Radius + drops.PickupRadius;
            for (var i = count - 1; i >= 0; i--)
            {
                age[i] += dt;
                switch (state[i])
                {
                    case Resting:
                        if ((playerPos - position[i]).sqrMagnitude <= reach * reach) Begin(i);
                        break;

                    case BackOff:
                    {
                        // Jump a little away from the hero (same as the gems)...
                        timer[i] += dt;
                        var t = anim.BackDuration > 0f ? Mathf.Clamp01(timer[i] / anim.BackDuration) : 1f;
                        position[i] = start[i] + away[i] * (anim.BackDistance * anim.Back(t));
                        if (t >= 1f)
                        {
                            state[i] = Flying;
                            timer[i] = 0f;
                            start[i] = position[i];
                        }
                        break;
                    }

                    case Flying:
                    {
                        // ...then fly into the hero (his current position, so it lands even if he moves).
                        timer[i] += dt;
                        var t = Mathf.Clamp01(timer[i] / anim.FlyDuration);
                        position[i] = Vector2.LerpUnclamped(start[i], playerPos, anim.Fly(t));
                        if (t >= 1f)
                        {
                            Collect(kind[i]);
                            RemoveAt(i);
                        }
                        break;
                    }
                }
            }
        }

        void Begin(int i)
        {
            var dir = position[i] - player.Position;
            away[i] = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
            start[i] = position[i];
            timer[i] = 0f;
            state[i] = BackOff;
        }

        void RemoveAt(int i)
        {
            var last = --count;
            kind[i] = kind[last];
            position[i] = position[last];
            age[i] = age[last];
            state[i] = state[last];
            timer[i] = timer[last];
            start[i] = start[last];
            away[i] = away[last];
        }

        void Collect(PickupKind k)
        {
            switch (k)
            {
                case PickupKind.Heal:
                    player.Heal(config.HealAmount);
                    audio.Play(Sfx.Heal);
                    effects.Ring(player.Position, 1.2f, new Color(0.4f, 1f, 0.4f));
                    break;
                case PickupKind.Magnet:
                    gems.AttractAll();
                    audio.Play(Sfx.Magnet);
                    effects.Ring(player.Position, 3f, new Color(0.4f, 0.7f, 1f), 0.5f);
                    break;
                case PickupKind.Chest:
                case PickupKind.BossChest:
                    progress.PendingChests++;
                    audio.Play(Sfx.Chest);
                    effects.Burst(player.Position, 2.5f, new Color(1f, 0.85f, 0.3f), 0.5f);
                    break;
            }
        }

        public void Draw(InstancedRenderer renderer)
        {
            for (var i = 0; i < count; i++)
            {
                var bob = state[i] == Resting ? Mathf.Sin(age[i] * 4f) * 0.06f : 0f; // gentle float while waiting on the ground
                var p = position[i] + new Vector2(0f, bob);
                if (state[i] == Resting && drops.HighlightDrops)
                {
                    if (kind[i] == PickupKind.Heal) DrawHighlight(renderer, position[i], drops.HealSize, drops.HealGlowColor, age[i]);
                    else if (kind[i] == PickupKind.Magnet) DrawHighlight(renderer, position[i], drops.MagnetSize, drops.MagnetGlowColor, age[i]);
                }
                switch (kind[i])
                {
                    case PickupKind.Heal:
                        if (sprites.Heal != null) renderer.Sprite(RenderLayer.Pickups, sprites.Heal, p, drops.HealSize, Color.white);
                        else
                        {
                            renderer.Rect(RenderLayer.Pickups, p, new Vector2(0.5f, 0.16f), new Color(1f, 0.3f, 0.3f));
                            renderer.Rect(RenderLayer.Pickups, p, new Vector2(0.16f, 0.5f), new Color(1f, 0.3f, 0.3f));
                        }
                        break;
                    case PickupKind.Magnet:
                        if (sprites.Magnet != null) renderer.Sprite(RenderLayer.Pickups, sprites.Magnet, p, drops.MagnetSize, Color.white);
                        else
                        {
                            renderer.Ring(RenderLayer.Pickups, p, 0.28f, 0.4f, new Color(0.4f, 0.7f, 1f));
                            renderer.Circle(RenderLayer.Pickups, p, 0.1f, Color.white);
                        }
                        break;
                    case PickupKind.Chest:
                        if (sprites.Chest != null) renderer.Sprite(RenderLayer.Pickups, sprites.Chest, p, drops.ChestSize, Color.white);
                        else
                        {
                            renderer.Ring(RenderLayer.Pickups, p, 0.9f + Mathf.Sin(age[i] * 3f) * 0.1f, 0.1f,
                                new Color(1f, 0.85f, 0.3f, 0.6f));
                            renderer.Rect(RenderLayer.Pickups, p, new Vector2(0.8f, 0.6f), new Color(0.55f, 0.35f, 0.15f));
                            renderer.Rect(RenderLayer.Pickups, p, new Vector2(0.8f, 0.1f), new Color(1f, 0.85f, 0.3f));
                        }
                        break;
                    case PickupKind.BossChest:
                        if (state[i] == Resting) DrawBeam(renderer, position[i], age[i]);
                        DrawChest(renderer, p, drops.BossChestSize);
                        if (state[i] == Resting) DrawPointer(renderer, position[i], age[i]);
                        break;
                }
            }
        }

        // Makes a drop stand out from the ground (drawn under it): a dark shadow, a pulsing colored glow, and rings
        // that keep spreading out from it and fading.
        void DrawHighlight(InstancedRenderer renderer, Vector2 at, float size, Color color, float t)
        {
            var pulse = 1f + 0.12f * Mathf.Sin(t * 5f);
            renderer.Ellipse(RenderLayer.Shadows, at - new Vector2(0f, size * 0.42f), new Vector2(size * 0.8f, size * 0.25f),
                new Color(0f, 0f, 0f, 0.3f));
            var glow = color;
            renderer.Circle(RenderLayer.Shadows, at, size * 0.5f * drops.GlowSize * pulse, glow);
            glow.a *= 0.6f;
            renderer.Circle(RenderLayer.Shadows, at, size * 0.5f * drops.GlowSize * 1.35f * pulse, glow);

            if (drops.RingInterval <= 0f) return;
            var k = (t % drops.RingInterval) / drops.RingInterval; // 0..1 for each ring
            var ring = color;
            ring.a = Mathf.Min(1f, color.a * 2f) * (1f - k);
            var radius = size * 0.5f * Mathf.Lerp(0.8f, drops.RingMaxSize, k);
            renderer.Ring(RenderLayer.Shadows, at, radius, Mathf.Clamp01(drops.RingThickness / Mathf.Max(0.05f, radius)), ring);
        }

        void DrawChest(InstancedRenderer renderer, Vector2 p, float size)
        {
            if (sprites.Chest != null) renderer.Sprite(RenderLayer.Pickups, sprites.Chest, p, size, Color.white);
            else renderer.Rect(RenderLayer.Pickups, p, new Vector2(size * 0.9f, size * 0.65f), new Color(0.55f, 0.35f, 0.15f));
        }

        // Pillar of light rising from the boss chest: a wide soft band and a narrow bright core, both fading out
        // toward the top (drawn as stacked slices), plus a glow on the ground. Behind the chest.
        void DrawBeam(InstancedRenderer renderer, Vector2 at, float t)
        {
            var height = drops.BeamHeight;
            if (height <= 0f) return;
            const int slices = 14;
            var pulse = 1f - drops.BeamPulse * (0.5f + 0.5f * Mathf.Sin(t * 3f));
            var bottom = at.y - drops.BossChestSize * 0.3f;
            var slice = height / slices;
            for (var k = 0; k < slices; k++)
            {
                var fade = 1f - (k + 0.5f) / slices;
                fade *= fade;
                var y = bottom + slice * (k + 0.5f);
                var outer = drops.BeamColor;
                outer.a *= fade * pulse;
                renderer.Rect(RenderLayer.Pickups, new Vector2(at.x, y), new Vector2(drops.BeamWidth, slice + 0.01f), outer);
                var core = drops.BeamCoreColor;
                core.a *= fade * pulse;
                renderer.Rect(RenderLayer.Pickups, new Vector2(at.x, y), new Vector2(drops.BeamWidth * 0.35f, slice + 0.01f), core);
            }
            var glow = drops.BeamColor;
            glow.a *= 0.8f * pulse;
            renderer.Ellipse(RenderLayer.Pickups, new Vector2(at.x, bottom), new Vector2(drops.BeamWidth * 1.3f, drops.BeamWidth * 0.45f), glow);
        }

        // Boss chest off screen: an icon (chest in a disc) at the screen edge, with an arrow pointing toward it.
        void DrawPointer(InstancedRenderer renderer, Vector2 at, float t)
        {
            if (!drops.ShowChestPointer) return;
            var v = renderer.ViewRect; // last frame's visible area (world units)
            var r = drops.BossChestSize * 0.5f;
            if (at.x + r >= v.x && at.x - r <= v.z && at.y + r >= v.y && at.y - r <= v.w) return; // on screen

            var center = new Vector2((v.x + v.z) * 0.5f, (v.y + v.w) * 0.5f);
            var size = drops.PointerSize;
            var inset = drops.PointerMargin + size * 0.6f;
            var hx = Mathf.Max(0.1f, (v.z - v.x) * 0.5f - inset);
            var hy = Mathf.Max(0.1f, (v.w - v.y) * 0.5f - inset);
            var d = at - center;
            var k = Mathf.Min(Mathf.Abs(d.x) > 0.0001f ? hx / Mathf.Abs(d.x) : float.MaxValue,
                Mathf.Abs(d.y) > 0.0001f ? hy / Mathf.Abs(d.y) : float.MaxValue);
            var pos = center + d * k;
            var dir = d.normalized;
            var bounce = 1f + 0.08f * Mathf.Sin(t * 6f);

            renderer.Circle(RenderLayer.Text, pos, size * 0.5f * bounce, drops.PointerBackColor);
            renderer.Ring(RenderLayer.Text, pos, size * 0.5f * bounce, 0.12f, drops.PointerColor);
            if (sprites.Chest != null) renderer.Sprite(RenderLayer.Text, sprites.Chest, pos, size * 0.6f, Color.white);
            var tip = pos + dir * (size * (0.5f + 0.25f) * bounce);
            var angle = Mathf.Atan2(dir.y, dir.x);
            if (sprites.UiArrow != null)
                renderer.SpriteRotated(RenderLayer.Text, sprites.UiArrow, tip, size * 0.42f, angle, drops.PointerColor);
            else
                renderer.Line(RenderLayer.Text, tip - dir * 0.15f, tip + dir * 0.15f, size * 0.25f, drops.PointerColor);
        }

        public void Dispose() { }
    }
}
