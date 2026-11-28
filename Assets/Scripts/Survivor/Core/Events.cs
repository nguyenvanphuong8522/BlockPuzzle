using UnityEngine;

namespace Survivor
{
    public readonly struct EnemyKilledEvent
    {
        public readonly Vector2 Position;
        public readonly int Xp;
        public readonly bool IsBoss;
        public readonly int Type; // index into GameConfig.Enemies
        public readonly bool IsElite; // elite (EliteSystem): extra rewards

        public EnemyKilledEvent(Vector2 position, int xp, bool isBoss, int type, bool isElite = false)
        {
            Type = type;
            IsElite = isElite;
            Position = position;
            Xp = xp;
            IsBoss = isBoss;
        }
    }
}
