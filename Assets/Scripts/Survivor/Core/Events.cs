using UnityEngine;

namespace Survivor
{
    public readonly struct EnemyKilledEvent
    {
        public readonly Vector2 Position;
        public readonly int Xp;
        public readonly bool IsBoss;

        public EnemyKilledEvent(Vector2 position, int xp, bool isBoss)
        {
            Position = position;
            Xp = xp;
            IsBoss = isBoss;
        }
    }
}
