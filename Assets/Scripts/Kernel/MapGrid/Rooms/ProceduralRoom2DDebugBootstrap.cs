using UnityEngine;
using Vocalith.Logging;

namespace Kernel.MapGrid
{
    /// <summary>
    /// Wires the generated debug Tilemap room to the temporary 2D player and 2D camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralRoom2DDebugBootstrap : MonoBehaviour
    {
        [SerializeField] private ProceduralRoomMapDebugController roomController;
        [SerializeField] private TilemapRoomPresenter roomPresenter;
        [SerializeField] private Player2DMovementController playerPrefab;
        [SerializeField] private Player2DIsometricCamera targetCamera;
        [SerializeField] private Transform runtimeContainer;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool disableLegacy3DPlayerOnSpawn = true;
        [SerializeField] private bool disableLegacy3DCameraComponents = true;

        private Player2DMovementController spawnedPlayer;

        public Player2DMovementController SpawnedPlayer => spawnedPlayer;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (!spawnOnStart)
            {
                return;
            }

            if (!GenerateAndSpawn(out string error))
            {
                GameDebug.LogError($"[ProceduralRoom2DDebugBootstrap] {error}");
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public bool GenerateAndSpawn(out string error)
        {
            error = null;
            if (!ResolveReferences())
            {
                error = "Missing room controller, player prefab, or target camera.";
                return false;
            }

            if (!roomController.GenerateRun(out error))
            {
                return false;
            }

            if (!roomController.TryGetCurrentLayout(out RoomResolvedLayout layout, out error))
            {
                return false;
            }

            if (!TryResolvePlayerEntryWorldPosition(layout, out Vector3 spawnPosition, out error))
            {
                return false;
            }

            SpawnPlayer(spawnPosition);
            BindCamera();
            ApplyDebugIsolation();
            return true;
        }

        private void SpawnPlayer(Vector3 spawnPosition)
        {
            if (spawnedPlayer != null)
            {
                DestroyRuntimeObject(spawnedPlayer.gameObject);
            }

            Transform parent = runtimeContainer != null ? runtimeContainer : transform;
            spawnedPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity, parent);
            spawnedPlayer.name = playerPrefab.name;
            spawnedPlayer.SetTargetCamera(targetCamera != null ? targetCamera.GetComponent<Camera>() : Camera.main);
            spawnedPlayer.WarpTo(spawnPosition);
        }

        private void BindCamera()
        {
            if (targetCamera == null || spawnedPlayer == null)
            {
                return;
            }

            targetCamera.enabled = true;
            targetCamera.SetTarget(spawnedPlayer.transform);
            targetCamera.SnapToTarget();
        }

        private void ApplyDebugIsolation()
        {
            if (disableLegacy3DPlayerOnSpawn)
            {
                PlayerPlaneMovement legacyPlayer = FindFirstObjectByType<PlayerPlaneMovement>();
                if (legacyPlayer != null)
                {
                    legacyPlayer.enabled = false;
                }
            }

            if (!disableLegacy3DCameraComponents || targetCamera == null)
            {
                return;
            }

            PlayerFollowCamera followCamera = targetCamera.GetComponent<PlayerFollowCamera>();
            if (followCamera != null)
            {
                followCamera.enabled = false;
            }

            CameraOcclusionFader occlusionFader = targetCamera.GetComponent<CameraOcclusionFader>();
            if (occlusionFader != null)
            {
                occlusionFader.enabled = false;
            }
        }

        private bool TryResolvePlayerEntryWorldPosition(RoomResolvedLayout layout, out Vector3 worldPosition, out string error)
        {
            worldPosition = default;
            error = null;
            if (layout == null)
            {
                error = "Current room layout is missing.";
                return false;
            }

            if (layout.PlayerEntryCells == null || layout.PlayerEntryCells.Count == 0)
            {
                error = $"Room layout '{layout.TemplateId}' has no player entry cells.";
                return false;
            }

            TilemapRoomPresenter presenter = roomPresenter != null ? roomPresenter : roomController.GetComponentInChildren<TilemapRoomPresenter>(true);
            if (presenter == null || presenter.FloorTilemap == null)
            {
                error = "Tilemap presenter or floor Tilemap is missing.";
                return false;
            }

            Vector2Int entry = layout.PlayerEntryCells[0];
            worldPosition = presenter.FloorTilemap.GetCellCenterWorld(new Vector3Int(entry.x, entry.y, 0));
            worldPosition.z = presenter.FloorTilemap.transform.position.z;
            return true;
        }

        private bool ResolveReferences()
        {
            if (roomController == null)
            {
                roomController = GetComponent<ProceduralRoomMapDebugController>();
            }

            if (roomPresenter == null && roomController != null)
            {
                roomPresenter = roomController.GetComponentInChildren<TilemapRoomPresenter>(true);
            }

            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Player2DIsometricCamera>();
            }

            return roomController != null && playerPrefab != null && targetCamera != null;
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
