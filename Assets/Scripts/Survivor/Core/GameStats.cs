using System;
using MessagePipe;
using R3;

namespace Survivor
{
    public sealed class GameStats : IDisposable
    {
        public float Elapsed { get; set; }
        public ReactiveProperty<int> Kills { get; } = new ReactiveProperty<int>(0);

        readonly IDisposable subscription;

        public GameStats(ISubscriber<EnemyKilledEvent> killed)
        {
            subscription = killed.Subscribe(_ => Kills.Value++);
        }

        public void Dispose()
        {
            subscription.Dispose();
            Kills.Dispose();
        }
    }
}
