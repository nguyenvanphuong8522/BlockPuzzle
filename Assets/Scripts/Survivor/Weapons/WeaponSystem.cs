using System;
using System.Collections.Generic;

namespace Survivor
{
    public sealed class WeaponSystem
    {
        public const int MaxWeapons = 4;

        readonly WeaponContext context;
        readonly List<Weapon> weapons = new List<Weapon>();

        public IReadOnlyList<Weapon> Weapons => weapons;

        public WeaponSystem(WeaponContext context) => this.context = context;

        public void Add(Weapon weapon) => weapons.Add(weapon);

        public Weapon Find(Type type)
        {
            foreach (var w in weapons)
                if (w.GetType() == type) return w;
            return null;
        }

        public void Tick(float dt)
        {
            foreach (var w in weapons) w.Tick(dt, context);
        }

        public void Draw(InstancedRenderer renderer)
        {
            foreach (var w in weapons) w.Draw(renderer, context);
        }
    }
}
