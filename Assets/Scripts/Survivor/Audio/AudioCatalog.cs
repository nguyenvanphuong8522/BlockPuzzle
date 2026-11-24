using System;
using UnityEngine;

namespace Survivor
{
    public enum Sfx
    {
        Shoot, Hit, EnemyDie, BossDie, Hurt, Lightning, Wave,
        Gem, Heal, Magnet, LevelUp, Chest, Click, BossSpawn, Victory, GameOver, Throw, Slash, Cast, Explode, Smg, Sling, Boing
    }

    // Sound files used by the game. Filled by "Survivor/Setup Art & Audio" from Assets/Audio by file name
    // (sfx_enemy_die.wav -> EnemyDie). Edit Assets/Resources/AudioCatalog.asset to swap sounds.
    [CreateAssetMenu(menuName = "Survivor/Audio Catalog", fileName = "AudioCatalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public Sfx Id;
            public AudioClip Clip;
            [Range(0f, 1f)] public float Volume = 0.7f;
            [Tooltip("How many copies of this sound may play at the same time. When full, the oldest copy is cut. 0 = default.")]
            [Range(0, 8)] public int MaxVoices = 0;
            [Tooltip("Shortest time between two starts of this sound (seconds). Repeats closer than this are skipped.")]
            [Range(0f, 0.5f)] public float MinInterval = 0f;
        }

        public Entry[] Sounds = new Entry[0];
        public AudioClip Music;
        [Range(0f, 1f)] public float MusicVolume = 0.35f;

        public static AudioCatalog LoadOrEmpty()
        {
            var catalog = Resources.Load<AudioCatalog>("AudioCatalog");
            return catalog != null ? catalog : CreateInstance<AudioCatalog>();
        }
    }
}
