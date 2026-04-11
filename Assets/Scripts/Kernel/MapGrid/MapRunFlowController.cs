using System;
using System.Collections;
using UnityEngine;
using Vocalith.Logging;

namespace Kernel.MapGrid
{
    public enum RunFlowState
    {
        InStartRoom,
        EnteringCombat,
        InCombat,
        ReturningToStartRoom,
    }

    /// <summary>
    /// 负责在同一场景中的起始房间与战斗地图之间切换，并驱动单局战斗的进入与返回。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapRunFlowController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private PlayerPlaneMovement targetPlayer;
        [SerializeField] private MapGridAuthoring startRoomMapGrid;
        [SerializeField] private MapGridAuthoring combatMapGrid;
        [SerializeField] private ArenaSeedMapGenerator combatSeedGenerator;
        [SerializeField] private EnemyGenerator enemyGenerator;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private Transform runtimeEnemyContainer;
        [SerializeField] private Transform runtimePickupContainer;

        [Header("Coordinates")]
        [SerializeField] private Vector2Int startRoomSpawnCell = Vector2Int.zero;
        [SerializeField] private Vector2Int startRoomReturnCell = Vector2Int.zero;
        [SerializeField] private Vector2Int combatEntryCell = Vector2Int.zero;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float postRunReturnDelay = 0.5f;

        private Coroutine pendingReturnRoutine;
        private RunFlowState currentState = RunFlowState.InStartRoom;

        public RunFlowState CurrentState => currentState;

        private void Awake()
        {
            SanitizeConfiguration();
            TryResolveReferences(out _);
        }

        private void OnEnable()
        {
            SubscribeToWaveManager();
        }

        private void Start()
        {
            SubscribeToWaveManager();
            if (!TryInitializeStartRoom(out string error))
            {
                GameDebug.LogError($"[MapRunFlowController] {error}");
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromWaveManager();
            CancelPendingReturnRoutine();
        }

        private void OnValidate()
        {
            SanitizeConfiguration();
        }

        /// <summary>
        /// summary: 当玩家在起始房间触发传送装置时，开始准备新一局战斗并把玩家送入战斗区。
        /// param: 无
        /// returns: 成功开始进入战斗流程时返回 true
        /// </summary>
        public bool TryEnterCombatRun()
        {
            CancelPendingReturnRoutine();
            if (currentState != RunFlowState.InStartRoom)
            {
                return false;
            }

            if (!TryResolveReferences(out string error))
            {
                GameDebug.LogError($"[MapRunFlowController] {error}");
                return false;
            }

            currentState = RunFlowState.EnteringCombat;
            if (TryPrepareCombatRun(out error))
            {
                currentState = RunFlowState.InCombat;
                return true;
            }

            GameDebug.LogError($"[MapRunFlowController] {error}");
            TryReturnPlayerToStartRoom(out _);
            currentState = RunFlowState.InStartRoom;
            return false;
        }

        /// <summary>
        /// summary: 在场景启动时把玩家绑定到起始房间地图，并吸附到起始房间出生格。
        /// param: error 初始化失败时返回的错误信息
        /// returns: 成功进入起始房间待机态时返回 true
        /// </summary>
        private bool TryInitializeStartRoom(out string error)
        {
            currentState = RunFlowState.InStartRoom;
            return TryMovePlayerToMap(startRoomMapGrid, startRoomSpawnCell, out error);
        }

        /// <summary>
        /// summary: 清理旧战斗残留、生成新的战斗布局、切换玩家活动地图并启动波次系统。
        /// param: error 进入战斗流程失败时返回的错误信息
        /// returns: 成功完成整套进入战斗准备时返回 true
        /// </summary>
        private bool TryPrepareCombatRun(out string error)
        {
            error = null;
            if (!TryResolveReferences(out error))
            {
                return false;
            }

            ClearRuntimeChildren(runtimeEnemyContainer);
            ClearRuntimeChildren(runtimePickupContainer);

            if (!enemyGenerator.TrySetTarget(targetPlayer.transform) ||
                !enemyGenerator.TrySetTargetMapGrid(combatMapGrid))
            {
                error = "EnemyGenerator could not bind the player or combat map.";
                return false;
            }

            combatSeedGenerator.RandomizeSeed();
            if (!combatSeedGenerator.TryGenerateAndApplyLayout(combatEntryCell, includePlayerSnap: false, out error))
            {
                return false;
            }

            if (!TryMovePlayerToMap(combatMapGrid, combatEntryCell, out error))
            {
                return false;
            }

            if (!waveManager.TryStartSequence())
            {
                error = "WaveManager failed to start a combat sequence.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// summary: 在战斗自然完成后，把玩家和活动地图切回起始房间并清理本局残留。
        /// param: error 返回起始房间失败时返回的错误信息
        /// returns: 成功回到起始房间时返回 true
        /// </summary>
        private bool TryReturnPlayerToStartRoom(out string error)
        {
            ClearRuntimeChildren(runtimeEnemyContainer);
            ClearRuntimeChildren(runtimePickupContainer);
            bool success = TryMovePlayerToMap(startRoomMapGrid, startRoomReturnCell, out error);
            if (success)
            {
                currentState = RunFlowState.InStartRoom;
            }

            return success;
        }

        /// <summary>
        /// summary: 把玩家绑定到指定地图并吸附到最近 Ground 格。
        /// param: targetMap 当前要切换到的活动地图
        /// param: requestedCoordinates 希望进入的目标格坐标
        /// param: error 切图或传送失败时返回的错误信息
        /// returns: 成功完成地图切换与传送时返回 true
        /// </summary>
        private bool TryMovePlayerToMap(MapGridAuthoring targetMap, Vector2Int requestedCoordinates, out string error)
        {
            error = null;
            if (!TryResolveReferences(out error))
            {
                return false;
            }

            if (targetMap == null)
            {
                error = "Target map grid is missing.";
                return false;
            }

            if (!targetPlayer.TrySetTargetMapGrid(targetMap))
            {
                error = $"PlayerPlaneMovement could not bind map '{targetMap.name}'.";
                return false;
            }

            if (!MapSpawnUtility.TryTeleportToNearestGroundCell(targetMap, targetPlayer.transform, requestedCoordinates, out _, out error))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// summary: 监听波次序列自然结束，并延迟执行返回起始房间流程。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleWaveSequenceCompleted()
        {
            if (!isActiveAndEnabled || currentState != RunFlowState.InCombat)
            {
                return;
            }

            CancelPendingReturnRoutine();
            currentState = RunFlowState.ReturningToStartRoom;
            pendingReturnRoutine = StartCoroutine(ReturnToStartRoomAfterDelay());
        }

        private IEnumerator ReturnToStartRoomAfterDelay()
        {
            if (postRunReturnDelay > 0f)
            {
                yield return new WaitForSeconds(postRunReturnDelay);
            }

            if (!TryReturnPlayerToStartRoom(out string error))
            {
                GameDebug.LogError($"[MapRunFlowController] {error}");
            }

            pendingReturnRoutine = null;
        }

        private void SubscribeToWaveManager()
        {
            if (waveManager == null)
            {
                return;
            }

            waveManager.SequenceCompleted -= HandleWaveSequenceCompleted;
            waveManager.SequenceCompleted += HandleWaveSequenceCompleted;
        }

        private void UnsubscribeFromWaveManager()
        {
            if (waveManager == null)
            {
                return;
            }

            waveManager.SequenceCompleted -= HandleWaveSequenceCompleted;
        }

        private void CancelPendingReturnRoutine()
        {
            if (pendingReturnRoutine == null)
            {
                return;
            }

            StopCoroutine(pendingReturnRoutine);
            pendingReturnRoutine = null;
        }

        private bool TryResolveReferences(out string error)
        {
            error = null;
            targetPlayer ??= FindFirstObjectByType<PlayerPlaneMovement>();
            startRoomMapGrid ??= GameObject.Find("StartRoomMapRoot")?.GetComponent<MapGridAuthoring>();
            combatMapGrid ??= GameObject.Find("CombatMapRoot")?.GetComponent<MapGridAuthoring>();
            combatSeedGenerator ??= combatMapGrid != null ? combatMapGrid.GetComponent<ArenaSeedMapGenerator>() : null;
            enemyGenerator ??= FindFirstObjectByType<EnemyGenerator>();
            waveManager ??= FindFirstObjectByType<WaveManager>();
            runtimeEnemyContainer ??= GameObject.Find("Enemy")?.transform.Find("RuntimeEnemies");
            runtimePickupContainer ??= GameObject.Find("BulletTokenPickup_Authoring")?.transform.Find("RuntimePickups");

            if (targetPlayer == null)
            {
                error = "PlayerPlaneMovement is missing.";
                return false;
            }

            if (startRoomMapGrid == null)
            {
                error = "StartRoomMapRoot is missing a MapGridAuthoring component.";
                return false;
            }

            if (combatMapGrid == null)
            {
                error = "CombatMapRoot is missing a MapGridAuthoring component.";
                return false;
            }

            if (combatSeedGenerator == null)
            {
                error = "CombatMapRoot is missing ArenaSeedMapGenerator.";
                return false;
            }

            if (enemyGenerator == null)
            {
                error = "EnemyGenerator is missing.";
                return false;
            }

            if (waveManager == null)
            {
                error = "WaveManager is missing.";
                return false;
            }

            if (runtimeEnemyContainer == null)
            {
                error = "RuntimeEnemies container is missing under Enemy.";
                return false;
            }

            if (runtimePickupContainer == null)
            {
                error = "RuntimePickups container is missing under BulletTokenPickup_Authoring.";
                return false;
            }

            if (isActiveAndEnabled)
            {
                SubscribeToWaveManager();
            }

            return true;
        }

        private static void ClearRuntimeChildren(Transform container)
        {
            if (container == null)
            {
                return;
            }

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                GameObject child = container.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(child);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child);
                }
            }
        }

        private void SanitizeConfiguration()
        {
            postRunReturnDelay = Mathf.Max(0f, postRunReturnDelay);
        }
    }
}
