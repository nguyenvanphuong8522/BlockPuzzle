using System;
using TMPro;
using UnityEngine;

namespace Survivor
{
    public sealed class ResultData
    {
        public bool Survived;
        public float Seconds;
        public int Kills;
        public int Level;
        public Action OnRestart;
    }

    // End-of-run screen.
    public sealed class ResultPopup : BasePopup
    {
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI stats;
        [SerializeField] TapButton restartButton;
        // Texts: Localization.csv (result.*).
        [SerializeField] Color survivedColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] Color defeatedColor = new Color(0.9f, 0.4f, 0.4f);

        protected override void OnShow(object data)
        {
            var d = (ResultData)data;
            title.text = Loc.Get(d.Survived ? "result.survived" : "result.defeated");
            title.color = d.Survived ? survivedColor : defeatedColor;
            var s = Mathf.FloorToInt(d.Seconds);
            stats.text = Loc.Format("result.stats", $"{s / 60:00}:{s % 60:00}", d.Kills, d.Level);
            restartButton.OnPressed = d.OnRestart;
        }
    }
}
