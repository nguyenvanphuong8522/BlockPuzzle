using UnityEngine;

namespace Survivor
{
    [CreateAssetMenu(menuName = "Survivor/Companion/Rái Cá Kiếm Sĩ", fileName = "SwordOtter")]
    public sealed class SwordOtterData : MeleeCompanionData
    {
        public override CompanionKind Kind => CompanionKind.SwordOtter;
        public override Companion Create() => new SwordOtterCompanion(this);
    }
}
