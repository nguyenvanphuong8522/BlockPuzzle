using System;
using UnityEngine;

namespace Survivor
{
    public enum Sfx
    {
        Shoot, Hit, EnemyDie, BossDie, Hurt, Lightning, Wave,
        Gem, Heal, Magnet, LevelUp, Chest, Click, BossSpawn, Victory, GameOver
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
