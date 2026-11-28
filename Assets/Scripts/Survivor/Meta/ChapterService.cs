using UnityEngine;

namespace Survivor
{
    // Chapters of the Home screen (HomeSettings.Chapters): which one is picked (the one played), which are open,
    // best times. Progress is saved in PlayerPrefs ("chapter.<asset>.cleared" / ".best").
    public sealed class ChapterService
    {
        const string SelectedKey = "chapter.selected";

        readonly HomeSettings settings;
        readonly ChapterData fallback;

        public ChapterService(HomeSettings settings)
        {
            this.settings = settings;
            fallback = ScriptableObject.CreateInstance<ChapterData>(); // no chapter assets yet: a plain normal run
            fallback.name = "Chapter1";
            SelectedIndex = Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, 0), 0, Mathf.Max(0, Count - 1));
            if (!IsUnlocked(SelectedIndex)) SelectedIndex = 0;
        }

        public int Count => settings.Chapters != null ? settings.Chapters.Length : 0;

        public ChapterData Get(int index) =>
            index >= 0 && index < Count && settings.Chapters[index] != null ? settings.Chapters[index] : fallback;

        // The picked chapter: shown on the Home card and played when Play is pressed.
        public int SelectedIndex { get; private set; }
        public ChapterData Current => Get(SelectedIndex);

        public void Select(int index)
        {
            SelectedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, Count - 1));
            PlayerPrefs.SetInt(SelectedKey, SelectedIndex);
        }

        public int CostOf(int index)
        {
            var c = Get(index).EnergyCost;
            return c > 0 ? c : settings.PlayCost;
        }

        public bool IsUnlocked(int index) => index <= 0 || IsCleared(index - 1);
        public bool IsCleared(int index) => PlayerPrefs.GetInt(Key(index, "cleared"), 0) == 1;
        public float BestSeconds(int index) => PlayerPrefs.GetFloat(Key(index, "best"), 0f);

        // End of a run in the picked chapter: longest survival time, and "cleared" when survived.
        public void RecordResult(bool survived, float seconds)
        {
            var i = SelectedIndex;
            if (seconds > BestSeconds(i)) PlayerPrefs.SetFloat(Key(i, "best"), seconds);
            if (survived) PlayerPrefs.SetInt(Key(i, "cleared"), 1);
            PlayerPrefs.Save();
        }

        // Debug: open every chapter.
        public void UnlockAll()
        {
            for (var i = 0; i < Count; i++) PlayerPrefs.SetInt(Key(i, "cleared"), 1);
            PlayerPrefs.Save();
        }

        string Key(int index, string what) => $"chapter.{Get(index).name}.{what}";
    }

    // Survives a scene reload: "Restart" reloads the scene and goes straight into a run instead of the Home screen.
    public static class GameSession
    {
        public static bool AutoStart;
    }
}
