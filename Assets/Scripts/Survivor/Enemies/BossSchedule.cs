using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Which boss comes when: Assets/Resources/Bosses/BossSchedule.asset. Times are play-time seconds (the clock stops
    // during a boss fight). Each boss has its own stats / skills in Resources/Bosses/<Id>/ (BossStats / BossSettings).
    [CreateAssetMenu(menuName = "Survivor/Boss Schedule", fileName = "BossSchedule")]
    public sealed class BossSchedule : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Play time (seconds) the boss appears.")]
            [Min(0f)] public float Time = 180f;
            [Tooltip("Boss Id (GameConfig): VuaCua = Giant King Crab, CaMapBua = Hammerhead Shark, TomTit = Giant Mantis Shrimp.")]
            public string BossId = "VuaCua";
        }

        [Tooltip("In time order. A boss can appear several times; HP grows with every boss fight (Hp Growth Per Appearance).")]
        public List<Entry> Bosses = new List<Entry>
        {
            new Entry { Time = 180f, BossId = "VuaCua" },
            new Entry { Time = 360f, BossId = "CaMapBua" },
            new Entry { Time = 540f, BossId = "TomTit" },
        };

        public static BossSchedule LoadOrDefault()
        {
            var schedule = Resources.Load<BossSchedule>("Bosses/BossSchedule");
            return schedule != null ? schedule : CreateInstance<BossSchedule>();
        }
    }
}
