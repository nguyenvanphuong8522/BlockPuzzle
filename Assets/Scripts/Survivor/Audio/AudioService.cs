using System;
using MessagePipe;
using UnityEngine;

namespace Survivor
{
    // Plays sound effects from a fixed pool of AudioSources ("voices"), so a big fight never turns into noise:
    //   - each voice plays exactly ONE sound (clip + Play, not PlayOneShot, which piles sounds up on one source
    //     and changes the pitch of everything already playing on it)
    //   - each sound has a max number of copies playing at once (AudioCatalog: Max Voices); when full, its oldest
    //     copy is cut and restarted
    //   - each sound has a minimum time between starts (AudioCatalog: Min Interval)
    //   - copies of the same sound get a little quieter the more of them are playing
    //   - when every voice is busy, the oldest one is reused
    public sealed class AudioService : IDisposable
    {
        const int Voices = 24;
        const string MusicVolumeKey = "audio.musicVolume";
        const string SfxVolumeKey = "audio.sfxVolume";

        readonly AudioCatalog catalog;
        readonly AudioClip[] clips;
        readonly float[] volumes;
        readonly int[] maxVoices;
        readonly float[] minIntervals;
        readonly float[] lastPlayed;
        readonly AudioSource[] sources = new AudioSource[Voices];
        readonly int[] voiceSound = new int[Voices];      // which Sfx each voice is playing (-1 = none)
        readonly float[] voiceStart = new float[Voices];
        readonly AudioSource music;
        readonly GameObject root;
        readonly IDisposable subscription;
        float musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        float sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);

        public AudioService(AudioCatalog catalog, ISubscriber<EnemyKilledEvent> killed)
        {
            this.catalog = catalog;
            var count = Enum.GetValues(typeof(Sfx)).Length;
            clips = new AudioClip[count];
            volumes = new float[count];
            maxVoices = new int[count];
            minIntervals = new float[count];
            lastPlayed = new float[count];
            for (var i = 0; i < count; i++)
            {
                lastPlayed[i] = -1f;
                maxVoices[i] = DefaultMaxVoices((Sfx)i);
            }
            foreach (var e in catalog.Sounds)
            {
                if (e == null) continue;
                var i = (int)e.Id;
                clips[i] = e.Clip;
                volumes[i] = e.Volume;
                if (e.MaxVoices > 0) maxVoices[i] = e.MaxVoices;
                minIntervals[i] = e.MinInterval;
            }

            root = new GameObject("Audio");
            for (var i = 0; i < Voices; i++)
            {
                sources[i] = root.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                voiceSound[i] = -1;
            }
            music = root.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;

            subscription = killed.Subscribe(e => Play(e.IsBoss ? Sfx.BossDie : Sfx.EnemyDie, 1f, 0.15f, 0.05f));
        }

        // Used when AudioCatalog leaves Max Voices at 0. Sounds that repeat a lot get few voices.
        public static int DefaultMaxVoices(Sfx id) => id switch
        {
            Sfx.Hit => 3,
            Sfx.EnemyDie => 3,
            Sfx.Gem => 4,
            Sfx.Smg => 2,
            Sfx.Shoot => 2,
            Sfx.Explode => 3,
            Sfx.Boing => 2,
            Sfx.Sling => 2,
            Sfx.Throw => 2,
            Sfx.Slash => 2,
            Sfx.Cast => 2,
            Sfx.Lightning => 2,
            _ => 1, // UI, level-up, hurt, boss... one at a time
        };

        // pitchJitter adds variety to repeated sounds; minInterval limits how often this sound can start
        // (the larger of this and the catalog's Min Interval wins).
        public void Play(Sfx id, float volume = 1f, float pitchJitter = 0.05f, float minInterval = 0.03f) =>
            PlayAtPitch(id, 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter), volume, minInterval);

        // Plays at an exact pitch (1 = original, 2 = one octave up), e.g. for rising pickup streaks.
        public void PlayAtPitch(Sfx id, float pitch, float volume = 1f, float minInterval = 0.03f)
        {
            var i = (int)id;
            var clip = clips[i];
            if (clip == null) return;
            var now = Time.unscaledTime;
            if (now - lastPlayed[i] < Mathf.Max(minInterval, minIntervals[i])) return;
            lastPlayed[i] = now;

            // How many copies of this sound are playing, and the oldest one.
            int playing = 0, oldestSame = -1, free = -1, oldestAny = 0, busyCount = 0;
            for (var v = 0; v < Voices; v++)
            {
                var busy = sources[v].isPlaying;
                if (!busy)
                {
                    if (free < 0) free = v;
                    continue;
                }
                busyCount++;
                if (voiceStart[v] < voiceStart[oldestAny] || !sources[oldestAny].isPlaying) oldestAny = v;
                if (voiceSound[v] != i) continue;
                playing++;
                if (oldestSame < 0 || voiceStart[v] < voiceStart[oldestSame]) oldestSame = v;
            }

            // Full for this sound: cut its oldest copy. Otherwise a free voice, or the oldest voice of all.
            var voice = playing >= maxVoices[i] && oldestSame >= 0 ? oldestSame : free >= 0 ? free : oldestAny;
            if (voice == oldestSame) playing--;

            var source = sources[voice];
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            // A few copies of the same sound add up loud; soften each extra one.
            source.volume = volumes[i] * volume * sfxVolume / (1f + 0.35f * Mathf.Max(0, playing));
            source.Play();
            voiceSound[voice] = i;
            voiceStart[voice] = now;
            PeakVoices = Mathf.Max(PeakVoices, free >= 0 ? busyCount + 1 : busyCount);
        }

        // Most sound effects that played at the same time since the last reset (for testing).
        public int PeakVoices { get; set; }

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

        // Number of sound effects playing right now (for testing / the debug stats).
        public int ActiveVoices
        {
            get
            {
                var n = 0;
                foreach (var s in sources) if (s.isPlaying) n++;
                return n;
            }
        }

        public void Dispose()
        {
            subscription.Dispose();
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
