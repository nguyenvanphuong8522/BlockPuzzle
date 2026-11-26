namespace Survivor
{
    // Companion #2: an otter with a sword. It dashes to the nearest enemy, slashes everything in a 120-degree arc
    // facing that enemy, then runs back to its place next to the hero (logic in MeleeDashCompanion).
    // Numbers and card text: Assets/Resources/Upgrades/Companions/SwordOtter.asset.
    public sealed class SwordOtterCompanion : MeleeDashCompanion
    {
        public SwordOtterCompanion(SwordOtterData data) : base(data) { }

        protected override SpriteAnim Anim(CompanionContext ctx) => ctx.Sprites.Otter;
    }
}
