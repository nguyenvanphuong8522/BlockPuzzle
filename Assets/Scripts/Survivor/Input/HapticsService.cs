using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Survivor
{
    // Vibration feedback on every platform:
    //   Gamepad         - Input System motor rumble (strength + duration)
    //   Android         - Vibrator service (duration; strength on Android 8+)
    //   iOS             - Handheld.Vibrate (the OS decides strength and length)
    //   WebGL on phones - navigator.vibrate (duration; Chrome on Android, not iOS Safari)
    // Settings are saved in PlayerPrefs and tuned from the debug panel.
    public sealed class HapticsService
    {
        const string EnabledKey = "haptics.enabled";
        const string StrengthKey = "haptics.strength";
        const string DurationKey = "haptics.durationMs";

        bool enabled = PlayerPrefs.GetInt(EnabledKey, 1) == 1;
        float strength = PlayerPrefs.GetFloat(StrengthKey, 0.6f);
        int durationMs = PlayerPrefs.GetInt(DurationKey, 120);
        float gamepadStopAt = -1f;

        public bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                if (!value) Stop();
            }
        }

        // 0..1
        public float Strength
        {
            get => strength;
            set
            {
                strength = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(StrengthKey, strength);
            }
        }

        public int DurationMs
        {
            get => durationMs;
            set
            {
                durationMs = Mathf.Clamp(value, 10, 1000);
                PlayerPrefs.SetInt(DurationKey, durationMs);
            }
        }

        // Player got hit by an enemy.
        public void PlayHit() => Pulse(strength, durationMs);

        public void Pulse(float intensity, int milliseconds)
        {
            if (!enabled || intensity <= 0f || milliseconds <= 0) return;

            var pad = Gamepad.current;
            if (pad != null)
            {
                // Low-frequency motor = heavy thump, high-frequency = buzz.
                pad.SetMotorSpeeds(intensity * 0.8f, intensity);
                gamepadStopAt = Time.unscaledTime + milliseconds / 1000f;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidVibrate(milliseconds, Mathf.Clamp(Mathf.RoundToInt(intensity * 255f), 1, 255));
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#elif UNITY_WEBGL && !UNITY_EDITOR
            Survivor_Vibrate(milliseconds);
#endif
        }

        // Called every frame: stops gamepad rumble when its time is up.
        public void Update()
        {
            if (gamepadStopAt >= 0f && Time.unscaledTime >= gamepadStopAt) Stop();
        }

        public void Stop()
        {
            gamepadStopAt = -1f;
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
        }

        public void Save() => PlayerPrefs.Save();

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject vibrator;
        int sdk = -1;

        void AndroidVibrate(int milliseconds, int amplitude)
        {
            if (vibrator == null)
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    sdk = version.GetStatic<int>("SDK_INT");
            }
            if (vibrator == null) return;

            if (sdk >= 26)
            {
                using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, amplitude))
                    vibrator.Call("vibrate", effect);
            }
            else
            {
                vibrator.Call("vibrate", (long)milliseconds);
            }
        }

        // Referencing Handheld.Vibrate makes Unity add the VIBRATE permission to the Android manifest.
        static void EnsureVibratePermission() => Handheld.Vibrate();
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void Survivor_Vibrate(int milliseconds);
#endif
    }
}
