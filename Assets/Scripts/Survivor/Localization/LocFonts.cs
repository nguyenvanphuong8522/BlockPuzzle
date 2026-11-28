using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Survivor
{
    // Fonts / materials of the current language's font rules (LocalizationSettings > Language > Fonts).
    // They are Addressables: loaded when a language is chosen, released once the next language's fonts are ready
    // (so a text never loses its font while switching). Until they arrive, texts keep the font they have.
    static class LocFonts
    {
        static readonly Dictionary<string, Object> loaded = new Dictionary<string, Object>(); // asset GUID -> asset
        static List<AsyncOperationHandle> active = new List<AsyncOperationHandle>();
        static int generation;

        static List<AsyncOperationHandle> stale = new List<AsyncOperationHandle>();

        // Starts loading `language`'s fonts and calls `ready` once they are all in (texts then switch; the previous
        // language's fonts are released right after). Returns false when it has none: the caller refreshes the texts
        // itself and then calls ReleaseStale.
        public static bool Load(LocalizationSettings.Language language, Action ready)
        {
            generation++;
            var gen = generation;
            stale.AddRange(active);
            active = new List<AsyncOperationHandle>();

            var refs = new List<AssetReference>();
            if (language != null)
                foreach (var rule in language.Fonts)
                {
                    if (rule == null) continue;
                    if (rule.ToFont != null && rule.ToFont.RuntimeKeyIsValid()) refs.Add(rule.ToFont);
                    if (rule.ToMaterial != null && rule.ToMaterial.RuntimeKeyIsValid()) refs.Add(rule.ToMaterial);
                }

            if (refs.Count == 0) return false;

            var pending = refs.Count;
            foreach (var r in refs)
            {
                var guid = r.AssetGUID;
                var handle = Addressables.LoadAssetAsync<Object>(r.RuntimeKey);
                active.Add(handle);
                handle.Completed += h =>
                {
                    if (h.Status == AsyncOperationStatus.Succeeded) loaded[guid] = h.Result;
                    else Debug.LogWarning($"Loc: could not load font asset {guid} for {language.Code} (is it Addressable?).");
                    if (--pending > 0 || gen != generation) return;
                    ready?.Invoke(); // texts switch to the new fonts
                    ReleaseStale();  // the old ones are no longer used
                };
            }
            return true;
        }

        public static void ReleaseStale() => Release(stale);

        // The loaded asset of a reference; in the editor outside Play the asset itself (preview).
        public static T Get<T>(AssetReference reference) where T : Object
        {
            if (reference == null || !reference.RuntimeKeyIsValid()) return null;
#if UNITY_EDITOR
            if (!Application.isPlaying) return reference.editorAsset as T;
#endif
            return loaded.TryGetValue(reference.AssetGUID, out var asset) ? asset as T : null;
        }

        public static void ReleaseAll()
        {
            Release(active);
            Release(stale);
            active = new List<AsyncOperationHandle>();
            loaded.Clear();
            generation++;
        }

        static void Release(List<AsyncOperationHandle> handles)
        {
            foreach (var h in handles)
            {
                if (!h.IsValid()) continue;
                if (h.IsDone && h.Status == AsyncOperationStatus.Succeeded && h.Result is Object asset)
                    foreach (var pair in new List<KeyValuePair<string, Object>>(loaded))
                        if (pair.Value == asset && !IsStillLoaded(asset)) loaded.Remove(pair.Key);
                Addressables.Release(h);
            }
            handles.Clear();
        }

        static bool IsStillLoaded(Object asset)
        {
            foreach (var h in active)
                if (h.IsValid() && h.IsDone && ReferenceEquals(h.Result, asset)) return true;
            return false;
        }

        // Applies the first matching rule of the current language to a text (see LocalizationSettings.FontRule).
        public static void Apply(TMP_Text text, LocalizationSettings.Language language, TMP_FontAsset original,
            Material originalMaterial)
        {
            if (text == null || original == null) return;
            LocalizationSettings.FontRule rule = null;
            if (language != null)
                foreach (var r in language.Fonts)
                    if (r != null && r.Matches(original, originalMaterial))
                    {
                        rule = r;
                        break;
                    }

            TMP_FontAsset font = original;
            Material material = null;
            if (rule != null)
            {
                var wantsFont = rule.ToFont != null && rule.ToFont.RuntimeKeyIsValid();
                var wantsMaterial = rule.ToMaterial != null && rule.ToMaterial.RuntimeKeyIsValid();
                if (wantsFont)
                {
                    font = Get<TMP_FontAsset>(rule.ToFont);
                    if (font == null) return; // still loading: keep the current font for now
                }
                if (wantsMaterial)
                {
                    material = Get<Material>(rule.ToMaterial);
                    if (material == null) return;
                }
                if (material == null) material = wantsFont ? font.material : originalMaterial;
            }
            else material = originalMaterial;
            if (material == null) material = font.material;

            if (text.font != font) text.font = font;
            if (material != null && text.fontSharedMaterial != material) text.fontSharedMaterial = material;
        }
    }

}
