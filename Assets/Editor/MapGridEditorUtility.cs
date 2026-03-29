using System;
using System.Collections.Generic;
using Kernel.MapGrid;
using TMPro;
using UnityEditor;
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

        public static bool TryValidateTextEditing(MapGridAuthoring authoring, bool requireBrushText, string brushText, out string error)
        {
            error = null;

            if (!TryValidateAuthoring(authoring, requireDefaultPrefab: false, out error))
            {
                return false;
            }

            if (!authoring.HasGeneratedContent())
            {
                error = "GeneratedContent is missing. Generate the grid before editing text in Scene view.";
                return false;
            }

            if (authoring.IndexedCellCount <= 0)
            {
                error = "The map index is empty. Rebuild Index before editing text in Scene view.";
                return false;
            }

            if (requireBrushText && string.IsNullOrEmpty(brushText))
            {
                error = "Brush Text cannot be empty while Fill Text mode is active.";
                return false;
            }

            return true;
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

        public static bool FrameCamera(MapGridAuthoring authoring, out string error)
        {
            return ExecuteUndoable("Frame Camera", authoring, FrameCameraInternal, out error);
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

            var generatedRoot = authoring.FindGeneratedContentRoot();
            if (generatedRoot == null)
            {
                error = "GeneratedContent was not found under the current MapRoot.";
                return false;
            }

            var cellTransform = FindSelectedCellRoot(authoring, selectedObject.transform, generatedRoot);
            if (cellTransform == null)
            {
                error = "The current selection is not a valid generated cell.";
                return false;
            }

            if (!authoring.CoordinateBinding.TryGetCoordinates(cellTransform.gameObject, out var coordinates, out error))
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

        public static bool TrySetCellText(
            MapGridAuthoring authoring,
            Vector2Int coordinates,
            string newText,
            out bool changed,
            out string error)
        {
            changed = false;
            error = null;

            if (!TryResolveCellText(authoring, coordinates, out _, out var textComponent, out error))
            {
                return false;
            }

            if (textComponent.text == newText)
            {
                return true;
            }

            Undo.RecordObject(textComponent, "Scene Text Edit");
            textComponent.text = newText;
            UnityEditor.EditorUtility.SetDirty(textComponent);
            changed = true;
            return true;
        }

        public static bool TryGetUniqueCellText(GameObject cellObject, out TMP_Text textComponent, out string error)
        {
            textComponent = null;
            error = null;

            if (cellObject == null)
            {
                error = "Cell object is null.";
                return false;
            }

            var textComponents = cellObject.GetComponentsInChildren<TMP_Text>(includeInactive: true);
            if (textComponents == null || textComponents.Length == 0)
            {
                error = $"Cell '{cellObject.name}' does not contain a TMP_Text component.";
                return false;
            }

            if (textComponents.Length > 1)
            {
                error = $"Cell '{cellObject.name}' contains {textComponents.Length} TMP_Text components. Scene text editing requires exactly one.";
                return false;
            }

            textComponent = textComponents[0];
            return true;
        }

        public static List<Vector2Int> BuildStrokeCoordinates(Vector2Int start, Vector2Int end)
        {
            var coordinates = new List<Vector2Int>();

            var x0 = start.x;
            var y0 = start.y;
            var x1 = end.x;
            var y1 = end.y;
            var dx = Mathf.Abs(x1 - x0);
            var dy = Mathf.Abs(y1 - y0);
            var stepX = x0 < x1 ? 1 : -1;
            var stepY = y0 < y1 ? 1 : -1;
            var error = dx - dy;

            while (true)
            {
                coordinates.Add(new Vector2Int(x0, y0));
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                var doubledError = error * 2;
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

        private static bool ExecuteUndoable(string actionName, MapGridAuthoring authoring, UndoableOperation operation, out string error)
        {
            error = null;
            var shouldRebuildLookup = actionName != "Frame Camera";

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(actionName);

            try
            {
                if (!operation(authoring, undoGroup, out error))
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    return false;
                }

                if (shouldRebuildLookup && !authoring.TryRebuildLookupFromEntries(out error))
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    return false;
                }

                if ((actionName == "Frame Camera" || authoring.AutoFrameCamera) &&
                    !TryFrameCameraForUndo(authoring, actionName, out error))
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

            var generatedRoot = GetOrCreateChild(authoring.transform, MapGridAuthoring.GeneratedContentObjectName, "Generate Grid");
            var entries = new List<CellEntry>(authoring.ExpectedCellCount);

            for (var chunkY = 0; chunkY < authoring.GetChunkCountY(); chunkY++)
            {
                var chunkRow = GetOrCreateChild(generatedRoot, BuildChunkRowName(chunkY), "Generate Grid");
                var rowStartY = chunkY * authoring.ChunkHeightInCells;
                var rowEndY = Mathf.Min(rowStartY + authoring.ChunkHeightInCells, authoring.GridHeight);

                for (var chunkX = 0; chunkX < authoring.GetChunkCountX(); chunkX++)
                {
                    var chunk = GetOrCreateChild(chunkRow, BuildChunkName(chunkX, chunkY), "Generate Grid");
                    var columnStartX = chunkX * authoring.ChunkWidthInCells;
                    var columnEndX = Mathf.Min(columnStartX + authoring.ChunkWidthInCells, authoring.GridWidth);

                    for (var y = rowStartY; y < rowEndY; y++)
                    {
                        var localY = authoring.GetLocalRowInChunk(y);
                        var row = GetOrCreateChild(chunk, BuildRowName(localY), "Generate Grid");

                        for (var x = columnStartX; x < columnEndX; x++)
                        {
                            if (!InstantiateCell(authoring.DefaultCellPrefab, row, "Generate Grid", out var cellObject, out error))
                            {
                                return false;
                            }

                            var cellTransform = cellObject.transform;
                            cellObject.name = BuildCellName(x, y);
                            cellTransform.localPosition = authoring.GetCellLocalPosition(x, y);
                            cellTransform.localRotation = Quaternion.identity;
                            cellTransform.localScale = Vector3.one;

                            if (!authoring.CoordinateBinding.TrySetCoordinates(cellObject, x, y, out error))
                            {
                                error = $"Failed to write coordinates to '{cellObject.name}'. {error}";
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

            if (!TryCollectIndexEntries(authoring, out var entries, out error))
            {
                return false;
            }

            Undo.RecordObject(authoring, "Rebuild Index");
            authoring.ReplaceCellEntries(entries);
            return true;
        }

        private static bool FrameCameraInternal(MapGridAuthoring authoring, int undoGroup, out string error)
        {
            error = null;
            if (authoring == null)
            {
                error = "MapGridAuthoring is null.";
                return false;
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

            if (!TryGetSelectedCellContext(authoring, selectedObject, out var selectedCell, out error))
            {
                return false;
            }

            if (!InstantiateCell(replacementPrefab, selectedCell.Parent, "Replace Cell", out var newCellObject, out error))
            {
                return false;
            }

            var newCellTransform = newCellObject.transform;
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

            Undo.DestroyObjectImmediate(selectedCell.CellObject);

            if (!TryCollectIndexEntries(authoring, out var entries, out error))
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

            var generatedRoot = authoring.FindGeneratedContentRoot();
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
            var allTransforms = generatedRoot.GetComponentsInChildren<Transform>(includeInactive: true);

            for (var i = 0; i < allTransforms.Length; i++)
            {
                var transform = allTransforms[i];
                if (transform == generatedRoot || !transform.name.StartsWith("Row_", StringComparison.Ordinal))
                {
                    continue;
                }

                for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
                {
                    var cellTransform = transform.GetChild(childIndex);
                    var instanceId = cellTransform.gameObject.GetInstanceID();
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

            for (var i = 0; i < allTransforms.Length; i++)
            {
                var transform = allTransforms[i];
                if (transform == generatedRoot)
                {
                    continue;
                }

                var instanceId = transform.gameObject.GetInstanceID();
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
                var yCompare = left.Y.CompareTo(right.Y);
                return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
            });
            return true;
        }

        private static bool TryResolveCellText(
            MapGridAuthoring authoring,
            Vector2Int coordinates,
            out GameObject cellObject,
            out TMP_Text textComponent,
            out string error)
        {
            cellObject = null;
            textComponent = null;
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
                error = $"No indexed cell exists at ({coordinates.x}, {coordinates.y}). Rebuild Index or Rebuild Grid before editing text.";
                return false;
            }

            if (!TryGetUniqueCellText(cellObject, out textComponent, out error))
            {
                error = $"Failed to resolve the TMP_Text for '{cellObject.name}'. {error}";
                return false;
            }

            return true;
        }

        private static bool TryAddCellEntry(
            MapGridAuthoring authoring,
            GameObject cellObject,
            IDictionary<Vector2Int, GameObject> seenPositions,
            ICollection<CellEntry> entries,
            out string error)
        {
            error = null;

            if (!authoring.CoordinateBinding.TryGetCoordinates(cellObject, out var coordinates, out error))
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
            var generatedRoot = authoring.FindGeneratedContentRoot();
            if (generatedRoot != null)
            {
                Undo.DestroyObjectImmediate(generatedRoot.gameObject);
            }

            return true;
        }

        private static Transform GetOrCreateChild(Transform parent, string name, string undoAction)
        {
            var child = parent.Find(name);
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

        private static bool TryFrameCameraForUndo(MapGridAuthoring authoring, string undoAction, out string error)
        {
            error = null;

            if (authoring == null)
            {
                error = "MapGridAuthoring is null.";
                return false;
            }

            var camera = authoring.ResolveTargetCamera();
            if (camera == null)
            {
                error = "No target camera was found. Assign Target Camera or tag a camera as MainCamera.";
                return false;
            }

            Undo.RecordObject(camera, undoAction);
            Undo.RecordObject(camera.transform, undoAction);

            if (!Kernel.MapGrid.MapGridCameraUtility.TryFrameCamera(
                    authoring,
                    camera,
                    authoring.CameraPadding,
                    authoring.CameraDistance,
                    out error))
            {
                return false;
            }

            UnityEditor.EditorUtility.SetDirty(camera);
            UnityEditor.EditorUtility.SetDirty(camera.transform);
            return true;
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

            for (var current = selection; current != null && current != generatedRoot; current = current.parent)
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
            for (var current = transform; current != null; current = current.parent)
            {
                names.Push(current.name);
            }

            return string.Join("/", names.ToArray());
        }
    }
}
