using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Survivor
{
    [Serializable]
    public sealed class PopupEntry
    {
        [Tooltip("Popup class name, e.g. LevelUpPopup")]
        public string TypeName;
        public AssetReferenceGameObject Asset;
        [Tooltip("Load when the game starts, so the first Show is instant.")]
        public bool Preload = true;
        [Tooltip("Keep the instance after it closes (fast to reopen). Off = destroy it.")]
        public bool KeepLoaded = true;
        [Tooltip("Keep the Addressable asset loaded after the instance is destroyed. Off = release it.")]
        public bool KeepAsset = true;
    }

    // Which prefab (Addressable) each popup type uses. Assets/Resources/PopupCatalog.asset;
    // "Survivor/Build UI Prefabs" fills it.
    [CreateAssetMenu(menuName = "Survivor/Popup Catalog", fileName = "PopupCatalog")]
    public sealed class PopupCatalog : ScriptableObject
    {
        public PopupEntry[] Entries = new PopupEntry[0];

        public PopupEntry Find(Type type)
        {
            foreach (var e in Entries)
                if (e != null && e.TypeName == type.Name) return e;
            return null;
        }

        public static PopupCatalog LoadOrEmpty()
        {
            var catalog = Resources.Load<PopupCatalog>("PopupCatalog");
            return catalog != null ? catalog : CreateInstance<PopupCatalog>();
        }
    }
}
