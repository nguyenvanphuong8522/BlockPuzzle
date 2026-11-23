using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Survivor
{
    public enum PopupShowAction
    {
        DoNothing,      // open on top, leave the current popup as it is
        DismissCurrent, // close the current popup first
        PauseCurrent    // hide the current popup; it comes back when this one closes
    }

    // Opens popups from Addressable prefabs (see PopupCatalog), keeps them in a stack, and handles
    // preloading, reuse and releasing. Popups get their dependencies injected by VContainer.
    //   popups.Show<PausePopup>(data);   popups.Dismiss<PausePopup>();   popups.Get<PausePopup>()
    public sealed class PopupController : IDisposable
    {
        const int BaseSortingOrder = 200;
        const int SortingStep = 10;

        readonly PopupCatalog catalog;
        readonly IObjectResolver resolver;
        readonly Dictionary<Type, BasePopup> instances = new Dictionary<Type, BasePopup>();
        readonly Dictionary<Type, AsyncOperationHandle<GameObject>> handles = new Dictionary<Type, AsyncOperationHandle<GameObject>>();
        readonly Dictionary<Type, UniTask<BasePopup>> loading = new Dictionary<Type, UniTask<BasePopup>>();
        readonly Dictionary<Type, BasePopup> paused = new Dictionary<Type, BasePopup>(); // popup -> the one it paused
        readonly List<BasePopup> stack = new List<BasePopup>();
        Transform root;

        public PopupController(PopupCatalog catalog, IObjectResolver resolver)
        {
            this.catalog = catalog;
            this.resolver = resolver;
        }

        public BasePopup Current => stack.Count > 0 ? stack[stack.Count - 1] : null;

        // Loaded instance of T (null if never loaded). Safe to call every frame.
        public T Get<T>() where T : BasePopup => instances.TryGetValue(typeof(T), out var p) ? (T)p : null;

        public bool IsOpen<T>() where T : BasePopup => Get<T>() is { IsShown: true };

        // Loads every popup marked Preload, so their first Show has no delay.
        public async UniTask PreloadAsync()
        {
            foreach (var entry in catalog.Entries)
            {
                if (entry == null || !entry.Preload) continue;
                var type = FindType(entry.TypeName);
                if (type != null) await GetOrLoad(type);
            }
        }

        public void Show<T>(object data = null, PopupShowAction action = PopupShowAction.DoNothing, bool animated = true)
            where T : BasePopup => ShowAsync<T>(data, action, animated).Forget();

        public async UniTask<T> ShowAsync<T>(object data = null, PopupShowAction action = PopupShowAction.DoNothing,
            bool animated = true) where T : BasePopup
        {
            var popup = await GetOrLoad(typeof(T));
            if (popup == null) return null;

            var current = Current;
            if (current != null && current != popup)
            {
                if (action == PopupShowAction.DismissCurrent) Dismiss(current, animated);
                else if (action == PopupShowAction.PauseCurrent)
                {
                    current.Pause(animated);
                    paused[typeof(T)] = current;
                }
            }

            stack.Remove(popup);
            stack.Add(popup);
            popup.SetOrder(BaseSortingOrder + stack.Count * SortingStep);
            popup.Show(data, animated);
            return (T)popup;
        }

        public void Dismiss<T>(bool animated = true) where T : BasePopup
        {
            var popup = Get<T>();
            if (popup != null) Dismiss(popup, animated);
        }

        public void Dismiss(BasePopup popup, bool animated = true)
        {
            if (!stack.Remove(popup)) return;
            var type = popup.GetType();
            popup.Hide(animated, () => AfterHidden(popup));

            if (paused.TryGetValue(type, out var under))
            {
                paused.Remove(type);
                if (stack.Contains(under)) under.Resume(animated);
            }
        }

        public void DismissAll(bool animated = false)
        {
            for (var i = stack.Count - 1; i >= 0; i--) Dismiss(stack[i], animated);
        }

        void AfterHidden(BasePopup popup)
        {
            if (popup == null || popup.IsShown) return; // reopened during the hide animation
            var entry = catalog.Find(popup.GetType());
            if (entry == null || entry.KeepLoaded) return;

            var type = popup.GetType();
            instances.Remove(type);
            Object.Destroy(popup.gameObject);
            if (!entry.KeepAsset && handles.TryGetValue(type, out var handle))
            {
                Addressables.Release(handle);
                handles.Remove(type);
            }
        }

        UniTask<BasePopup> GetOrLoad(Type type)
        {
            if (instances.TryGetValue(type, out var existing)) return UniTask.FromResult(existing);
            if (loading.TryGetValue(type, out var pending)) return pending;
            var task = Load(type).Preserve(); // several callers may await the same load
            if (!task.Status.IsCompleted()) loading[type] = task; // Load removes it again when done
            return task;
        }

        async UniTask<BasePopup> Load(Type type)
        {
            try
            {
                var entry = catalog.Find(type);
                if (entry == null || entry.Asset == null || !entry.Asset.RuntimeKeyIsValid())
                {
                    Debug.LogError($"PopupController: no prefab for {type.Name} in PopupCatalog. Run Survivor > Build UI Prefabs.");
                    return null;
                }

                if (!handles.TryGetValue(type, out var handle))
                {
                    handle = Addressables.LoadAssetAsync<GameObject>(entry.Asset);
                    handles[type] = handle;
                }
                var prefab = await handle.ToUniTask();

                if (root == null) root = new GameObject("Popups").transform;
                var go = Object.Instantiate(prefab, root);
                resolver.InjectGameObject(go); // [Inject] methods on the popup and its children
                var popup = go.GetComponent<BasePopup>();
                popup.Setup(this);
                instances[type] = popup;
                return popup;
            }
            finally
            {
                loading.Remove(type);
            }
        }

        static Type FindType(string name)
        {
            var type = typeof(PopupController).Assembly.GetType("Survivor." + name);
            if (type == null) Debug.LogError($"PopupController: unknown popup type {name}");
            return type;
        }

        public void Dispose()
        {
            foreach (var handle in handles.Values)
                if (handle.IsValid()) Addressables.Release(handle);
            handles.Clear();
            instances.Clear();
            stack.Clear();
        }
    }
}
