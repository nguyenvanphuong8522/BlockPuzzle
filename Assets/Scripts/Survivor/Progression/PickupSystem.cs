using System;
using MessagePipe;
using UnityEngine;

namespace Survivor
{
    public enum PickupKind { Heal, Magnet, Chest }

    // Pickups on the floor: heal (restores HP), magnet (pulls every gem), chest (free upgrade).
    // RewardDropSystem decides what enemies drop (EnemyDropSettings).
    // Unlike gems they are not pulled by the magnet: the player has to walk over them.
    public sealed class PickupSystem : IDisposable
    {
        readonly GameConfig config;
        readonly Player player;
        readonly GemSystem gems;
        readonly PlayerProgress progress;
        readonly EffectSystem effects;
        readonly SpriteCatalog sprites;
        readonly AudioService audio;
        readonly EnemyDropSettings drops;   // sizes on the ground, pickup radius

        int count;
        PickupKind[] kind = new PickupKind[32];
        Vector2[] position = new Vector2[32];
        float[] age = new float[32];

        public int Count => count;

        public PickupSystem(GameConfig config, Player player, GemSystem gems, PlayerProgress progress,
            EffectSystem effects, SpriteCatalog sprites, AudioService audio, EnemyDropSettings drops)
        {
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
                Array.Resize(ref kind, count * 2);
                Array.Resize(ref position, count * 2);
                Array.Resize(ref age, count * 2);
            }
            var i = count++;
            kind[i] = k;
            position[i] = at;
            age[i] = 0f;
        }

        public void Tick(float dt)
        {
            var reach = player.Radius + drops.PickupRadius;
            for (var i = count - 1; i >= 0; i--)
            {
                age[i] += dt;
                if ((player.Position - position[i]).sqrMagnitude > reach * reach) continue;
                Collect(kind[i]);
                var last = --count;
                kind[i] = kind[last];
                position[i] = position[last];
                age[i] = age[last];
            }
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
                var bob = Mathf.Sin(age[i] * 4f) * 0.06f; // gentle float so drops catch the eye
                var p = position[i] + new Vector2(0f, bob);
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
                }
            }
        }

        public void Dispose() { }
    }
}
