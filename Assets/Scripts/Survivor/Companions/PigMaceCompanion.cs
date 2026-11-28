using UnityEngine;

namespace Survivor
{
    // Art: companion_pig_0..8 (0-3 idle, 4 dash, 5 smash, 6 blink, 7-8 buff).
    // Companion: a pig with a spiked club. Fights like the sword otter (dash to the nearest enemy, smash a
    // 120-degree arc, run back; logic in MeleeDashCompanion), hits harder but slower.
    // Extra skill: every Pig Buff Interval seconds (18 by default) it gives the hero a random permanent bonus:
    // +ATK (damage multiplier) or +Max HP (also heals that much). A bar under its feet shows the countdown.
    // Every number and the card text: Assets/Resources/Upgrades/Companions/PigMace.asset.
    public sealed class PigMaceCompanion : MeleeDashCompanion
    {
        readonly PigMaceData data;
        float buffTimer;

        public PigMaceCompanion(PigMaceData data) : base(data) => this.data = data;

        protected override SpriteAnim Anim(CompanionContext ctx) => ctx.Sprites.Pig;
        protected override float BodyRadius => 0.42f;
        protected override Color FallbackColor => new Color(0.96f, 0.64f, 0.71f);
        protected override void PlaySwingSound(CompanionContext ctx) => ctx.Audio.PlayAtPitch(Sfx.Slash, 0.72f, 0.8f, 0.05f);

        // Buff skill: when the countdown is full the pig waits until it is back next to the hero, then "casts"
        // (club raised, glowing) for Pig Buff Cast Time. While waiting/casting it does NOT dash or smash.
        bool casting;
        float castTime;

        public override void Tick(float dt, CompanionContext ctx)
        {
            var t = data;
            var interval = Mathf.Max(0.5f, t.BuffInterval);

            if (casting)
            {
                AdvanceSwingEffect(dt);
                castTime += dt;
                if (castTime >= t.BuffCastTime)
                {
                    casting = false;
                    buffTimer = 0f;
                    GiveBuff(ctx);
                }
                return; // no attacking while casting
            }

            buffTimer = Mathf.Min(buffTimer + dt, interval);
            if (buffTimer >= interval && StaysHome)
            {
                // Ready and standing next to the hero: start casting instead of attacking.
                casting = true;
                castTime = 0f;
                ctx.Effects.Ring(Position, 0.7f, Tint, 0.3f);
                return;
            }
            base.Tick(dt, ctx); // ready but still away (dashing / swinging / returning): finish that first
        }

        void GiveBuff(CompanionContext ctx)
        {
            var t = data;
            var hero = ctx.Player;
            if (hero.IsDead) return;

            if (Random.value * 100f < t.AtkChancePercent)
            {
                var bonus = At(t.AtkBonus);
                hero.Stats.BonusMight += bonus;
                ctx.Effects.Ring(hero.Position, 1.3f, t.AtkColor, 0.5f);
                ctx.Effects.Burst(hero.Position, 0.9f, t.AtkColor, 0.3f);
                ctx.FloatingText.Show(Format(Loc.Text(t.AtkText), (bonus * 100f).ToString("0.#")), t.AtkColor);
            }
            else
            {
                var amount = At(t.MaxHpBonus);
                hero.IncreaseMaxHp(amount); // also heals that much and plays the plus-sign effect
                ctx.Effects.Ring(hero.Position, 1.3f, t.HpColor, 0.5f);
                ctx.FloatingText.Show(Format(Loc.Text(t.HpText), amount.ToString("0.#")), t.HpColor);
            }
            ctx.Effects.Ring(Position, 0.9f, Tint, 0.35f);
            ctx.Audio.Play(Sfx.Cast, 0.6f, 0.05f, 0.05f);
        }

        // "{0}" in the text is replaced by the number; a broken format string just shows the number.
        static string Format(string pattern, string value)
        {
            if (string.IsNullOrEmpty(pattern)) return value;
            try { return string.Format(pattern, value); }
            catch (System.FormatException) { return pattern + " " + value; }
        }

        // Buff pose: frames 7-8 (club raised high, happy hop) alternating. Old art without them: the dash pose.
        const int BuffFrameA = 7, BuffFrameB = 8;
        const float BuffFps = 7f;
        protected override Sprite OverrideFrame(SpriteAnim anim)
        {
            if (!casting) return null;
            var frames = anim.Frames;
            if (frames.Length <= BuffFrameB || frames[BuffFrameB] == null) return frames[RaiseFrame];
            return frames[(int)(castTime * BuffFps) % 2 == 0 ? BuffFrameA : BuffFrameB];
        }

        public override void Draw(InstancedRenderer renderer, CompanionContext ctx)
        {
            base.Draw(renderer, ctx);
            if (casting) DrawCastGlow(renderer, ctx);
            DrawBuffBar(renderer, ctx);
        }

        // Pulsing rings around the pig while it charges the gift.
        void DrawCastGlow(InstancedRenderer renderer, CompanionContext ctx)
        {
            var k = Mathf.Clamp01(castTime / Mathf.Max(0.05f, data.BuffCastTime));
            var center = Position + new Vector2(0f, BodyRadius * 0.2f);
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 20f);
            renderer.Ring(RenderLayer.Effects, center, BodyRadius * (1.4f + 0.5f * k), 0.12f,
                new Color(1f, 0.85f, 0.35f, 0.5f + 0.4f * pulse));
            renderer.Ring(RenderLayer.Effects, center, BodyRadius * (2.4f - 1.2f * k), 0.08f,
                new Color(1f, 1f, 1f, 0.6f * k));
        }

        // Countdown bar under the feet: fills up over Pig Buff Interval, then the buff fires.
        void DrawBuffBar(InstancedRenderer renderer, CompanionContext ctx)
        {
            var t = data;
            if (t.BarWidth <= 0f) return;
            var fill = buffTimer / Mathf.Max(0.5f, t.BuffInterval);
            var center = new Vector2(Position.x, Position.y - BodyRadius + t.BarOffsetY);
            var size = new Vector2(t.BarWidth, t.BarHeight);
            var outline = t.BarOutline;
            if (outline > 0f)
                renderer.Rect(RenderLayer.Overlay, center, size + new Vector2(outline * 2f, outline * 2f), Color.black);
            // Glows a little in the last second so you know the gift is coming.
            var color = t.BarColor;
            var left = (1f - fill) * t.BuffInterval;
            if (left < 1f) color = Color.Lerp(color, Color.white, 0.35f + 0.35f * Mathf.Sin(Time.time * 18f));
            renderer.Bar(RenderLayer.Overlay, center, size, fill, t.BarBackground, color);
        }
    }
}
