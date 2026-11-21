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

        readonly float hurtCooldown;
        readonly DebugSettings debug;
        readonly SpriteCatalog sprites;
        readonly AudioService audio;
        float hurtTimer;

        public Player(GameConfig config, DebugSettings debug, SpriteCatalog sprites, AudioService audio)
        {
            Stats = config.CreatePlayerStats();
            Radius = config.PlayerRadius;
            hurtCooldown = config.PlayerHurtCooldown;
            this.debug = debug;
            this.sprites = sprites;
            this.audio = audio;
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
            if (debug.GodMode || debug.SessionGodMode) return; // still flashes, so you can see you were hit
            Hp.Value = Mathf.Max(0f, Hp.Value - damage);
        }

        public void Heal(float amount) => Hp.Value = Mathf.Min(Stats.MaxHp, Hp.Value + amount);

        public void IncreaseMaxHp(float amount)
        {
            Stats.MaxHp += amount;
            Heal(amount);
        }

        public void Draw(InstancedRenderer renderer)
        {
            var flash = HurtFlash > 0f ? 1f : 0f;
            renderer.Ellipse(RenderLayer.Shadows, Position + new Vector2(0f, -Radius * 0.9f), new Vector2(Radius * 1.8f, Radius * 0.6f),
                new Color(0f, 0f, 0f, 0.25f));
            if (sprites.Player != null && !sprites.Player.IsEmpty)
            {
                // Walk cycle while moving, standing frame when idle.
                var frame = IsMoving ? sprites.Player.Frame(Time.time) : sprites.Player.Frames[sprites.Player.Frames.Length > 1 ? 1 : 0];
                renderer.Sprite(RenderLayer.Player, frame, Position + new Vector2(0f, Radius * 0.35f),
                    Radius * 2f * sprites.SpriteScale, Color.white, flipX: Facing.x < 0f, flash: flash);
            }
            else
            {
                renderer.Circle(RenderLayer.Player, Position, Radius, new Color(0.35f, 0.75f, 1f), flash);
                renderer.Circle(RenderLayer.Player, Position + Facing * Radius * 0.55f, Radius * 0.28f, Color.white);
            }

            if (debug.GodMode)
                renderer.Ring(RenderLayer.Overlay, Position, Radius * 1.5f, 0.12f, new Color(1f, 0.85f, 0.3f, 0.7f));

            renderer.Bar(RenderLayer.Overlay, Position + new Vector2(0f, -Radius - 0.25f), new Vector2(1f, 0.12f),
                Hp.Value / Stats.MaxHp, new Color(0f, 0f, 0f, 0.7f), new Color(0.9f, 0.2f, 0.2f));
        }

        public void Dispose() => Hp.Dispose();
    }
}
