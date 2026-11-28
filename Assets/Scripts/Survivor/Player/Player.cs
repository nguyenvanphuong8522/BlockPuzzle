using System;
using R3;
using UnityEngine;

namespace Survivor
{
    // Plain C# object: the player is data + logic, drawn by InstancedRenderer like everything else.
    public sealed class Player : IDisposable
    {
        public Vector2 Position { get; set; }
        public Vector2 Facing { get; private set; } = Vector2.right;
        public float Radius { get; }
        public PlayerStats Stats { get; }
        public ReactiveProperty<float> Hp { get; }
        public bool IsDead => Hp.Value <= 0f;
        public float HurtFlash { get; private set; }
        public bool IsMoving { get; private set; }

        // Depth key for the World layer: where the feet touch the ground.
        public float FeetY => Position.y - Radius;

        readonly float hurtCooldown;
        readonly DebugSettings debug;
        readonly SpriteCatalog sprites;
        readonly AudioService audio;
        readonly HapticsService haptics;
        readonly DamageNumberSystem damageNumbers;
        readonly BloodSystem blood;
        readonly ChapterService chapters; // chapter difficulty: damage taken x Damage Taken Scale
        readonly PlayerTuning tuning;
        float hurtTimer;

        public Player(GameConfig config, PlayerTuning tuning, DebugSettings debug, SpriteCatalog sprites, AudioService audio,
            HapticsService haptics, DamageNumberSystem damageNumbers, BloodSystem blood,
            ChapterService chapters)
        {
            this.damageNumbers = damageNumbers;
            this.blood = blood;
            this.chapters = chapters;
            this.tuning = tuning;
            Stats = new PlayerStats(tuning);
            Radius = config.PlayerRadius;
            hurtCooldown = config.PlayerHurtCooldown;
            this.debug = debug;
            this.sprites = sprites;
            this.audio = audio;
            this.haptics = haptics;
            Hp = new ReactiveProperty<float>(Stats.MaxHp);
        }

        public void Tick(Vector2 move, float dt)
        {
            hurtTimer -= dt;
            HurtFlash -= dt;
            // Regeneration (Bình Hồi Phục item): quiet heal, no plus-sign effect.
            if (Stats.HpRegen > 0f && !IsDead && Hp.Value < Stats.MaxHp)
                Hp.Value = Mathf.Min(Stats.MaxHp, Hp.Value + Stats.HpRegen * dt);
            IsMoving = move.sqrMagnitude >= 0.0001f;
            if (!IsMoving) return;
            if (move.sqrMagnitude > 1f) move.Normalize();
            Position += move * Stats.MoveSpeed * dt;
            Facing = move.normalized;
        }

        // Short invulnerability after each hit so a crowd can't drain HP in one frame.
        public void TryHurt(float damage)
        {
            if (hurtTimer > 0f || IsDead) return;
            // Armor (Giáp item): takes less damage, never below 1 so a hit always counts.
            var reduction = Mathf.Clamp(Stats.DamageReduction, 0f, Stats.DamageReductionCap);
            damage = Mathf.Max(1f, damage * chapters.Current.DamageTakenScale * (1f - reduction));
            hurtTimer = hurtCooldown;
            HurtFlash = 0.12f;
            audio.Play(Sfx.Hurt, 0.6f, 0.08f, 0.2f);
            haptics.PlayHit(); // phone / gamepad vibration (also in god mode, so it can be tuned)
            blood.Hero(Position, Radius); // enemy touch or shot (also in god mode)
            damageNumbers.SpawnHurt(Position + new Vector2(0.75f, Radius * 1.2f), damage); // red number, beside the marker (also in god mode)
            if (debug.GodMode || debug.SessionGodMode) return; // still flashes, so you can see you were hit
            Hp.Value = Mathf.Max(0f, Hp.Value - damage);
        }

        public void Heal(float amount)
        {
            Hp.Value = Mathf.Min(Stats.MaxHp, Hp.Value + amount);
            PlayHealEffect();
        }

        // ---- Heal effect: plus signs floating up around the hero. Positions are stored RELATIVE to the hero
        // (like child objects), so they keep following while the hero runs. Settings in PlayerTuning. ----
        const int MaxHealFx = 64;
        readonly Vector2[] healFxOffset = new Vector2[MaxHealFx];
        readonly float[] healFxStart = new float[MaxHealFx];
        readonly float[] healFxSpeed = new float[MaxHealFx];
        readonly float[] healFxPhase = new float[MaxHealFx];
        int healFxNext;

        void PlayHealEffect()
        {
            var n = Mathf.Min(tuning.HealFxCount, MaxHealFx);
            for (var k = 0; k < n; k++)
            {
                var i = healFxNext;
                healFxNext = (healFxNext + 1) % MaxHealFx;
                // Spread around the body, starting low; each one a little late so they rise as a stream.
                healFxOffset[i] = new Vector2(UnityEngine.Random.Range(-1f, 1f) * tuning.HealFxSpread, UnityEngine.Random.Range(-0.6f, 0.2f));
                healFxStart[i] = Time.time + k * 0.03f;
                healFxSpeed[i] = tuning.HealFxRise * UnityEngine.Random.Range(0.75f, 1.25f);
                healFxPhase[i] = UnityEngine.Random.Range(0f, 6.28f);
            }
        }

        void DrawHealEffect(InstancedRenderer renderer)
        {
            var life = tuning.HealFxDuration;
            var now = Time.time;
            var brush = renderer.Brush(RenderLayer.Effects, sprites.HealPlus);
            for (var i = 0; i < MaxHealFx; i++)
            {
                var age = now - healFxStart[i];
                if (age < 0f || age > life || healFxStart[i] == 0f) continue;
                var t = age / life;
                var local = healFxOffset[i] + new Vector2(Mathf.Sin(healFxPhase[i] + age * 6f) * 0.08f, healFxSpeed[i] * age);
                var at = Position + local; // follows the hero
                var pop = t < 0.15f ? t / 0.15f : 1f;                   // grow in
                var size = tuning.HealFxSize * pop * (1f - 0.3f * t);
                var tint = tuning.HealFxTint;
                tint.a *= t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;       // fade out
                if (brush.IsValid) renderer.Draw(brush, at, size, tint);
                else
                {
                    var green = new Color(0.3f, 0.85f, 0.4f, tint.a);
                    renderer.Rect(RenderLayer.Effects, at, new Vector2(size, size * 0.3f), green);
                    renderer.Rect(RenderLayer.Effects, at, new Vector2(size * 0.3f, size), green);
                }
            }
        }

        public void IncreaseMaxHp(float amount)
        {
            Stats.BonusMaxHp += amount;
            Heal(amount);
        }

        const float BlinkEvery = 3.5f, BlinkTime = 0.13f;

        // hero_idle_0..3 loop, last frame = blink. Falls back to a walk frame if the idle art is missing.
        Sprite IdleFrame()
        {
            var idle = sprites.PlayerIdle;
            if (idle == null || idle.IsEmpty || idle.Frames.Length < 2)
                return sprites.Player.Frames[sprites.Player.Frames.Length > 1 ? 1 : 0];
            var loop = idle.Frames.Length - 1;
            if (Time.time % BlinkEvery < BlinkTime) return idle.Frames[loop];
            return idle.Frames[(int)(Time.time * idle.Fps) % loop];
        }

        public void Draw(InstancedRenderer renderer)
        {
            var flash = HurtFlash > 0f ? 1f : 0f;
            renderer.Ellipse(RenderLayer.Shadows, Position + new Vector2(0f, -Radius * 0.9f), new Vector2(Radius * 1.8f, Radius * 0.6f),
                new Color(0f, 0f, 0f, 0.25f));
            if (sprites.Player != null && !sprites.Player.IsEmpty)
            {
                // Walk cycle while moving, breathing idle loop (with a blink now and then) when standing still.
                var frame = IsMoving ? sprites.Player.Frame(Time.time) : IdleFrame();
                renderer.Sprite(RenderLayer.World, frame, Position + new Vector2(0f, Radius * 0.35f),
                    Radius * 2f * sprites.SpriteScale, Color.white, flipX: Facing.x < 0f, flash: flash, sortY: FeetY);
            }
            else
            {
                renderer.Circle(RenderLayer.World, Position, Radius, new Color(0.35f, 0.75f, 1f), flash, FeetY);
                renderer.Circle(RenderLayer.World, Position + Facing * Radius * 0.55f, Radius * 0.28f, Color.white, 0f, FeetY - 0.001f);
            }

            if (debug.ShowPickupRange)
            {
                var r = Stats.MagnetRadius;
                renderer.Circle(RenderLayer.Shadows, Position, r, new Color(0.3f, 0.8f, 1f, 0.12f));
                renderer.Ring(RenderLayer.Overlay, Position, r, 0.04f, new Color(0.3f, 0.8f, 1f, 0.9f));
            }

            if (debug.GodMode)
                renderer.Ring(RenderLayer.Overlay, Position, Radius * 1.5f, 0.12f, new Color(1f, 0.85f, 0.3f, 0.7f));

            DrawHpBar(renderer);
            DrawHealEffect(renderer);

            DrawMarker(renderer);
        }

        // HP bar under the feet: black outline, dark background, fill colored by how much HP is left
        // (green / orange / red, thresholds and colors in PlayerTuning).
        void DrawHpBar(InstancedRenderer renderer)
        {
            var fill = Mathf.Clamp01(Hp.Value / Stats.MaxHp);
            var size = new Vector2(tuning.HpBarWidth, tuning.HpBarHeight);
            var center = Position + new Vector2(0f, -Radius - 0.25f);
            var o = tuning.HpBarOutline;
            if (o > 0f) renderer.Rect(RenderLayer.Overlay, center, size + new Vector2(o * 2f, o * 2f), tuning.HpBarOutlineColor);
            var color = fill <= tuning.HpBarLowBelow ? tuning.HpBarLowColor
                : fill <= tuning.HpBarMidBelow ? tuning.HpBarMidColor
                : tuning.HpBarHighColor;
            renderer.Bar(RenderLayer.Overlay, center, size, fill, tuning.HpBarBackground, color);
        }

        // Blue upside-down triangle bobbing over the head: "this one is you".
        void DrawMarker(InstancedRenderer renderer)
        {
            var at = Position + new Vector2(0f, tuning.MarkerHeight + Mathf.Sin(Time.time * 4f) * 0.06f);
            if (sprites.HeroMarker != null)
                renderer.Sprite(RenderLayer.Overlay, sprites.HeroMarker, at, tuning.MarkerSize, tuning.MarkerTint);
            else
                renderer.Circle(RenderLayer.Overlay, at, tuning.MarkerSize * 0.4f, new Color(0.2f, 0.55f, 1f) * tuning.MarkerTint);
        }

        public void Dispose() => Hp.Dispose();
    }
}
