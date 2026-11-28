using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    // Enemy type by name, same order as GameConfig.Enemies (the number is the index there).
    public enum TimelineEnemy
    {
        [InspectorName("Tự chọn (loại mới nhất đã mở)")] Auto = -1,
        [InspectorName("Cua Con")] CuaCon = 0,
        [InspectorName("Cá Bay")] CaBay = 1,
        [InspectorName("Sứa Độc")] SuaDoc = 2,
        [InspectorName("Sao Biển Gai")] SaoBienGai = 3,
        [InspectorName("Bạch Tuộc")] BachTuoc = 4,
        [InspectorName("Ốc Mượn Hồn")] OcMuonHon = 5,
        [InspectorName("Mực Phun Mực (bắn xa)")] MucPhunMuc = 7,
    }

    public enum TimelinePattern
    {
        [InspectorName("Rải rác quanh màn hình")] Scatter,
        [InspectorName("Vòng vây (vòng tròn khép lại)")] Encircle,
        [InspectorName("Bức tường (hàng ngang)")] Wall,
        [InspectorName("Cụm đông một hướng")] Cluster,
    }

    public enum TimelineSide
    {
        [InspectorName("Ngẫu nhiên")] Random,
        [InspectorName("Trên")] Top,
        [InspectorName("Dưới")] Bottom,
        [InspectorName("Trái")] Left,
        [InspectorName("Phải")] Right,
        [InspectorName("Trái hoặc phải")] LeftOrRight,
        [InspectorName("Trên hoặc dưới")] TopOrBottom,
    }

    // One scripted moment: "at second 90, a wall of octopus from the left".
    [Serializable]
    public sealed class TimelineEvent
    {
        [Tooltip("Name shown in the list (only for you).")]
        public string Note = "Sự kiện";
        [Tooltip("Game time (seconds) it happens. The clock stops during boss fights, so these are 'play time' seconds.")]
        [Min(0f)] public float Time = 30f;
        public TimelinePattern Pattern = TimelinePattern.Cluster;
        public TimelineEnemy Enemy = TimelineEnemy.CuaCon;
        [Tooltip("How many enemies (for a wall: per row).")]
        [Min(1)] public int Count = 20;
        [Tooltip("Wall: how many rows deep.")]
        [Min(1)] public int Rows = 1;
        [Tooltip("Where they come from (wall / cluster). Ignored by Encircle and Scatter.")]
        public TimelineSide Side = TimelineSide.Random;
        [Tooltip("Distance from the hero where they appear. 0 = just outside the screen.")]
        [Min(0f)] public float Distance = 0f;
        [Tooltip("Gap between two enemies (wall) / how tight the group is (cluster).")]
        [Min(0.2f)] public float Spacing = 0.8f;
        [Tooltip("Wall / cluster: march straight across the screen (like the flying swarm) instead of chasing the hero.")]
        public bool March;
        [Tooltip("Marching speed (world units / second).")]
        [Min(0.2f)] public float MarchSpeed = 2.2f;
        [Tooltip("Health multiplier on top of the normal growth over time.")]
        [Min(0.05f)] public float HpMultiplier = 1f;
        [Tooltip("Spawn them as elites (big, tough, drop a chest). Not with March.")]
        public bool Elite;
        [Tooltip("Spread the spawning over this many seconds (0 = all at once).")]
        [Min(0f)] public float SpawnOverSeconds = 0f;
        [Tooltip("Repeat every N seconds after the first time (0 = once).")]
        [Min(0f)] public float RepeatEvery = 0f;
        [Tooltip("How many extra times it repeats (0 = until the run ends, when Repeat Every > 0).")]
        [Min(0)] public int RepeatTimes = 0;
        [Tooltip("Warning before it starts (seconds): the text below + an arrow on the side it comes from.")]
        [Min(0f)] public float WarningTime = 2f;
        [Tooltip("Show the warning on screen (the text below + the arrow on the screen edge). Off = no warning UI for this event.")]
        public bool ShowWarningUI = true;
        [Tooltip("Play the warning sound.")]
        public bool WarningSound = true;
        [Tooltip("Text shown at the warning: a key from Localization.csv (announce.*) or plain text. Empty = no text.")]
        public string Announce = "";
        public Color AnnounceColor = new Color(1f, 0.55f, 0.3f);
    }

    // The scripted enemy timeline: Assets/Resources/SpawnTimeline.asset. Runs on top of the normal trickle of enemies.
    [CreateAssetMenu(menuName = "Survivor/Spawn Timeline", fileName = "SpawnTimeline")]
    public sealed class SpawnTimeline : ScriptableObject
    {
        [Tooltip("Turn the whole timeline on / off.")]
        public bool Enabled = true;
        [Tooltip("Turn off the automatic horde (circle of enemies every minute) - the timeline takes over.")]
        public bool ReplaceAutoHordes = true;
        [Tooltip("Master switch for the warning UI (text + edge arrow) of every event. Off = no event shows a warning, whatever its own setting.")]
        public bool ShowWarnings = true;

        public List<TimelineEvent> Events = new List<TimelineEvent>
        {
            new TimelineEvent
            {
                Note = "0:30 đàn cua", Time = 30f, Pattern = TimelinePattern.Cluster, Enemy = TimelineEnemy.CuaCon,
                Count = 28, Spacing = 0.7f, Announce = "announce.crab_swarm"
            },
            new TimelineEvent
            {
                Note = "1:00 vòng vây sứa", Time = 60f, Pattern = TimelinePattern.Encircle, Enemy = TimelineEnemy.SuaDoc,
                Count = 36, Announce = "announce.jelly_ring", AnnounceColor = new Color(0.8f, 0.5f, 1f)
            },
            new TimelineEvent
            {
                Note = "1:30 tường bạch tuộc", Time = 90f, Pattern = TimelinePattern.Wall, Enemy = TimelineEnemy.BachTuoc,
                Count = 14, Rows = 2, Spacing = 1f, Side = TimelineSide.LeftOrRight, March = true, MarchSpeed = 1.8f,
                Announce = "announce.octopus_wall", AnnounceColor = new Color(1f, 0.4f, 0.6f)
            },
            new TimelineEvent
            {
                Note = "2:00 tường sao biển", Time = 120f, Pattern = TimelinePattern.Wall, Enemy = TimelineEnemy.SaoBienGai,
                Count = 16, Spacing = 0.8f, Side = TimelineSide.TopOrBottom, Announce = "announce.starfish"
            },
            new TimelineEvent
            {
                Note = "2:30 vòng vây cá bay", Time = 150f, Pattern = TimelinePattern.Encircle, Enemy = TimelineEnemy.CaBay,
                Count = 48, Announce = "announce.surrounded"
            },
            new TimelineEvent
            {
                Note = "3:30 ốc tinh anh", Time = 210f, Pattern = TimelinePattern.Cluster, Enemy = TimelineEnemy.OcMuonHon,
                Count = 2, Spacing = 1.6f, Elite = true, Announce = "announce.giant_hermit"
            },
            new TimelineEvent
            {
                Note = "4:00 mực bao quanh", Time = 240f, Pattern = TimelinePattern.Scatter, Enemy = TimelineEnemy.MucPhunMuc,
                Count = 10, Announce = "announce.squid"
            },
            new TimelineEvent
            {
                Note = "từ 4:30 vòng vây mỗi 60s", Time = 270f, Pattern = TimelinePattern.Encircle, Enemy = TimelineEnemy.Auto,
                Count = 50, RepeatEvery = 60f, Announce = "announce.surrounded"
            },
        };

        public static SpawnTimeline LoadOrDefault()
        {
            var timeline = Resources.Load<SpawnTimeline>("SpawnTimeline");
            return timeline != null ? timeline : CreateInstance<SpawnTimeline>();
        }
    }
}
