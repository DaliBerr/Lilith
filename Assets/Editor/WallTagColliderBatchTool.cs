using System.Collections.Generic;
using Kernel.MapGrid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kernel.MapGrid.Editor
{
    /// <summary>
    /// Adds colliders for wall-tagged art objects so camera occlusion raycasts can hit them.
    /// Skips objects under CellData to avoid duplicating gameplay wall colliders.
    /// </summary>
    public static class WallTagColliderBatchTool
    {
        private const string OpenScenesMenuPath = "Tools/Lilith/Wall Collider/Add Missing Colliders In Open Scenes";
        private const string SelectedPrefabsMenuPath = "Tools/Lilith/Wall Collider/Add Missing Colliders In Selected Prefabs";

        private enum ProcessState
        {
            Added,
            SkippedCellData,
            SkippedNestedWallTag,
            SkippedExistingCollider,
            SkippedNoRenderer,
        }

        private struct ProcessCounters
        {
            public int Candidates;
            public int Added;
            public int SkippedCellData;
            public int SkippedNestedWallTag;
            public int SkippedExistingCollider;
            public int SkippedNoRenderer;
        }

        /// <summary>
        /// summary: Batch-adds missing non-trigger BoxCollider components for Wall-tagged objects in all open scenes.
        /// param: none
        /// returns: none
        /// </summary>
        [MenuItem(OpenScenesMenuPath)]
        private static void AddMissingCollidersInOpenScenes()
        {
            ProcessCounters counters = default;
            int modifiedSceneCount = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                bool sceneModified = false;
                foreach (GameObject wallObject in EnumerateWallTaggedObjects(scene))
                {
                    counters.Candidates++;
                    ProcessState state = TryAddCollider(wallObject, useUndo: true);
                    CountState(ref counters, state);
                    sceneModified |= state == ProcessState.Added;
                }

                if (!sceneModified)
                {
                    continue;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                modifiedSceneCount++;
            }

            Debug.Log(BuildSummary(
                "Wall collider batch (open scenes)",
                counters,
                $"Modified scenes: {modifiedSceneCount}."));
        }

        /// <summary>
        /// summary: Validates whether the selected-prefab batch menu should be enabled.
        /// param: none
        /// returns: true when at least one selected asset is a prefab
        /// </summary>
        [MenuItem(SelectedPrefabsMenuPath, true)]
        private static bool ValidateAddMissingCollidersInSelectedPrefabs()
        {
            string[] selectionGuids = Selection.assetGUIDs;
            if (selectionGuids == null || selectionGuids.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < selectionGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(selectionGuids[i]);
                if (assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// summary: Batch-adds missing non-trigger BoxCollider components for Wall-tagged objects in selected prefab assets.
        /// param: none
        /// returns: none
        /// </summary>
        [MenuItem(SelectedPrefabsMenuPath)]
        private static void AddMissingCollidersInSelectedPrefabs()
        {
            string[] selectionGuids = Selection.assetGUIDs;
            if (selectionGuids == null || selectionGuids.Length == 0)
            {
                Debug.LogWarning("[WallTagColliderBatchTool] No assets selected.");
                return;
            }

            ProcessCounters counters = default;
            int modifiedPrefabCount = 0;

            for (int i = 0; i < selectionGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(selectionGuids[i]);
                if (!assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                bool prefabModified = false;

                foreach (GameObject wallObject in EnumerateWallTaggedObjects(prefabRoot))
                {
                    counters.Candidates++;
                    ProcessState state = TryAddCollider(wallObject, useUndo: false);
                    CountState(ref counters, state);
                    prefabModified |= state == ProcessState.Added;
                }

                if (prefabModified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                    modifiedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            if (modifiedPrefabCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log(BuildSummary(
                "Wall collider batch (selected prefabs)",
                counters,
                $"Modified prefabs: {modifiedPrefabCount}."));
        }

        /// <summary>
        /// summary: Enumerates all Wall-tagged objects from every root object in a scene.
        /// param name="scene": loaded scene to scan
        /// returns: an enumerable of Wall-tagged objects
        /// </summary>
        private static IEnumerable<GameObject> EnumerateWallTaggedObjects(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                foreach (GameObject wallObject in EnumerateWallTaggedObjects(roots[i]))
                {
                    yield return wallObject;
                }
            }
        }

        /// <summary>
        /// summary: Enumerates Wall-tagged descendants under a root object.
        /// param name="root": hierarchy root
        /// returns: an enumerable of Wall-tagged objects
        /// </summary>
        private static IEnumerable<GameObject> EnumerateWallTaggedObjects(GameObject root)
        {
            if (root == null)
            {
                yield break;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current == null)
                {
                    continue;
                }

                if (current.gameObject.CompareTag(MapGridAuthoring.WallTagName))
                {
                    yield return current.gameObject;
                }
            }
        }

        /// <summary>
        /// summary: Tries to add a collider to a Wall-tagged object using renderer bounds.
        /// param name="target": candidate wall object
        /// param name="useUndo": whether to register Undo for component creation
        /// returns: processing state describing whether a collider was added or skipped
        /// </summary>
        private static ProcessState TryAddCollider(GameObject target, bool useUndo)
        {
            if (target == null)
            {
                return ProcessState.SkippedNoRenderer;
            }

            if (HasWallTagAncestor(target.transform))
            {
                return ProcessState.SkippedNestedWallTag;
            }

            if (target.GetComponentInParent<CellData>() != null)
            {
                return ProcessState.SkippedCellData;
            }

            if (HasNonTriggerCollider(target))
            {
                return ProcessState.SkippedExistingCollider;
            }

            if (!TryBuildRendererLocalBounds(target.transform, out Bounds localBounds))
            {
                return ProcessState.SkippedNoRenderer;
            }

            if (localBounds.size.sqrMagnitude <= Mathf.Epsilon)
            {
                return ProcessState.SkippedNoRenderer;
            }

            BoxCollider collider = useUndo ? Undo.AddComponent<BoxCollider>(target) : target.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.center = localBounds.center;
            collider.size = localBounds.size;

            if (!useUndo)
            {
                EditorUtility.SetDirty(target);
            }

            return ProcessState.Added;
        }

        /// <summary>
        /// summary: Checks whether any ancestor already has Wall tag, to avoid duplicate colliders in nested tagged hierarchies.
        /// param name="transform": current transform
        /// returns: true when an ancestor has Wall tag
        /// </summary>
        private static bool HasWallTagAncestor(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            Transform current = transform.parent;
            while (current != null)
            {
                if (current.gameObject.CompareTag(MapGridAuthoring.WallTagName))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// summary: Checks whether the object hierarchy already has any non-trigger collider.
        /// param name="target": wall object root
        /// returns: true when a non-trigger collider already exists
        /// </summary>
        private static bool HasNonTriggerCollider(GameObject target)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !colliders[i].isTrigger)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// summary: Builds an aggregated local-space bounds from all renderer world bounds under a root transform.
        /// param name="root": target hierarchy root
        /// param name="localBounds": computed local-space bounds
        /// returns: true when at least one renderer bound can be aggregated
        /// </summary>
        private static bool TryBuildRendererLocalBounds(Transform root, out Bounds localBounds)
        {
            localBounds = default;
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            Matrix4x4 worldToLocal = root.worldToLocalMatrix;
            bool initialized = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                EncapsulateRendererBounds(renderer.bounds, worldToLocal, ref initialized, ref localBounds);
            }

            return initialized;
        }

        /// <summary>
        /// summary: Converts a renderer world bounds to local corners and encapsulates them into an accumulated bounds.
        /// param name="worldBounds": renderer world bounds
        /// param name="worldToLocal": matrix to convert world points to local points
        /// param name="initialized": whether the accumulated bounds has been initialized
        /// param name="accumulatedBounds": aggregated local-space bounds
        /// returns: none
        /// </summary>
        private static void EncapsulateRendererBounds(
            Bounds worldBounds,
            Matrix4x4 worldToLocal,
            ref bool initialized,
            ref Bounds accumulatedBounds)
        {
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 localCorner = worldToLocal.MultiplyPoint3x4(corner);

                        if (!initialized)
                        {
                            accumulatedBounds = new Bounds(localCorner, Vector3.zero);
                            initialized = true;
                        }
                        else
                        {
                            accumulatedBounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// summary: Counts each processing state for summary logging.
        /// param name="counters": counters accumulator
        /// param name="state": processing state of one object
        /// returns: none
        /// </summary>
        private static void CountState(ref ProcessCounters counters, ProcessState state)
        {
            switch (state)
            {
                case ProcessState.Added:
                    counters.Added++;
                    break;
                case ProcessState.SkippedCellData:
                    counters.SkippedCellData++;
                    break;
                case ProcessState.SkippedNestedWallTag:
                    counters.SkippedNestedWallTag++;
                    break;
                case ProcessState.SkippedExistingCollider:
                    counters.SkippedExistingCollider++;
                    break;
                case ProcessState.SkippedNoRenderer:
                    counters.SkippedNoRenderer++;
                    break;
            }
        }

        /// <summary>
        /// summary: Builds a detailed summary string for batch execution logs.
        /// param name="title": summary title
        /// param name="counters": processing counters
        /// param name="tail": trailing message text
        /// returns: formatted summary string
        /// </summary>
        private static string BuildSummary(string title, ProcessCounters counters, string tail)
        {
            return $"[WallTagColliderBatchTool] {title}. Candidates: {counters.Candidates}, Added: {counters.Added}, " +
                   $"Skipped(CellData): {counters.SkippedCellData}, Skipped(NestedWallTag): {counters.SkippedNestedWallTag}, " +
                   $"Skipped(ExistingCollider): {counters.SkippedExistingCollider}, Skipped(NoRenderer): {counters.SkippedNoRenderer}. {tail}";
        }
    }
}
