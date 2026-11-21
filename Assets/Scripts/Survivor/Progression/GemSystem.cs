using System;
using MessagePipe;
using UnityEngine;

namespace Survivor
{
    // Experience gems dropped by enemies. Listens to EnemyKilledEvent through MessagePipe.
    // Pickup animation (tuned in GemPickupSettings): Resting -> BackOff (jump away) -> Fly (into the player) -> collected.
    public sealed class GemSystem : IDisposable
    {
        const float StreakWindow = 0.4f;   // pickups closer together than this continue the streak
        const int MaxStreakSteps = 12;     // pitch rises a semitone per pickup, up to one octave

        const byte Resting = 0, BackOff = 1, Fly = 2;

        readonly GameConfig config;
        readonly Player player;
        readonly PlayerProgress progress;
        readonly SpriteCatalog sprites;
        readonly AudioService audio;
        readonly GemPickupSettings pickup;
        readonly IDisposable subscription;

        int count;
        Vector2[] position = new Vector2[256];
        int[] value = new int[256];
        byte[] state = new byte[256];
        float[] timer = new float[256];
        Vector2[] start = new Vector2[256];   // where the current phase started
        Vector2[] away = new Vector2[256];    // back-off direction (away from the player)
        int streak;
        float lastPickupTime = -1f;

        public int Count => count;

        public GemSystem(GameConfig config, Player player, PlayerProgress progress, SpriteCatalog sprites,
            AudioService audio, GemPickupSettings pickup, ISubscriber<EnemyKilledEvent> killed)
        {
            this.config = config;
            this.player = player;
            this.progress = progress;
            this.sprites = sprites;
            this.audio = audio;
            this.pickup = pickup;
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
            state[i] = Resting;
        }

        // Debug / tuning helper: a ring of gems just inside the magnet radius, so the pickup animation plays at once.
        public void SpawnTestRing(int amount)
        {
            var radius = player.Stats.MagnetRadius * 0.9f;
            for (var k = 0; k < amount; k++)
            {
                var a = k * Mathf.PI * 2f / amount;
                Spawn(player.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, 1);
            }
        }

        // Pulls every gem on the map to the player (magnet pickup).
        public void AttractAll()
        {
            for (var i = 0; i < count; i++)
                if (state[i] == Resting) Begin(i);
        }

        public void Tick(float dt)
        {
            var playerPos = player.Position;
            var magnetSq = player.Stats.MagnetRadius * player.Stats.MagnetRadius;

            for (var i = count - 1; i >= 0; i--)
            {
                switch (state[i])
                {
                    case Resting:
                        if ((playerPos - position[i]).sqrMagnitude <= magnetSq) Begin(i);
                        break;

                    case BackOff:
                    {
                        timer[i] += dt;
                        var t = pickup.BackDuration > 0f ? Mathf.Clamp01(timer[i] / pickup.BackDuration) : 1f;
                        position[i] = start[i] + away[i] * (pickup.BackDistance * pickup.Back(t));
                        if (t >= 1f)
                        {
                            state[i] = Fly;
                            timer[i] = 0f;
                            start[i] = position[i];
                        }
                        break;
                    }

                    case Fly:
                    {
                        timer[i] += dt;
                        var t = Mathf.Clamp01(timer[i] / pickup.FlyDuration);
                        // Target is the player's current position, so the gem still lands if the player moves.
                        position[i] = Vector2.LerpUnclamped(start[i], playerPos, pickup.Fly(t));
                        if (t >= 1f)
                        {
                            progress.AddXp(value[i]);
                            PlayPickupSound();
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

        // Each pickup in quick succession plays a semitone higher: collecting a pile sounds like a little melody.
        void PlayPickupSound()
        {
            var now = Time.time;
            if (now - lastPickupTime < 0.035f) return; // several gems in one frame: one sound, one step
            streak = now - lastPickupTime < StreakWindow ? Mathf.Min(streak + 1, MaxStreakSteps) : 0;
            lastPickupTime = now;
            audio.PlayAtPitch(Sfx.Gem, Mathf.Pow(2f, streak / 12f), 1f, 0f);
        }

        void RemoveAt(int i)
        {
            var last = --count;
            position[i] = position[last];
            value[i] = value[last];
            state[i] = state[last];
            timer[i] = timer[last];
            start[i] = start[last];
            away[i] = away[last];
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
            Array.Resize(ref state, size);
            Array.Resize(ref timer, size);
            Array.Resize(ref start, size);
            Array.Resize(ref away, size);
        }

        public void Dispose() => subscription.Dispose();
    }
}
