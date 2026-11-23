using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    // Base for every popup prefab (level-up, chest, pause, result...). Adapted from the Dacodelaac popup system,
    // as a plain MonoBehaviour: dependencies come from VContainer ([Inject]), data comes through Show(data).
    // Prefab layout: root (Canvas + CanvasGroup + GraphicRaycaster + this) > AnimatedGroup > content.
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster))]
    public abstract class BasePopup : MonoBehaviour
    {
        [SerializeField] RectTransform animatedGroup;
        [SerializeField] float duration = 0.2f;
        [SerializeField] Ease showEase = Ease.OutBack;

        Canvas canvas;
        CanvasGroup canvasGroup;

        public PopupController Controller { get; private set; }
        public bool IsShown { get; private set; }

        internal void Setup(PopupController controller)
        {
            Controller = controller;
            canvas = GetComponent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            if (animatedGroup != null) animatedGroup.localScale = Vector3.zero;
            gameObject.SetActive(false);
        }

        internal void SetOrder(int order) => canvas.sortingOrder = order;

        // Asks the controller to close this popup (use from buttons).
        public void Close() => Controller.Dismiss(this);

        internal void Show(object data, bool animated)
        {
            IsShown = true;
            OnShow(data);
            Animate(true, animated, OnShown);
        }

        internal void Hide(bool animated, Action onHidden)
        {
            IsShown = false;
            OnHide();
            Animate(false, animated, () =>
            {
                gameObject.SetActive(false);
                onHidden?.Invoke();
            });
        }

        // Another popup opened on top with PopupShowAction.PauseCurrent.
        internal void Pause(bool animated) => Animate(false, animated, () => gameObject.SetActive(false));

        internal void Resume(bool animated) => Animate(true, animated, null);

        void Animate(bool show, bool animated, Action onComplete)
        {
            DOTween.Kill(this, true); // finish any running show/hide first
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = show;
            if (show) gameObject.SetActive(true);

            var targetAlpha = show ? 1f : 0f;
            var targetScale = show ? Vector3.one : Vector3.zero;
            void Finish()
            {
                canvasGroup.alpha = targetAlpha;
                if (animatedGroup != null) animatedGroup.localScale = targetScale;
                canvasGroup.interactable = show;
                onComplete?.Invoke();
            }

            if (!animated || duration <= 0f)
            {
                Finish();
                return;
            }

            // SetUpdate(true): animates even while the game is paused.
            DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, targetAlpha, show ? duration : duration * 0.5f)
                .SetUpdate(true).SetTarget(this);
            if (animatedGroup != null)
                animatedGroup.DOScale(targetScale, duration).SetEase(show ? showEase : Ease.InBack)
                    .SetUpdate(true).SetTarget(this).OnComplete(Finish);
            else
                DOVirtual.DelayedCall(duration, Finish, true).SetTarget(this);
        }

        protected virtual void OnDestroy() => DOTween.Kill(this);

        // ---- Override in popups ----

        // Called before the show animation. data is whatever the caller passed to Show.
        protected virtual void OnShow(object data) { }

        protected virtual void OnShown() { }

        protected virtual void OnHide() { }
    }
}
