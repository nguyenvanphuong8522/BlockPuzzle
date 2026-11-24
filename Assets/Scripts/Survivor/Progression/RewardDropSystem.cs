using System;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Survivor
{
    // Drops rewards when an enemy dies, using the per-type lists in EnemyDropSettings.
    // Every reward line is rolled on its own (so a death can drop a gem AND a magnet).
    public sealed class RewardDropSystem : IInitializable, IDisposable
    {
        readonly EnemyDropSettings settings;
        readonly GemSystem gems;
        readonly PickupSystem pickups;
        readonly Player player;
        readonly PlayerTuning tuning;
        readonly IDisposable subscription;

        public RewardDropSystem(EnemyDropSettings settings, GemSystem gems, PickupSystem pickups, Player player,
            PlayerTuning tuning, ISubscriber<EnemyKilledEvent> killed)
        {
            this.player = player;
            this.tuning = tuning;
            this.settings = settings;
            this.gems = gems;
            this.pickups = pickups;
            subscription = killed.Subscribe(OnEnemyKilled);
        }

        void OnEnemyKilled(EnemyKilledEvent e)
        {
            foreach (var reward in settings.RewardsFor(e.Type))
            {
                if (reward == null || UnityEngine.Random.value * 100f >= reward.ChancePercent) continue;
                Give(reward, e.Position, e.IsBoss);
            }
        }

        void Give(EnemyDropSettings.Reward reward, Vector2 at, bool fromBoss)
        {
            switch (reward.Type)
            {
                case RewardType.Gem:
                    if (fromBoss) gems.SpawnTrail(at, player.Position, reward.Amount, tuning.BossTrailGems); // a line of gems
                    else gems.Spawn(at, reward.Amount); // one gem holding all the experience
                    break;
                case RewardType.Heal:
                    SpawnPickups(PickupKind.Heal, at, reward.Amount);
                    break;
                case RewardType.Magnet:
                    SpawnPickups(PickupKind.Magnet, at, reward.Amount);
                    break;
                case RewardType.Chest:
                    SpawnPickups(PickupKind.Chest, at, reward.Amount);
                    break;
            }
        }

        // Several of the same pickup are spread around the spot so they don't stack.
        void SpawnPickups(PickupKind kind, Vector2 at, int amount)
        {
            for (var k = 0; k < amount; k++)
                pickups.Spawn(kind, k == 0 ? at : at + UnityEngine.Random.insideUnitCircle * 0.6f);
        }

        public void Initialize() { } // only here so VContainer creates it as an entry point

        public void Dispose() => subscription.Dispose();
    }
}
