using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;
using Kernel.GameState;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Vocalith.Logging;
using System.Collections;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/MainMenuUI")]
    public sealed class MainMenuScreen : GameUIScreen
    {
        public Button startBtn,loadBtn, optionsBtn, quitBtn;
        
        public override Status currentStatus { get; } = StatusList.InMainMenuStatus;

        public List<Image> backgroundImages;

        /// <summary>
        /// 主菜单初始化：绑定按钮事件、设置背景。
        /// </summary>
        /// <returns>无返回值。</returns>
        protected override void OnInit()
        {
            startBtn.onClick.AddListener(
                () => StartCoroutine(TryStartGame())
            );
            loadBtn.onClick.AddListener(
                () => TryOpenLoadMenu()
            );
            // loadBtn.onClick.AddListener(() => UIManager.Instance.PushScreen<LoadGameModal>());
            optionsBtn.onClick.AddListener(
                () => TryOpenOptions()
            );
            quitBtn.onClick.AddListener(
                () => TryQuitGame()
            );
            //TODO: 在没有存档的情况下禁用加载按钮
            // TODO: 随机背景图


        }
        /// <summary>
        /// 开始游戏按钮逻辑：根据当前状态决定如何进入游戏。
        /// </summary>
        /// <returns>无返回值。</returns>
        private IEnumerator TryStartGame()
        {
            yield return null;
            // if (StatusController.HasStatus(StatusList.DevModeStatus))
            // {
            //     yield return SceneManager.LoadSceneAsync("Main");
            //     // 开发模式：直接切到主场景
            //     StatusController.AddStatus(StatusList.PlayingStatus);
            //     UIManager.Instance.RequestStartGame();
            // }
            // else
            // {
            //     // 普通模式：显示加载界面 + 标记加载状态
            //     // StatusController.AddStatus(StatusList.GameLoadingStatus);
            //     UIManager.Instance.PushScreen<GameLoading>();

            //     // TODO: 在这里启动真正的关卡加载 / 存档读取协程，
            //     // 加载完成后再切换场景，并视情况切到 Playing / Paused 等状态。
            // }
        }

        private void TryOpenLoadMenu()
        {
            // UIManager.Instance.PushScreen<LoadMenuUI>();
            // StatusController.AddStatus(StatusList.InMenuStatus);
        }
        private void TryOpenOptions()
        {
            // UIManager.Instance.PushScreen<OptionsModal>();
            // StatusController.AddStatus(StatusList.InMenuStatus);
        }

        private void TryQuitGame()
        {
            // UIManager.Instance.ShowModal<QuitConfirmPopupModal>();
        }
    }
}
