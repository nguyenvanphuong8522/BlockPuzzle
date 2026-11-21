using UnityEngine;

namespace Survivor
{
    public sealed class EnemyDefinition
    {
        public readonly string Name;
        public readonly float Hp;
        public readonly float Speed;
        public readonly float Damage;
        public readonly float Radius;
        public readonly int Xp;
        public readonly Color Color;
        public readonly float UnlockTime; // seconds into the run before this enemy appears
        public readonly float Weight;     // relative spawn chance (0 = never spawned randomly)
        public readonly bool IsBoss;

        public EnemyDefinition(string name, float hp, float speed, float damage, float radius, int xp,
            Color color, float unlockTime, float weight, bool isBoss = false)
        {
            Name = name;
            Hp = hp;
            Speed = speed;
            Damage = damage;
            Radius = radius;
            Xp = xp;
            Color = color;
            UnlockTime = unlockTime;
            Weight = weight;
            IsBoss = isBoss;
        }
    }
}
