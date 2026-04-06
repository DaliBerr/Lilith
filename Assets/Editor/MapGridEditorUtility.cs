using System;
using System.Collections.Generic;
using Kernel.MapGrid;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Kernel.MapGrid.Editor
{
    public readonly struct SelectedCellContext
    {
        public SelectedCellContext(
            GameObject cellObject,
            Transform parent,
            Vector2Int coordinates,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            int siblingIndex)
        {
            CellObject = cellObject;
            Parent = parent;
            Coordinates = coordinates;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
            SiblingIndex = siblingIndex;
        }

        public GameObject CellObject { get; }
        public Transform Parent { get; }
        public Vector2Int Coordinates { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }
        public int SiblingIndex { get; }
    }

    public static class MapGridEditorUtility
    {
        private const string SceneCellEditUndoName = "Scene Cell Edit";
        private const string SyncSurfaceStateUndoName = "Sync Surface State";

        private delegate bool UndoableOperation(MapGridAuthoring authoring, int undoGroup, out string error);

        public static string BuildChunkRowName(int chunkY) => $"ChunkRow_{chunkY}";
        public static string BuildChunkName(int chunkX, int chunkY) => $"Chunk_{chunkX}_{chunkY}";
        public static string BuildRowName(int localY) => $"Row_{localY}";
        public static string BuildCellName(int x, int y) => $"Cell_{x}_{y}";

        public static bool TryValidateAuthoring(MapGridAuthoring authoring, bool requireDefaultPrefab, out string error)
        {
            return TryValidateAuthoring(authoring, requireDefaultPrefab, requireCoordinateBinding: true, out error);
        }

        public static bool TryValidateAuthoring(
            MapGridAuthoring authoring,
            bool requireDefaultPrefab,
            bool requireCoordinateBinding,
            out string error)
        {
            error = null;

            if (authoring == null)
            {
                error = "MapGridAuthoring is null.";
                return false;
            }

            if (authoring.GridWidth <= 0 || authoring.GridHeight <= 0)
            {
                error = "Grid width and height must both be greater than zero.";
                return false;
            }

            if (authoring.ChunkWidthInCells <= 0 || authoring.ChunkHeightInCells <= 0)
            {
                error = "Chunk width and height must both be greater than zero.";
                return false;
            }

            if (authoring.CellSize.x <= 0f || authoring.CellSize.y <= 0f)
            {
                error = "Cell size X and Y must both be greater than zero.";
                return false;
            }

            if (!requireCoordinateBinding)
            {
                return true;
            }

            if (authoring.CoordinateBinding == null || !authoring.CoordinateBinding.IsConfigured)
            {
                error = "Coordinate Binding is incomplete. Configure the existing cell component type and coordinate members before running editor actions.";
                return false;
            }

            if (!authoring.CoordinateBinding.TryResolveComponentType(out _, out error))
            {
                return false;
            }

            if (!requireDefaultPrefab)
            {
                return true;
            }

            if (authoring.DefaultCellPrefab == null)
            {
                error = "Default Cell Prefab is not assigned.";
                return false;
            }

            if (!PrefabUtility.IsPartOfPrefabAsset(authoring.DefaultCellPrefab))
            {
                error = "Default Cell Prefab must reference a prefab asset.";
                return false;
            }

            if (!authoring.CoordinateBinding.TryValidate(authoring.DefaultCellPrefab, requireRead: true, requireWrite: true, out error))
            {
                error = $"Default Cell Prefab is not compatible with the configured Coordinate Binding. {error}";
                return false;
            }

            return true;
        }

        public static bool TryValidateSceneCellEditing(MapGridAuthoring authoring, out string error)
        {
            if (!TryValidateIndexedGeneratedCells(authoring, out error))
            {
                return false;
            }

            return TryValidateRequiredTag(MapGridAuthoring.GroundTagName, out error) &&
                   TryValidateRequiredTag(MapGridAuthoring.WallTagName, out error);
        }

        public static bool GenerateGrid(MapGridAuthoring authoring, out string error)
        {
            return ExecuteUndoable("Generate Grid", authoring, GenerateGridInternal, out error);
        }

        public static bool ClearGrid(MapGridAuthoring authoring, out string error)
        {
            return ExecuteUndoable("Clear Grid", authoring, ClearGridInternal, out error);
        }

        public static bool RebuildGrid(MapGridAuthoring authoring, out string error)
        {
            return ExecuteUndoable("Rebuild Grid", authoring, RebuildGridInternal, out error);
        }

        public static bool RebuildIndex(MapGridAuthoring authoring, out string error)
        {
            return ExecuteUndoable("Rebuild Index", authoring, RebuildIndexInternal, out error);
        }

        public static bool SyncSurfaceState(MapGridAuthoring authoring, out string error)
        {
            return ExecuteUndoable(SyncSurfaceStateUndoName, authoring, SyncSurfaceStateInternal, out error);
        }

        public static bool NormalizeCellPresentation(MapGridAuthoring authoring, out string error)
        {
            return SyncSurfaceState(authoring, out error);
        }

        public static bool ReplaceSelectedCell(MapGridAuthoring authoring, GameObject replacementPrefab, GameObject selectedObject, out string error)
        {
            return ExecuteUndoable(
                "Replace Cell",
                authoring,
                (MapGridAuthoring target, int undoGroup, out string operationError) =>
                    ReplaceSelectedCellInternal(target, replacementPrefab, selectedObject, undoGroup, out operationError),
                out error);
        }

        public static bool TryGetSelectedCellContext(MapGridAuthoring authoring, GameObject selectedObject, out SelectedCellContext context, out string error)
        {
            context = default;
            error = null;

            if (!TryValidateAuthoring(authoring, requireDefaultPrefab: false, requireCoordinateBinding: false, out error))
            {
                return false;
            }

            if (selectedObject == null)
            {
                error = "Select a generated cell to replace.";
                return false;
            }

            Transform generatedRoot = authoring.FindGeneratedContentRoot();
            if (generatedRoot == null)
            {
                error = "GeneratedContent was not found under the current MapRoot.";
                return false;
            }

            Transform cellTransform = FindSelectedCellRoot(authoring, selectedObject.transform, generatedRoot);
            if (cellTransform == null)
            {
                error = "The current selection is not a valid generated cell.";
                return false;
            }

            if (!authoring.CoordinateBinding.TryGetCoordinates(cellTransform.gameObject, out Vector2Int coordinates, out error))
            {
                error = $"Unable to read coordinates from '{cellTransform.name}'. {error}";
                return false;
            }

            if (!authoring.IsValidGridCoordinate(coordinates.x, coordinates.y))
            {
                error = $"Selected cell coordinates ({coordinates.x}, {coordinates.y}) are out of grid bounds.";
                return false;
            }

            if (cellTransform.parent == null)
            {
                error = $"Selected cell '{cellTransform.name}' has no parent row container.";
                return false;
            }

            context = new SelectedCellContext(
                cellTransform.gameObject,
                cellTransform.parent,
                coordinates,
                cellTransform.localPosition,
                cellTransform.localRotation,
                cellTransform.localScale,
                cellTransform.GetSiblingIndex());
            return true;
        }

        public static bool TrySetCellSurfaceType(
            MapGridAuthoring authoring,
            Vector2Int coordinates,
            CellData.CellSurfaceType surfaceType,
            out bool changed,
            out string error)
        {
            changed = false;
            error = null;

            if (!TryResolveCellData(authoring, coordinates, out GameObject cellObject, out CellData cellData, out error))
            {
                return false;
            }

            if (!TryCollectCellDirtyTargets(cellObject, cellData, out List<UnityEngine.Object> dirtyTargets, out error))
            {
                return false;
            }

            bool wasCurrent = cellData.IsSurfacePresentationCurrent();
            bool alreadyTargetSurface = cellData.SurfaceType == surfaceType;
            if (alreadyTargetSurface && wasCurrent)
            {
                return true;
            }

            for (int i = 0; i < dirtyTargets.Count; i++)
            {
                Undo.RecordObject(dirtyTargets[i], SceneCellEditUndoName);
            }

            if (!cellData.TrySetSurfaceType(surfaceType))
            {
                error = $"Failed to switch '{cellObject.name}' to surface '{surfaceType}'.";
                return false;
            }

            for (int i = 0; i < dirtyTargets.Count; i++)
            {
                UnityEditor.EditorUtility.SetDirty(dirtyTargets[i]);
            }

            changed = true;
            return true;
        }

        public static bool TrySetCellColliderEnabled(
            MapGridAuthoring authoring,
            Vector2Int coordinates,
            bool enabled,
            out bool changed,
            out string error)
        {
            changed = false;
            error = null;

            if (!TryResolveCellData(authoring, coordinates, out GameObject cellObject, out CellData cellData, out error))
            {
                return false;
            }

            if (!TryResolveManagedCollider(cellObject, cellData, out Collider managedCollider, out error))
            {
                return false;
            }

            if (managedCollider.enabled == enabled)
            {
                return true;
            }

            Undo.RecordObject(cellData, SceneCellEditUndoName);
            Undo.RecordObject(managedCollider, SceneCellEditUndoName);
            Undo.RecordObject(managedCollider.gameObject, SceneCellEditUndoName);

            if (!cellData.SetColliderEnabled(enabled))
            {
                error = $"Failed to update the managed Collider on '{cellObject.name}'.";
                return false;
            }

            UnityEditor.EditorUtility.SetDirty(cellData);
            UnityEditor.EditorUtility.SetDirty(managedCollider);
            UnityEditor.EditorUtility.SetDirty(managedCollider.gameObject);
            changed = true;
            return true;
        }

        public static bool TryResolveCellData(
            MapGridAuthoring authoring,
            Vector2Int coordinates,
            out GameObject cellObject,
            out CellData cellData,
            out string error)
        {
            cellObject = null;
            cellData = null;
            error = null;

            if (!TryResolveIndexedCell(authoring, coordinates, out cellObject, out error))
            {
                return false;
            }

            if (!cellObject.TryGetComponent(out cellData) || cellData == null)
            {
                error = $"Cell '{cellObject.name}' does not contain a CellData component.";
                return false;
            }

            return true;
        }

        public static List<Vector2Int> BuildStrokeCoordinates(Vector2Int start, Vector2Int end)
        {
            var coordinates = new List<Vector2Int>();

            int x0 = start.x;
            int y0 = start.y;
            int x1 = end.x;
            int y1 = end.y;
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int stepX = x0 < x1 ? 1 : -1;
            int stepY = y0 < y1 ? 1 : -1;
            int error = dx - dy;

            while (true)
            {
                coordinates.Add(new Vector2Int(x0, y0));
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int doubledError = error * 2;
                if (doubledError > -dy)
                {
                    error -= dy;
                    x0 += stepX;
                }

                if (doubledError < dx)
                {
                    error += dx;
                    y0 += stepY;
                }
            }

            return coordinates;
        }

        public static List<Vector2Int> BuildRectangleCoordinates(Vector2Int start, Vector2Int end)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            var coordinates = new List<Vector2Int>((maxX - minX + 1) * (maxY - minY + 1));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    coordinates.Add(new Vector2Int(x, y));
                }
            }

            return coordinates;
        }

        private static bool TryValidateIndexedGeneratedCells(MapGridAuthoring authoring, out string error)
        {
            error = null;

            if (!TryValidateAuthoring(authoring, requireDefaultPrefab: false, requireCoordinateBinding: false, out error))
            {
                return false;
            }

            if (!authoring.HasGeneratedContent())
            {
                error = "GeneratedContent is missing. Generate the grid before running map actions.";
                return false;
            }

            if (authoring.IndexedCellCount <= 0)
            {
                error = "The map index is empty. Rebuild Index before running map actions.";
                return false;
            }

            return true;
        }

        private static bool ExecuteUndoable(string actionName, MapGridAuthoring authoring, UndoableOperation operation, out string error)
        {
            error = null;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(actionName);

            try
            {
                if (!operation(authoring, undoGroup, out error))
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    return false;
                }

                if (!authoring.TryRebuildLookupFromEntries(out error))
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    return false;
                }

                UnityEditor.EditorUtility.SetDirty(authoring);
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                error = $"{actionName} failed: {exception.Message}";
                return false;
            }
        }

        private static bool GenerateGridInternal(MapGridAuthoring authoring, int undoGroup, out string error)
        {
            error = null;

            if (!TryValidateAuthoring(authoring, requireDefaultPrefab: true, out error))
            {
                return false;
            }

            if (authoring.HasGeneratedContent() || authoring.IndexedCellCount > 0)
            {
                error = "MapRoot already contains generated map content. Use Rebuild Grid or Clear Grid instead of Generate Grid.";
                return false;
            }

            Transform generatedRoot = GetOrCreateChild(authoring.transform, MapGridAuthoring.GeneratedContentObjectName, "Generate Grid");
            var entries = new List<CellEntry>(authoring.ExpectedCellCount);

            for (int chunkY = 0; chunkY < authoring.GetChunkCountY(); chunkY++)
            {
                Transform chunkRow = GetOrCreateChild(generatedRoot, BuildChunkRowName(chunkY), "Generate Grid");
                int rowStartY = chunkY * authoring.ChunkHeightInCells;
                int rowEndY = Mathf.Min(rowStartY + authoring.ChunkHeightInCells, authoring.GridHeight);

                for (int chunkX = 0; chunkX < authoring.GetChunkCountX(); chunkX++)
                {
                    Transform chunk = GetOrCreateChild(chunkRow, BuildChunkName(chunkX, chunkY), "Generate Grid");
                    int columnStartX = chunkX * authoring.ChunkWidthInCells;
                    int columnEndX = Mathf.Min(columnStartX + authoring.ChunkWidthInCells, authoring.GridWidth);

                    for (int y = rowStartY; y < rowEndY; y++)
                    {
                        int localY = authoring.GetLocalRowInChunk(y);
                        Transform row = GetOrCreateChild(chunk, BuildRowName(localY), "Generate Grid");

                        for (int x = columnStartX; x < columnEndX; x++)
                        {
                            if (!InstantiateCell(authoring.DefaultCellPrefab, row, "Generate Grid", out GameObject cellObject, out error))
                            {
                                return false;
                            }

                            Transform cellTransform = cellObject.transform;
                            cellObject.name = BuildCellName(x, y);
                            cellTransform.localPosition = authoring.GetCellLocalPosition(x, y);
                            cellTransform.localRotation = Quaternion.identity;
                            cellTransform.localScale = Vector3.one;

                            if (!authoring.CoordinateBinding.TrySetCoordinates(cellObject, x, y, out error))
                            {
                                error = $"Failed to write coordinates to '{cellObject.name}'. {error}";
                                return false;
                            }

                            if (cellObject.TryGetComponent(out CellData cellData) && !cellData.TryRefreshSurfacePresentation())
                            {
                                error = $"Failed to initialize surface presentation on '{cellObject.name}'.";
                                return false;
                            }

                            entries.Add(new CellEntry(x, y, cellObject));
                        }
                    }
                }
            }

            Undo.RecordObject(authoring, "Generate Grid");
            authoring.ReplaceCellEntries(entries);
            return true;
        }

        private static bool ClearGridInternal(MapGridAuthoring authoring, int undoGroup, out string error)
        {
            error = null;

            if (authoring == null)
            {
                error = "MapGridAuthoring is null.";
                return false;
            }

            if (!ClearGeneratedContent(authoring, out error))
            {
                return false;
            }

            Undo.RecordObject(authoring, "Clear Grid");
            authoring.ClearCellEntries();
            return true;
        }

        private static bool RebuildGridInternal(MapGridAuthoring authoring, int undoGroup, out string error)
        {
            error = null;

            if (!TryValidateAuthoring(authoring, requireDefaultPrefab: true, out error))
            {
                return false;
            }

            if (!ClearGeneratedContent(authoring, out error))
            {
                return false;
            }

            Undo.RecordObject(authoring, "Rebuild Grid");
            authoring.ClearCellEntries();
            return GenerateGridInternal(authoring, undoGroup, out error);
        }

        private static bool RebuildIndexInternal(MapGridAuthoring authoring, int undoGroup, out string error)
        {
            error = null;

            if (!TryValidateAuthoring(authoring, requireDefaultPrefab: false, out error))
            {
                return false;
            }

            if (!TryCollectIndexEntries(authoring, out List<CellEntry> entries, out error))
            {
                return false;
            }

            Undo.RecordObject(authoring, "Rebuild Index");
            authoring.ReplaceCellEntries(entries);
            return true;
        }

        private static bool SyncSurfaceStateInternal(MapGridAuthoring authoring, int undoGroup, out string error)
        {
            error = null;

            if (!TryValidateIndexedGeneratedCells(authoring, out error))
            {
                return false;
            }

            if (!TryValidateRequiredTag(MapGridAuthoring.GroundTagName, out error) ||
                !TryValidateRequiredTag(MapGridAuthoring.WallTagName, out error))
            {
                return false;
            }

            if (!TryCollectSurfaceRefreshTargets(authoring, out List<UnityEngine.Object> dirtyTargets, out error))
            {
                return false;
            }

            for (int i = 0; i < dirtyTargets.Count; i++)
            {
                Undo.RecordObject(dirtyTargets[i], SyncSurfaceStateUndoName);
            }

            if (!authoring.TryRefreshGroundWallState(out error))
            {
                return false;
            }

            for (int i = 0; i < dirtyTargets.Count; i++)
            {
                UnityEditor.EditorUtility.SetDirty(dirtyTargets[i]);
            }

            return true;
        }

        private static bool ReplaceSelectedCellInternal(
            MapGridAuthoring authoring,
            GameObject replacementPrefab,
            GameObject selectedObject,
            int undoGroup,
            out string error)
        {
            error = null;

            if (!TryValidateAuthoring(authoring, requireDefaultPrefab: false, out error))
            {
                return false;
            }

            if (replacementPrefab == null)
            {
                error = "Replacement prefab is not assigned.";
                return false;
            }

            if (!PrefabUtility.IsPartOfPrefabAsset(replacementPrefab))
            {
                error = "Replacement prefab must reference a prefab asset.";
                return false;
            }

            if (!authoring.CoordinateBinding.TryValidate(replacementPrefab, requireRead: true, requireWrite: true, out error))
            {
                error = $"Replacement prefab is not compatible with the configured Coordinate Binding. {error}";
                return false;
            }

            if (!TryGetSelectedCellContext(authoring, selectedObject, out SelectedCellContext selectedCell, out error))
            {
                return false;
            }

            if (!InstantiateCell(replacementPrefab, selectedCell.Parent, "Replace Cell", out GameObject newCellObject, out error))
            {
                return false;
            }

            Transform newCellTransform = newCellObject.transform;
            newCellObject.name = BuildCellName(selectedCell.Coordinates.x, selectedCell.Coordinates.y);
            newCellTransform.SetSiblingIndex(selectedCell.SiblingIndex);
            newCellTransform.localPosition = selectedCell.LocalPosition;
            newCellTransform.localRotation = selectedCell.LocalRotation;
            newCellTransform.localScale = selectedCell.LocalScale;

            if (!authoring.CoordinateBinding.TrySetCoordinates(newCellObject, selectedCell.Coordinates.x, selectedCell.Coordinates.y, out error))
            {
                error = $"Failed to write coordinates to replacement cell '{newCellObject.name}'. {error}";
                return false;
            }

            if (newCellObject.TryGetComponent(out CellData newCellData) && !newCellData.TryRefreshSurfacePresentation())
            {
                error = $"Failed to initialize the replacement cell presentation on '{newCellObject.name}'.";
                return false;
            }

            Undo.DestroyObjectImmediate(selectedCell.CellObject);

            if (!TryCollectIndexEntries(authoring, out List<CellEntry> entries, out error))
            {
                return false;
            }

            Undo.RecordObject(authoring, "Replace Cell");
            authoring.ReplaceCellEntries(entries);
            Selection.activeGameObject = newCellObject;
            return true;
        }

        private static bool TryCollectIndexEntries(MapGridAuthoring authoring, out List<CellEntry> entries, out string error)
        {
            entries = new List<CellEntry>();
            error = null;

            Transform generatedRoot = authoring.FindGeneratedContentRoot();
            if (generatedRoot == null)
            {
                error = "GeneratedContent was not found under the current MapRoot.";
                return false;
            }

            if (!authoring.CoordinateBinding.TryResolveComponentType(out _, out error))
            {
                return false;
            }

            var seenPositions = new Dictionary<Vector2Int, GameObject>();
            var processedObjects = new HashSet<int>();
            Transform[] allTransforms = generatedRoot.GetComponentsInChildren<Transform>(includeInactive: true);

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform transform = allTransforms[i];
                if (transform == generatedRoot || !transform.name.StartsWith("Row_", StringComparison.Ordinal))
                {
                    continue;
                }

                for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
                {
                    Transform cellTransform = transform.GetChild(childIndex);
                    int instanceId = cellTransform.gameObject.GetInstanceID();
                    if (!processedObjects.Add(instanceId))
                    {
                        continue;
                    }

                    if (!authoring.CoordinateBinding.TryValidate(cellTransform.gameObject, requireRead: true, requireWrite: false, out error))
                    {
                        error = $"Row child '{GetHierarchyPath(cellTransform)}' is not a valid readable cell. {error}";
                        return false;
                    }

                    if (!TryAddCellEntry(authoring, cellTransform.gameObject, seenPositions, entries, out error))
                    {
                        return false;
                    }
                }
            }

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform transform = allTransforms[i];
                if (transform == generatedRoot)
                {
                    continue;
                }

                int instanceId = transform.gameObject.GetInstanceID();
                if (processedObjects.Contains(instanceId))
                {
                    continue;
                }

                if (!authoring.CoordinateBinding.HasCoordinateComponent(transform.gameObject))
                {
                    continue;
                }

                if (HasCoordinateCarrierAncestor(transform.parent, generatedRoot, authoring.CoordinateBinding))
                {
                    continue;
                }

                processedObjects.Add(instanceId);
                if (!TryAddCellEntry(authoring, transform.gameObject, seenPositions, entries, out error))
                {
                    return false;
                }
            }

            if (entries.Count != authoring.ExpectedCellCount)
            {
                Debug.LogWarning(
                    $"Rebuild Index found {entries.Count} readable cells, but grid expects {authoring.ExpectedCellCount}. Use Rebuild Grid to restore missing generated cells.",
                    authoring);
            }

            entries.Sort(static (left, right) =>
            {
                int yCompare = left.Y.CompareTo(right.Y);
                return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
            });
            return true;
        }

        private static bool TryResolveIndexedCell(
            MapGridAuthoring authoring,
            Vector2Int coordinates,
            out GameObject cellObject,
            out string error)
        {
            cellObject = null;
            error = null;

            if (authoring == null)
            {
                error = "MapGridAuthoring is null.";
                return false;
            }

            if (!authoring.IsValidGridCoordinate(coordinates.x, coordinates.y))
            {
                error = $"Coordinate ({coordinates.x}, {coordinates.y}) is out of grid bounds.";
                return false;
            }

            if (!authoring.TryGetCell(coordinates, out cellObject) || cellObject == null)
            {
                error = $"No indexed cell exists at ({coordinates.x}, {coordinates.y}). Rebuild Index or Rebuild Grid before editing cells.";
                return false;
            }

            return true;
        }

        private static bool TryResolveManagedCollider(
            GameObject cellObject,
            CellData cellData,
            out Collider managedCollider,
            out string error)
        {
            managedCollider = null;
            error = null;

            if (cellData == null)
            {
                error = $"Cell '{cellObject?.name ?? "<null>"}' does not contain a CellData component.";
                return false;
            }

            if (!cellData.TryCacheManagedCollider())
            {
                error = $"Cell '{cellObject.name}' does not have a managed Collider configured on its CellData component.";
                return false;
            }

            managedCollider = cellData.ManagedCollider;
            if (managedCollider == null)
            {
                error = $"Cell '{cellObject.name}' does not have a managed Collider configured on its CellData component.";
                return false;
            }

            return true;
        }

        private static bool TryCollectSurfaceRefreshTargets(MapGridAuthoring authoring, out List<UnityEngine.Object> dirtyTargets, out string error)
        {
            dirtyTargets = new List<UnityEngine.Object>();
            error = null;

            var seenInstanceIds = new HashSet<int>();
            var cells = authoring.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                CellEntry entry = cells[i];
                if (entry == null)
                {
                    error = $"Cell entry at index {i} is null.";
                    return false;
                }

                if (!TryResolveCellData(authoring, entry.Position, out GameObject cellObject, out CellData cellData, out error))
                {
                    return false;
                }

                if (!TryCollectCellDirtyTargets(cellObject, cellData, dirtyTargets, seenInstanceIds, out error))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryCollectCellDirtyTargets(GameObject cellObject, CellData cellData, out List<UnityEngine.Object> dirtyTargets, out string error)
        {
            dirtyTargets = new List<UnityEngine.Object>();
            error = null;
            return TryCollectCellDirtyTargets(cellObject, cellData, dirtyTargets, new HashSet<int>(), out error);
        }

        private static bool TryCollectCellDirtyTargets(
            GameObject cellObject,
            CellData cellData,
            ICollection<UnityEngine.Object> dirtyTargets,
            ISet<int> seenInstanceIds,
            out string error)
        {
            error = null;

            if (cellObject == null || cellData == null)
            {
                error = "Cell presentation targets are null.";
                return false;
            }

            if (!cellData.TryCacheSurfaceBindings())
            {
                error = $"Cell '{cellObject.name}' does not have valid wall/ground surface bindings configured on its CellData component.";
                return false;
            }

            TryAddDirtyTarget(cellObject, dirtyTargets, seenInstanceIds);
            TryAddDirtyTarget(cellData, dirtyTargets, seenInstanceIds);
            TryAddDirtyTarget(cellData.WallCollider, dirtyTargets, seenInstanceIds);
            TryAddDirtyTarget(cellData.GroundCollider, dirtyTargets, seenInstanceIds);
            TryAddDirtyTarget(cellData.WallCollider != null ? cellData.WallCollider.gameObject : null, dirtyTargets, seenInstanceIds);
            TryAddDirtyTarget(cellData.GroundCollider != null ? cellData.GroundCollider.gameObject : null, dirtyTargets, seenInstanceIds);
            TryAddDirtyTarget(cellData.WallModelRoot != null ? cellData.WallModelRoot.gameObject : null, dirtyTargets, seenInstanceIds);
            TryAddDirtyTarget(cellData.GroundModelRoot != null ? cellData.GroundModelRoot.gameObject : null, dirtyTargets, seenInstanceIds);
            return true;
        }

        private static bool TryValidateRequiredTag(string tagName, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(tagName))
            {
                error = "Required tag name is empty.";
                return false;
            }

            string[] tags = InternalEditorUtility.tags;
            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tagName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            error = $"Unity tag '{tagName}' is not defined in Project Settings > Tags and Layers.";
            return false;
        }

        private static void TryAddDirtyTarget(UnityEngine.Object target, ICollection<UnityEngine.Object> dirtyTargets, ISet<int> seenInstanceIds)
        {
            if (target == null)
            {
                return;
            }

            int instanceId = target.GetInstanceID();
            if (!seenInstanceIds.Add(instanceId))
            {
                return;
            }

            dirtyTargets.Add(target);
        }

        private static bool TryAddCellEntry(
            MapGridAuthoring authoring,
            GameObject cellObject,
            IDictionary<Vector2Int, GameObject> seenPositions,
            ICollection<CellEntry> entries,
            out string error)
        {
            error = null;

            if (!authoring.CoordinateBinding.TryGetCoordinates(cellObject, out Vector2Int coordinates, out error))
            {
                error = $"Unable to read coordinates from '{GetHierarchyPath(cellObject.transform)}'. {error}";
                return false;
            }

            if (!authoring.IsValidGridCoordinate(coordinates.x, coordinates.y))
            {
                error = $"Cell '{GetHierarchyPath(cellObject.transform)}' resolved to out-of-range coordinate ({coordinates.x}, {coordinates.y}).";
                return false;
            }

            if (seenPositions.ContainsKey(coordinates))
            {
                error = $"Duplicate cell coordinate ({coordinates.x}, {coordinates.y}) detected while rebuilding the index.";
                return false;
            }

            seenPositions.Add(coordinates, cellObject);
            entries.Add(new CellEntry(coordinates.x, coordinates.y, cellObject));
            return true;
        }

        private static bool HasCoordinateCarrierAncestor(Transform transform, Transform stopAt, MapGridCoordinateBinding coordinateBinding)
        {
            while (transform != null && transform != stopAt)
            {
                if (coordinateBinding.HasCoordinateComponent(transform.gameObject))
                {
                    return true;
                }

                transform = transform.parent;
            }

            return false;
        }

        private static bool ClearGeneratedContent(MapGridAuthoring authoring, out string error)
        {
            error = null;
            Transform generatedRoot = authoring.FindGeneratedContentRoot();
            if (generatedRoot != null)
            {
                Undo.DestroyObjectImmediate(generatedRoot.gameObject);
            }

            return true;
        }

        private static Transform GetOrCreateChild(Transform parent, string name, string undoAction)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(childObject, undoAction);
            Undo.SetTransformParent(childObject.transform, parent, undoAction);
            childObject.transform.localPosition = Vector3.zero;
            childObject.transform.localRotation = Quaternion.identity;
            childObject.transform.localScale = Vector3.one;
            return childObject.transform;
        }

        private static bool InstantiateCell(GameObject prefab, Transform parent, string undoAction, out GameObject cellObject, out string error)
        {
            error = null;
            cellObject = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (cellObject == null)
            {
                error = $"Failed to instantiate prefab '{prefab?.name ?? "<null>"}'.";
                return false;
            }

            Undo.RegisterCreatedObjectUndo(cellObject, undoAction);
            return true;
        }

        private static Transform FindSelectedCellRoot(MapGridAuthoring authoring, Transform selection, Transform generatedRoot)
        {
            if (selection == null || generatedRoot == null)
            {
                return null;
            }

            if (!selection.IsChildOf(generatedRoot))
            {
                return null;
            }

            for (Transform current = selection; current != null && current != generatedRoot; current = current.parent)
            {
                if (authoring.CoordinateBinding.HasCoordinateComponent(current.gameObject))
                {
                    return current;
                }
            }

            return null;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            var names = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent)
            {
                names.Push(current.name);
            }

            return string.Join("/", names.ToArray());
        }
    }
}
