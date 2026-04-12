using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.EventSystem;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// 负责展示 Boss 名称与笔画式生命条的常驻 Overlay UI。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Boss Info UI")]
    public sealed class BossInfoUIScreen : UIScreen
    {
        [SerializeField] private RectTransform healthBarRoot;
        [SerializeField] private StrokeHealthBarController healthBarController;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField, Min(0f)] private float transitionDuration = 0.12f;

        private IDisposable bossStartedSubscription;
        private IDisposable bossEndedSubscription;
        private Coroutine visibilityRoutine;
        private Enemy currentBoss;

        protected override void Awake()
        {
            if (!canvasGroup)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (!canvasGroup)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            base.Awake();
        }

        private void OnEnable()
        {
            if (ui != null)
            {
                TryAutoBindReferences();
                SubscribeToBossEvents();
            }
        }

        private void OnDisable()
        {
            UnbindCurrentBoss();
            DisposeBossSubscriptions();
        }

        protected override void OnInit()
        {
            TryAutoBindReferences();
            SubscribeToBossEvents();
            ApplyVisibility(visible: false, immediate: true);
            ClearBossPresentation();
        }

        private void OnDestroy()
        {
            UnbindCurrentBoss();
            DisposeBossSubscriptions();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
            transitionDuration = Mathf.Max(0f, transitionDuration);
        }

        /// <summary>
        /// Boss Overlay 需要保持常驻激活以便接收事件，因此 Show 只改透明度不主动拦截输入。
        /// </summary>
        /// <param name="fade">淡入时长。</param>
        /// <returns>协程枚举器。</returns>
        public override IEnumerator Show(float fade = 0.15f)
        {
            OnBeforeShow();
            gameObject.SetActive(true);
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            isVisible = true;

            if (fade > 0f)
            {
                float elapsed = 0f;
                float startAlpha = canvasGroup.alpha;
                while (elapsed < fade)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(elapsed / fade));
                    yield return null;
                }
            }

            canvasGroup.alpha = 1f;
            OnAfterShow();
        }

        /// <summary>
        /// Boss Overlay 隐藏时保留对象激活，仅收起可见性以继续监听后续遭遇事件。
        /// </summary>
        /// <param name="fade">淡出时长。</param>
        /// <returns>协程枚举器。</returns>
        public override IEnumerator Hide(float fade = 0.12f)
        {
            OnBeforeHide();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            isVisible = false;

            if (fade > 0f)
            {
                float elapsed = 0f;
                float startAlpha = canvasGroup.alpha;
                while (elapsed < fade)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / fade));
                    yield return null;
                }
            }

            canvasGroup.alpha = 0f;
            OnAfterHide();
        }

        /// <summary>
        /// 订阅 Boss 遭遇开始与结束事件，驱动 Overlay 的显隐和目标切换。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void SubscribeToBossEvents()
        {
            if (bossStartedSubscription == null)
            {
                bossStartedSubscription = EventManager.eventBus.Subscribe<BossEncounterStartedEvent>(HandleBossEncounterStarted);
            }

            if (bossEndedSubscription == null)
            {
                bossEndedSubscription = EventManager.eventBus.Subscribe<BossEncounterEndedEvent>(HandleBossEncounterEnded);
            }
        }

        /// <summary>
        /// 释放当前 Overlay 对事件总线的订阅。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void DisposeBossSubscriptions()
        {
            bossStartedSubscription?.Dispose();
            bossEndedSubscription?.Dispose();
            bossStartedSubscription = null;
            bossEndedSubscription = null;
        }

        /// <summary>
        /// 响应 Boss 开战事件，绑定目标并刷新名称与血量显示。
        /// </summary>
        /// <param name="evt">本次 Boss 开战事件。</param>
        /// <returns>无。</returns>
        private void HandleBossEncounterStarted(BossEncounterStartedEvent evt)
        {
            if (evt.boss == null)
            {
                return;
            }

            TryAutoBindReferences();
            UnbindCurrentBoss();
            currentBoss = evt.boss;
            currentBoss.Damaged += HandleCurrentBossDamaged;

            if (bossNameText != null)
            {
                bossNameText.text = evt.displayName ?? string.Empty;
            }

            healthBarController?.SetHealthImmediate(evt.currentHealth, evt.maxHealth);
            ApplyVisibility(visible: true, immediate: transitionDuration <= 0f);
        }

        /// <summary>
        /// 响应 Boss 遭遇结束事件，并在当前展示目标匹配时收起 Overlay。
        /// </summary>
        /// <param name="evt">本次 Boss 遭遇结束事件。</param>
        /// <returns>无。</returns>
        private void HandleBossEncounterEnded(BossEncounterEndedEvent evt)
        {
            if (currentBoss != null && evt.boss != null && evt.boss != currentBoss)
            {
                return;
            }

            UnbindCurrentBoss();
            ClearBossPresentation();
            ApplyVisibility(visible: false, immediate: transitionDuration <= 0f);
        }

        /// <summary>
        /// 在当前 Boss 受击时刷新共享笔画血条。
        /// </summary>
        /// <param name="boss">当前受击的 Boss 实例。</param>
        /// <returns>无。</returns>
        private void HandleCurrentBossDamaged(Enemy boss)
        {
            if (boss == null || boss != currentBoss)
            {
                return;
            }

            healthBarController?.SetHealthAnimatedValue(boss.CurrentHealth, boss.MaxHealth);
        }

        /// <summary>
        /// 解除当前 Boss 的受击订阅，避免场景切换后保留无效引用。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void UnbindCurrentBoss()
        {
            if (currentBoss != null)
            {
                currentBoss.Damaged -= HandleCurrentBossDamaged;
            }

            currentBoss = null;
        }

        /// <summary>
        /// 清空当前 Boss 的名称与血条展示。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void ClearBossPresentation()
        {
            if (bossNameText != null)
            {
                bossNameText.text = string.Empty;
            }

            healthBarController?.ClearDisplay();
        }

        /// <summary>
        /// 统一控制 Overlay 的可见性，并在需要时执行一次短淡入淡出。
        /// </summary>
        /// <param name="visible">目标可见状态。</param>
        /// <param name="immediate">是否立即切换而不播放过渡。</param>
        /// <returns>无。</returns>
        private void ApplyVisibility(bool visible, bool immediate)
        {
            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                return;
            }

            if (immediate)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                isVisible = visible;
                return;
            }

            visibilityRoutine = StartCoroutine(ApplyVisibilityRoutine(visible));
        }

        /// <summary>
        /// 执行一次 Overlay 的显隐过渡，并保持对象常驻激活。
        /// </summary>
        /// <param name="visible">目标可见状态。</param>
        /// <returns>协程枚举器。</returns>
        private IEnumerator ApplyVisibilityRoutine(bool visible)
        {
            yield return visible ? Show(transitionDuration) : Hide(transitionDuration);
            visibilityRoutine = null;
        }

        /// <summary>
        /// 自动补齐 Boss Overlay 需要的文本、血条根节点和共享血条组件引用。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void TryAutoBindReferences()
        {
            healthBarRoot ??= transform.Find("Hp Bar") as RectTransform;
            if (healthBarRoot != null)
            {
                healthBarController ??= healthBarRoot.GetComponent<StrokeHealthBarController>();
            }

            bossNameText ??= transform.Find("Boss Info/Text (TMP)")?.GetComponent<TMP_Text>();

            if (TryGetComponent(out Image rootImage))
            {
                rootImage.raycastTarget = false;
            }
        }
    }
}
