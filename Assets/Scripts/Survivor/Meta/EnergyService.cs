using System;
using UnityEngine;

namespace Survivor
{
    // Energy for runs (HomeSettings): Max 60, a run costs 5, +1 every Regen Seconds while below the max, counted
    // from real time so it also refills while the game is closed. Saved in PlayerPrefs.
    public sealed class EnergyService
    {
        const string ValueKey = "energy.value";
        const string TimeKey = "energy.time"; // UTC ticks of the last +1 (or of when it dropped below the max)

        readonly HomeSettings settings;
        int value;
        long since;

        public EnergyService(HomeSettings settings)
        {
            this.settings = settings;
            value = PlayerPrefs.GetInt(ValueKey, settings.StartEnergy);
            since = long.TryParse(PlayerPrefs.GetString(TimeKey, ""), out var t) ? t : Now;
            Update();
        }

        static long Now => DateTime.UtcNow.Ticks;

        public int Max => settings.MaxEnergy;

        public int Current
        {
            get
            {
                Update();
                return value;
            }
        }

        public bool IsFull => Current >= Max;

        // Seconds until the next +1 (0 when full).
        public float SecondsToNext
        {
            get
            {
                Update();
                if (value >= Max) return 0f;
                var elapsed = (float)TimeSpan.FromTicks(Now - since).TotalSeconds;
                return Mathf.Max(0f, settings.RegenSeconds - elapsed);
            }
        }

        public bool CanPay(int cost) => Current >= cost;

        public bool TryConsume(int cost)
        {
            Update();
            if (value < cost) return false;
            if (value >= Max) since = Now; // the refill clock starts when it drops below the max
            value -= cost;
            Save();
            return true;
        }

        // Debug / rewards: may go above the max (like gifts in other games); refilling waits until below it again.
        public void Add(int amount)
        {
            Update();
            value = Mathf.Max(0, value + amount);
            Save();
        }

        public void Refill()
        {
            value = Mathf.Max(value, Max);
            since = Now;
            Save();
        }

        // Adds the energy earned since the last +1.
        void Update()
        {
            if (value >= Max)
            {
                since = Now;
                return;
            }
            var step = TimeSpan.FromSeconds(Mathf.Max(1f, settings.RegenSeconds)).Ticks;
            var gained = (Now - since) / step;
            if (gained <= 0) return;
            value = (int)Math.Min(Max, value + gained);
            since = value >= Max ? Now : since + gained * step;
            Save();
        }

        void Save()
        {
            PlayerPrefs.SetInt(ValueKey, value);
            PlayerPrefs.SetString(TimeKey, since.ToString());
            PlayerPrefs.Save();
        }
    }
}
