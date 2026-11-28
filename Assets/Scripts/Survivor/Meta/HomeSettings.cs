using UnityEngine;

namespace Survivor
{
    // Home screen and energy: Assets/Resources/HomeSettings.asset (live-tunable).
    [CreateAssetMenu(menuName = "Survivor/Home Settings", fileName = "HomeSettings")]
    public sealed class HomeSettings : ScriptableObject
    {
        [Header("Năng lượng")]
        [Tooltip("Most energy you can hold (refilling over time stops here).")]
        [Min(1)] public int MaxEnergy = 60;
        [Tooltip("Energy a run costs (a chapter can override it).")]
        [Min(0)] public int PlayCost = 5;
        [Tooltip("Seconds to get 1 energy back (also while the game is closed).")]
        [Min(1f)] public float RegenSeconds = 720f;
        [Tooltip("Energy on the very first launch.")]
        [Min(0)] public int StartEnergy = 60;

        [Header("Chương")]
        [Tooltip("Chapters in order. Chapter N opens once chapter N-1 has been survived.")]
        public ChapterData[] Chapters = new ChapterData[0];

        public static HomeSettings LoadOrDefault()
        {
            var settings = Resources.Load<HomeSettings>("HomeSettings");
            return settings != null ? settings : CreateInstance<HomeSettings>();
        }
    }
}
