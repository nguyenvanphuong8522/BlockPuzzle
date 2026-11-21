// Placeholder sound generator: synthesizes retro sound effects and a looping beach tune to WAV.
// C# 5 (Windows csc.exe). Replace any WAV later with a real recording using the same file name.
using System;
using System.IO;
using System.Text;

static class SfxGen
{
    const int Rate = 44100;
    const int Sine = 0, Square = 1, Saw = 2, Triangle = 3, Noise = 4;

    static readonly Random Rnd = new Random(1234);
    static string dir;

    public static void Run(string outputDir)
    {
        dir = outputDir;

        // Combat
        // Flintlock "bang": sharp powder crack + low thump + short noisy tail.
        Save("sfx_shoot", Mix(
            Lowpass(Tone(0.05f, 0, 0, Noise, 0.0005f, 3f, 0.9f), 0.7f),
            Tone(0.16f, 160, 45, Sine, 0.001f, 3f, 0.8f),
            Lowpass(Tone(0.22f, 0, 0, Noise, 0.002f, 4f, 0.35f), 0.12f)));
        Save("sfx_hit", Lowpass(Tone(0.06f, 0, 0, Noise, 0.001f, 5f, 0.5f), 0.35f));
        Save("sfx_enemy_die", Mix(Tone(0.14f, 620, 140, Sine, 0.001f, 3f, 0.5f), Lowpass(Tone(0.1f, 0, 0, Noise, 0.001f, 4f, 0.3f), 0.2f)));
        Save("sfx_boss_die", Mix(Tone(1.2f, 160, 40, Saw, 0.005f, 2f, 0.4f), Lowpass(Tone(1.0f, 0, 0, Noise, 0.002f, 2f, 0.5f), 0.08f)));
        Save("sfx_hurt", Mix(Tone(0.18f, 240, 110, Square, 0.002f, 2.5f, 0.35f), Lowpass(Tone(0.1f, 0, 0, Noise, 0.001f, 4f, 0.25f), 0.3f)));
        Save("sfx_lightning", Mix(Lowpass(Tone(0.35f, 0, 0, Noise, 0.001f, 3.5f, 0.7f), 0.6f), Tone(0.4f, 90, 50, Saw, 0.01f, 2f, 0.3f)));
        Save("sfx_wave", Swoosh(0.45f));

        // Pickups and progression
        // Bright two-note "ting" (E6 then B6) with a bell-like overtone. The game raises its pitch on pickup streaks.
        Save("sfx_gem", Mix(
            Tone(0.22f, Midi(88), Midi(88), Sine, 0.001f, 2.5f, 0.45f),
            Tone(0.22f, Midi(100), Midi(100), Sine, 0.001f, 3.5f, 0.12f),
            Delay(Tone(0.25f, Midi(95), Midi(95), Sine, 0.001f, 2.5f, 0.4f), 0.045f),
            Delay(Tone(0.25f, Midi(107), Midi(107), Sine, 0.001f, 3.5f, 0.1f), 0.045f)));
        Save("sfx_heal", Tone(0.35f, 400, 900, Triangle, 0.01f, 1.5f, 0.45f));
        Save("sfx_magnet", Vibrato(Tone(0.5f, 500, 1100, Square, 0.01f, 1.5f, 0.25f), 18f));
        Save("sfx_level_up", Arpeggio(new[] { 72, 76, 79, 84 }, 0.07f, Square, 0.3f, 0.25f));
        Save("sfx_chest", Mix(Arpeggio(new[] { 79, 84, 88, 91, 96 }, 0.06f, Triangle, 0.5f, 0.35f), Sparkle(0.9f)));
        Save("sfx_click", Tone(0.04f, 1000, 800, Square, 0.001f, 6f, 0.2f));
        Save("sfx_boss_spawn", Vibrato(Mix(Tone(1.4f, 110, 98, Saw, 0.05f, 1.2f, 0.35f), Tone(1.4f, 55, 49, Square, 0.05f, 1.2f, 0.25f)), 5f));
        Save("sfx_victory", Melody(new[] { 72, 76, 79, 84, 79, 84 }, new[] { 1, 1, 1, 2, 1, 4 }, 0.12f, Square, 0.3f));
        Save("sfx_game_over", Melody(new[] { 67, 66, 65, 64, 60 }, new[] { 2, 2, 2, 2, 6 }, 0.14f, Triangle, 0.4f));

        Save("music_beach", BeachTune());
    }

    // ---------- Building blocks ----------

    static float Osc(int wave, double phase)
    {
        var p = phase - Math.Floor(phase);
        switch (wave)
        {
            case Sine: return (float)Math.Sin(p * Math.PI * 2);
            case Square: return p < 0.5 ? 0.8f : -0.8f;
            case Saw: return (float)(p * 2 - 1) * 0.8f;
            case Triangle: return (float)(p < 0.5 ? p * 4 - 1 : 3 - p * 4);
            default: return (float)(Rnd.NextDouble() * 2 - 1);
        }
    }

    // Tone with exponential pitch sweep f0 -> f1, short attack and a decay curve (higher = faster fade).
    static float[] Tone(float duration, float f0, float f1, int wave, float attack, float decay, float volume)
    {
        var n = (int)(duration * Rate);
        var s = new float[n];
        double phase = 0;
        for (var i = 0; i < n; i++)
        {
            var t = i / (float)n;
            var f = f0 <= 0 ? 0 : f0 * Math.Pow(f1 / f0, t);
            phase += f / Rate;
            var env = Math.Min(1f, i / (attack * Rate)) * (float)Math.Pow(1 - t, decay);
            s[i] = Osc(wave, phase) * env * volume;
        }
        return s;
    }

    static float Midi(int note)
    {
        return 440f * (float)Math.Pow(2, (note - 69) / 12.0);
    }

    static float[] Mix(params float[][] parts)
    {
        var n = 0;
        foreach (var p in parts) n = Math.Max(n, p.Length);
        var s = new float[n];
        foreach (var p in parts)
            for (var i = 0; i < p.Length; i++) s[i] += p[i];
        return s;
    }

    static void AddAt(float[] target, float[] source, int offset, float gain)
    {
        for (var i = 0; i < source.Length && offset + i < target.Length; i++) target[offset + i] += source[i] * gain;
    }

    // Shifts a sound later by seconds (adds silence in front).
    static float[] Delay(float[] s, float seconds)
    {
        var offset = (int)(seconds * Rate);
        var d = new float[s.Length + offset];
        Array.Copy(s, 0, d, offset, s.Length);
        return d;
    }

    static float[] Lowpass(float[] s, float k)
    {
        float y = 0;
        for (var i = 0; i < s.Length; i++)
        {
            y += k * (s[i] - y);
            s[i] = y;
        }
        return s;
    }

    static float[] Vibrato(float[] s, float hz)
    {
        for (var i = 0; i < s.Length; i++) s[i] *= 0.75f + 0.25f * (float)Math.Sin(i * hz * Math.PI * 2 / Rate);
        return s;
    }

    static float[] Swoosh(float duration)
    {
        var n = (int)(duration * Rate);
        var s = new float[n];
        float y = 0;
        for (var i = 0; i < n; i++)
        {
            var t = i / (float)n;
            var k = 0.02f + 0.25f * (float)Math.Sin(t * Math.PI); // filter opens then closes: "whoosh"
            y += k * ((float)(Rnd.NextDouble() * 2 - 1) - y);
            s[i] = y * (float)Math.Sin(t * Math.PI) * 0.9f;
        }
        return s;
    }

    static float[] Sparkle(float duration)
    {
        var s = new float[(int)(duration * Rate)];
        for (var k = 0; k < 14; k++)
        {
            var at = (int)(Rnd.NextDouble() * (s.Length - Rate * 0.1));
            var f = 2000 + (float)Rnd.NextDouble() * 2500;
            AddAt(s, Tone(0.08f, f, f * 1.2f, Sine, 0.001f, 3f, 0.08f), at, 1f);
        }
        return s;
    }

    static float[] Arpeggio(int[] notes, float step, int wave, float tail, float volume)
    {
        var s = new float[(int)((notes.Length * step + tail) * Rate)];
        for (var i = 0; i < notes.Length; i++)
        {
            var f = Midi(notes[i]);
            AddAt(s, Tone(step + tail, f, f, wave, 0.003f, 2.5f, volume), (int)(i * step * Rate), 1f);
        }
        return s;
    }

    static float[] Melody(int[] notes, int[] lengths, float unit, int wave, float volume)
    {
        var total = 0;
        foreach (var l in lengths) total += l;
        var s = new float[(int)((total * unit + 0.3f) * Rate)];
        var at = 0;
        for (var i = 0; i < notes.Length; i++)
        {
            var f = Midi(notes[i]);
            AddAt(s, Tone(lengths[i] * unit + 0.1f, f, f, wave, 0.005f, 1.2f, volume), (int)(at * unit * Rate), 1f);
            at += lengths[i];
        }
        return s;
    }

    // ---------- Background music: 8 bars at 120 BPM, loops seamlessly ----------
    static float[] BeachTune()
    {
        const float beat = 0.5f;           // 120 BPM
        const float eighth = beat / 2f;
        const int bars = 8;
        var s = new float[(int)(bars * 4 * beat * Rate)];

        int[][] chords = { new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 57, 60, 65 }, new[] { 55, 59, 62 } };
        int[] roots = { 48, 45, 41, 43 };

        // Melody in C major pentatonic: (note, length in eighths); 8 eighths per bar.
        int[] mNotes = { 76, 79, 81, 79, 76, 84, 81, 79, 76, 81, 84, 81, 79, 79, 81, 79, 76, 74,
                         76, 79, 84, 81, 79, 81, 79, 76, 72, 74, 76, 79, 81, 79, 74, 76, 72 };
        int[] mLens = { 2, 2, 1, 1, 2, 2, 2, 2, 2, 2, 1, 1, 4, 1, 1, 1, 1, 4,
                        2, 2, 2, 2, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 4 };

        for (var bar = 0; bar < bars; bar++)
        {
            var chord = chords[bar % 4];
            var root = roots[bar % 4];
            var barStart = bar * 4 * beat;

            // Bass: root on 1 and 3, octave on the "and" of 2
            AddAt(s, Tone(beat * 0.9f, Midi(root), Midi(root), Triangle, 0.005f, 1.5f, 0.45f), (int)(barStart * Rate), 1f);
            AddAt(s, Tone(eighth * 0.9f, Midi(root + 12), Midi(root + 12), Triangle, 0.005f, 1.5f, 0.3f), (int)((barStart + 1.5f * beat) * Rate), 1f);
            AddAt(s, Tone(beat * 0.9f, Midi(root + 7), Midi(root + 7), Triangle, 0.005f, 1.5f, 0.4f), (int)((barStart + 2 * beat) * Rate), 1f);

            // Plucked "ukulele" strums
            float[] strums = { 0f, 1.5f, 2f, 3.5f };
            foreach (var st in strums)
                for (var k = 0; k < chord.Length; k++)
                {
                    var f = Midi(chord[k] + 12);
                    AddAt(s, Tone(0.35f, f, f, Triangle, 0.002f, 4f, 0.09f), (int)((barStart + st * beat + k * 0.012f) * Rate), 1f);
                }

            // Drums: kick on 1 and 3, snare on 2 and 4, soft hats on eighths
            for (var b = 0; b < 4; b++)
            {
                var t = (int)((barStart + b * beat) * Rate);
                if (b % 2 == 0) AddAt(s, Tone(0.18f, 140, 45, Sine, 0.001f, 2.5f, 0.6f), t, 1f);
                else AddAt(s, Lowpass(Tone(0.12f, 0, 0, Noise, 0.001f, 3f, 0.35f), 0.4f), t, 1f);
            }
            for (var e = 0; e < 8; e++)
                AddAt(s, Tone(0.03f, 0, 0, Noise, 0.001f, 6f, 0.07f), (int)((barStart + e * eighth) * Rate), 1f);
        }

        var pos = 0;
        for (var i = 0; i < mNotes.Length; i++)
        {
            var f = Midi(mNotes[i]);
            var len = mLens[i] * eighth;
            var note = Vibrato(Tone(len * 0.95f, f, f, Square, 0.01f, 0.6f, 0.13f), 5.5f);
            AddAt(s, note, (int)(pos * eighth * Rate), 1f);
            pos += mLens[i];
        }
        return s;
    }

    // ---------- Output ----------

    static void Save(string name, float[] samples)
    {
        // Normalize to a safe peak so every clip has a similar loudness.
        float peak = 0.0001f;
        foreach (var v in samples) peak = Math.Max(peak, Math.Abs(v));
        var gain = 0.85f / peak;

        // 3 ms fade-out avoids a click at the end.
        var fade = Math.Min(samples.Length, Rate * 3 / 1000);
        var path = Path.Combine(dir, name + ".wav");
        Directory.CreateDirectory(dir);
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + samples.Length * 2);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(samples.Length * 2);
            for (var i = 0; i < samples.Length; i++)
            {
                var v = samples[i] * gain;
                if (name.StartsWith("music") == false && i > samples.Length - fade) v *= (samples.Length - i) / (float)fade;
                w.Write((short)(Math.Max(-1f, Math.Min(1f, v)) * 32767));
            }
        }
    }
}

static class Program
{
    static void Main(string[] args)
    {
        var root = args.Length > 0 ? args[0] : ".";
        ArtGen.Run(Path.Combine(root, "Assets", "Art"));
        SfxGen.Run(Path.Combine(root, "Assets", "Audio"));
        Console.WriteLine("Done.");
    }
}
