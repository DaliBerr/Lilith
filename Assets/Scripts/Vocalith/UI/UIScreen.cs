
using System.Collections;
using UnityEngine;

namespace Vocalith.UI
{
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public abstract class UIScreen : MonoBehaviour
    {
        [Header("Auto")]
        [SerializeField] protected CanvasGroup canvasGroup;

        public virtual bool PreservePrefabRootRectTransform => false;
        protected bool isVisible;
        protected UIManager ui;

        // 生命周期：子类可 override
        protected virtual void OnBeforeShow() { }
        protected virtual void OnAfterShow()  { }
        protected virtual void OnBeforeHide() { }
        protected virtual void OnAfterHide()  { }
        protected virtual void OnManagerInitialized() { }

        internal void __Init(UIManager manager)
        {
            ui = manager;
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            isVisible = false;
            OnManagerInitialized();
            OnInit();
        }

        protected virtual void Awake()
        {
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
            
            // 【关键点】
            // 在物体诞生的第一毫秒，强制将其设为透明。
            // 此时渲染管线还没来得及画出这一帧，所以玩家完全看不见闪烁。
            // 而在编辑器（Edit Mode）下，Awake 不会运行，所以你可以正常看到界面。
            if (canvasGroup)
            {
                canvasGroup.alpha = 0f;
                
                // 建议同时也把交互关掉，防止隐形按钮挡住操作
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
        // public abstract void HandleStart();
        // 子类做一次性初始化（抓引用/绑定按钮）
        protected virtual void OnInit() { }
        
        public virtual IEnumerator Show(float fade = 0.15f)
        {
            OnBeforeShow();
            gameObject.SetActive(true);
            ui?.ApplyResponsiveLayouts();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            isVisible = true;

            if (fade > 0f)
            {
                float t = 0f;
                while (t < fade)
                {
                    t += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fade);
                    yield return null;
                }
            }
            canvasGroup.alpha = 1f;
            OnAfterShow();
            ui?.ApplyResponsiveLayouts();
        }

        public virtual IEnumerator Hide(float fade = 0.12f)
        {
            OnBeforeHide();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            isVisible = false;

            if (fade > 0f)
            {
                float t = 0f;
                float start = canvasGroup.alpha;
                while (t < fade)
                {
                    t += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(start, 0f, t / fade);
                    yield return null;
                }
            }
            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            OnAfterHide();
        }

        public void setAlpha(float alpha)
        {
            canvasGroup.alpha = alpha;
        }
        public float getAlpha()
        {
            float alpha = canvasGroup.alpha;
            return alpha;
        }
        
    }
}
