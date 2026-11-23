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
        readonly PlayerTuning tuning;
        float hurtTimer;

        public Player(GameConfig config, PlayerTuning tuning, DebugSettings debug, SpriteCatalog sprites, AudioService audio,
            HapticsService haptics, DamageNumberSystem damageNumbers)
        {
            this.damageNumbers = damageNumbers;
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
            hurtTimer = hurtCooldown;
            HurtFlash = 0.12f;
            audio.Play(Sfx.Hurt, 0.6f, 0.08f, 0.2f);
            haptics.PlayHit(); // phone / gamepad vibration (also in god mode, so it can be tuned)
            damageNumbers.SpawnHurt(Position + new Vector2(0.75f, Radius * 1.2f), damage); // red number, beside the marker (also in god mode)
            if (debug.GodMode || debug.SessionGodMode) return; // still flashes, so you can see you were hit
            Hp.Value = Mathf.Max(0f, Hp.Value - damage);
        }

        public void Heal(float amount) => Hp.Value = Mathf.Min(Stats.MaxHp, Hp.Value + amount);

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

            renderer.Bar(RenderLayer.Overlay, Position + new Vector2(0f, -Radius - 0.25f), new Vector2(1f, 0.12f),
                Hp.Value / Stats.MaxHp, new Color(0f, 0f, 0f, 0.7f), new Color(0.9f, 0.2f, 0.2f));

            DrawMarker(renderer);
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
