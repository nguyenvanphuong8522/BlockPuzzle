using System;
using MessagePipe;
using UnityEngine;

namespace Survivor
{
    // Plays sound effects from a small pool of AudioSources.
    // Each sound has a minimum interval, so 500 enemies dying in one frame still makes one "pop", not 500.
    public sealed class AudioService : IDisposable
    {
        const int Voices = 16;
        const string MusicVolumeKey = "audio.musicVolume";
        const string SfxVolumeKey = "audio.sfxVolume";

        readonly AudioCatalog catalog;
        readonly AudioClip[] clips;
        readonly float[] volumes;
        readonly float[] lastPlayed;
        readonly AudioSource[] sources = new AudioSource[Voices];
        readonly AudioSource music;
        readonly GameObject root;
        readonly IDisposable subscription;
        int nextVoice;
        float musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        float sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);

        public AudioService(AudioCatalog catalog, ISubscriber<EnemyKilledEvent> killed)
        {
            this.catalog = catalog;
            var count = Enum.GetValues(typeof(Sfx)).Length;
            clips = new AudioClip[count];
            volumes = new float[count];
            lastPlayed = new float[count];
            for (var i = 0; i < count; i++) lastPlayed[i] = -1f;
            foreach (var e in catalog.Sounds)
            {
                clips[(int)e.Id] = e.Clip;
                volumes[(int)e.Id] = e.Volume;
            }

            root = new GameObject("Audio");
            for (var i = 0; i < Voices; i++)
            {
                sources[i] = root.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
            }
            music = root.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;

            subscription = killed.Subscribe(e => Play(e.IsBoss ? Sfx.BossDie : Sfx.EnemyDie, 1f, 0.15f, 0.05f));
        }

        // pitchJitter adds variety to repeated sounds; minInterval limits how often this sound can start.
        public void Play(Sfx id, float volume = 1f, float pitchJitter = 0.05f, float minInterval = 0.03f) =>
            PlayAtPitch(id, 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter), volume, minInterval);

        // Plays at an exact pitch (1 = original, 2 = one octave up), e.g. for rising pickup streaks.
        public void PlayAtPitch(Sfx id, float pitch, float volume = 1f, float minInterval = 0.03f)
        {
            var i = (int)id;
            var clip = clips[i];
            if (clip == null) return;
            var now = Time.unscaledTime;
            if (now - lastPlayed[i] < minInterval) return;
            lastPlayed[i] = now;

            var source = sources[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            source.pitch = pitch;
            source.PlayOneShot(clip, volumes[i] * volume * sfxVolume);
        }

        public void PlayMusic()
        {
            if (catalog.Music == null) return;
            music.clip = catalog.Music;
            music.volume = catalog.MusicVolume * musicVolume;
            music.Play();
        }

        public void StopMusic() => music.Stop();

        // Player settings (0..1), saved between sessions. Shown as sliders in the pause menu.
        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                music.volume = catalog.MusicVolume * musicVolume;
                PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
            }
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
            }
        }

        public void SaveSettings() => PlayerPrefs.Save();

        public bool Muted
        {
            get => AudioListener.volume <= 0f;
            set => AudioListener.volume = value ? 0f : 1f;
        }

        public void Dispose()
        {
            subscription.Dispose();
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
