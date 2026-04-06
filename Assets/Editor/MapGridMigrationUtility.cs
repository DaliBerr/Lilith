using Kernel.MapGrid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kernel.MapGrid.Editor
{
    public static class MapGridMigrationUtility
    {
        private const string Cell3DPrefabPath = "Assets/Prefabs/Map/Cell3D.prefab";
        private const string StartScenePath = "Assets/Scenes/Start.unity";
        private const string MenuItemPath = "Tools/Lilith/Map/Migrate Start Scene To Cell3D";

        /// <summary>
        /// summary: 迁移 Start 场景到 Cell3D 地图系统，并立即重建 XZ 平面的格子内容。
        /// param: 无
        /// returns: 无
        /// </summary>
        [MenuItem(MenuItemPath)]
        public static void MigrateStartSceneToCell3D()
        {
            if (!TryPrepareCell3DPrefab(out GameObject cell3DPrefab, out string error))
            {
                Debug.LogError(error);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
            if (!TryResolveMapAuthoring(scene, out MapGridAuthoring authoring, out error))
            {
                Debug.LogError(error);
                return;
            }

            authoring.transform.rotation = Quaternion.identity;
            authoring.DefaultCellPrefab = cell3DPrefab;

            MapGridCoordinateBinding binding = authoring.CoordinateBinding ?? new MapGridCoordinateBinding();
            binding.ComponentTypeName = nameof(CellData);
            binding.SetCoordinatesMethodName = nameof(CellData.SetCoordinates);
            binding.GetCoordinatesMethodName = nameof(CellData.GetCoordinates);
            authoring.CoordinateBinding = binding;

            if (!MapGridEditorUtility.RebuildGrid(authoring, out error))
            {
                Debug.LogError($"Failed to rebuild the Start scene grid: {error}");
                return;
            }

            if (!MapGridEditorUtility.SyncSurfaceState(authoring, out error))
            {
                Debug.LogError($"Failed to sync Start scene surface state: {error}");
                return;
            }

            EditorUtility.SetDirty(authoring);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);

            Debug.Log("MapGridMigrationUtility completed the Cell3D migration for Start.unity.");
        }

        /// <summary>
        /// summary: 确保 Cell3D prefab 带有新的 CellData 绑定，并规范根刚体为静态地图用途。
        /// param: cell3DPrefab 输出已准备好的 Cell3D prefab
        /// param: error 输出失败原因
        /// returns: prefab 准备完成时返回 true
        /// </summary>
        private static bool TryPrepareCell3DPrefab(out GameObject cell3DPrefab, out string error)
        {
            cell3DPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Cell3DPrefabPath);
            error = null;

            if (cell3DPrefab == null)
            {
                error = $"Unable to locate Cell3D prefab at '{Cell3DPrefabPath}'.";
                return false;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(Cell3DPrefabPath);
            try
            {
                CellData cellData = prefabRoot.GetComponent<CellData>();
                if (cellData == null)
                {
                    cellData = prefabRoot.AddComponent<CellData>();
                }

                Rigidbody rootRigidbody = prefabRoot.GetComponent<Rigidbody>();
                if (rootRigidbody == null)
                {
                    rootRigidbody = prefabRoot.AddComponent<Rigidbody>();
                }

                rootRigidbody.useGravity = false;
                rootRigidbody.isKinematic = true;

                cellData.TryCacheSurfaceBindings(overwriteExisting: true);
                cellData.TryRefreshSurfacePresentation(syncTags: false);

                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, Cell3DPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            cell3DPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Cell3DPrefabPath);
            return cell3DPrefab != null;
        }

        /// <summary>
        /// summary: 从场景根对象中解析当前地图系统使用的 MapGridAuthoring。
        /// param: scene 目标场景
        /// param: authoring 输出解析到的 MapGridAuthoring
        /// param: error 输出失败原因
        /// returns: 成功找到唯一 MapGridAuthoring 时返回 true
        /// </summary>
        private static bool TryResolveMapAuthoring(Scene scene, out MapGridAuthoring authoring, out string error)
        {
            authoring = null;
            error = null;

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                authoring = rootObject.GetComponent<MapGridAuthoring>();
                if (authoring != null)
                {
                    return true;
                }
            }

            error = $"Scene '{scene.path}' does not contain a root MapGridAuthoring.";
            return false;
        }
    }
}
