using System;
using MessagePipe;
using UnityEngine;

namespace Survivor
{
    // Experience gems dropped by enemies. Listens to EnemyKilledEvent through MessagePipe.
    public sealed class GemSystem : IDisposable
    {
        const float PickupDistance = 0.3f;

        readonly GameConfig config;
        readonly Player player;
        readonly PlayerProgress progress;
        readonly SpriteCatalog sprites;
        readonly AudioService audio;
        readonly IDisposable subscription;

        int count;
        Vector2[] position = new Vector2[256];
        int[] value = new int[256];
        float[] speed = new float[256]; // 0 = resting, > 0 = flying to the player

        public int Count => count;

        public GemSystem(GameConfig config, Player player, PlayerProgress progress, SpriteCatalog sprites,
            AudioService audio, ISubscriber<EnemyKilledEvent> killed)
        {
            this.config = config;
            this.player = player;
            this.progress = progress;
            this.sprites = sprites;
            this.audio = audio;
            subscription = killed.Subscribe(e => Spawn(e.Position, e.Xp));
        }

        public void Spawn(Vector2 at, int xp)
        {
            // Too many gems on the floor: merge into an existing one instead of adding more.
            if (count >= config.MaxGems)
            {
                value[UnityEngine.Random.Range(0, count)] += xp;
                return;
            }
            if (count == position.Length) Grow();
            var i = count++;
            position[i] = at;
            value[i] = xp;
            speed[i] = 0f;
        }

        // Pulls every gem on the map to the player (e.g. a magnet pickup later).
        public void AttractAll()
        {
            for (var i = 0; i < count; i++) speed[i] = Mathf.Max(speed[i], 6f);
        }

        public void Tick(float dt)
        {
            var playerPos = player.Position;
            var magnetSq = player.Stats.MagnetRadius * player.Stats.MagnetRadius;
            var collect = player.Radius + PickupDistance;

            for (var i = count - 1; i >= 0; i--)
            {
                var d = playerPos - position[i];
                var sq = d.sqrMagnitude;
                if (speed[i] <= 0f && sq > magnetSq) continue;

                if (sq <= collect * collect)
                {
                    progress.AddXp(value[i]);
                    audio.Play(Sfx.Gem, 0.45f, 0.12f, 0.04f);
                    RemoveAt(i);
                    continue;
                }

                speed[i] = Mathf.Max(speed[i], 4f) + 25f * dt;
                position[i] += d / Mathf.Sqrt(sq) * speed[i] * dt;
            }
        }

        void RemoveAt(int i)
        {
            var last = --count;
            position[i] = position[last];
            value[i] = value[last];
            speed[i] = speed[last];
        }

        public void Draw(InstancedRenderer renderer)
        {
            for (var i = 0; i < count; i++)
            {
                var v = value[i];
                var color = v >= 5 ? new Color(1f, 0.35f, 0.35f) : v >= 2 ? new Color(0.4f, 1f, 0.5f) : new Color(0.4f, 0.75f, 1f);
                var size = v >= 5 ? 0.2f : 0.14f;
                if (sprites.Gem != null) renderer.Sprite(RenderLayer.Gems, sprites.Gem, position[i], size * 2.6f, color);
                else renderer.Circle(RenderLayer.Gems, position[i], size, color);
            }
        }

        void Grow()
        {
            var size = position.Length * 2;
            Array.Resize(ref position, size);
            Array.Resize(ref value, size);
            Array.Resize(ref speed, size);
        }

        public void Dispose() => subscription.Dispose();
    }
}
