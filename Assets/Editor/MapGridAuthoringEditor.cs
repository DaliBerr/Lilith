using System.Collections.Generic;
using Kernel.MapGrid;
using UnityEditor;
using UnityEngine;

namespace Kernel.MapGrid.Editor
{
    internal enum MapGridCellBrushMode
    {
        FillText,
        EraseText,
        SetColliderState,
    }

    internal enum MapGridSceneCellSelectionMode
    {
        Paint,
        Rectangle,
    }

    [CustomEditor(typeof(MapGridAuthoring))]
    public sealed class MapGridAuthoringEditor : UnityEditor.Editor
    {
        private const string SceneCellEditUndoName = "Scene Cell Edit";
        private const float HoverFillAlpha = 0.08f;

        private delegate bool InspectorAction(out string error);

        private readonly HashSet<Vector2Int> strokeVisitedCoordinates = new();

        private GameObject replacementPrefab;
        private bool sceneCellEditEnabled;
        private MapGridSceneCellSelectionMode sceneCellSelectionMode = MapGridSceneCellSelectionMode.Paint;
        private MapGridCellBrushMode cellBrushMode = MapGridCellBrushMode.FillText;
        private string brushText = string.Empty;
        private bool colliderBrushEnabled = true;
        private bool isStrokeActive;
        private int activeStrokeUndoGroup = -1;
        private bool hasHoveredCoordinate;
        private Vector2Int hoveredCoordinate;
        private Vector2Int? lastStrokeCoordinate;
        private Vector2Int? rectangleAnchorCoordinate;
        private Vector2Int? rectanglePreviewCoordinate;
        private string sceneCellEditMessage;
        private MessageType sceneCellEditMessageType = MessageType.Info;

        private SerializedProperty gridWidthProperty;
        private SerializedProperty gridHeightProperty;
        private SerializedProperty cellSizeProperty;
        private SerializedProperty chunkWidthProperty;
        private SerializedProperty chunkHeightProperty;
        private SerializedProperty defaultCellPrefabProperty;
        private SerializedProperty coordinateBindingProperty;
        private SerializedProperty targetCameraProperty;
        private SerializedProperty autoFrameCameraProperty;
        private SerializedProperty cameraPaddingProperty;
        private SerializedProperty cameraDistanceProperty;

        private void OnEnable()
        {
            gridWidthProperty = serializedObject.FindProperty("gridWidth");
            gridHeightProperty = serializedObject.FindProperty("gridHeight");
            cellSizeProperty = serializedObject.FindProperty("cellSize");
            chunkWidthProperty = serializedObject.FindProperty("chunkWidthInCells");
            chunkHeightProperty = serializedObject.FindProperty("chunkHeightInCells");
            defaultCellPrefabProperty = serializedObject.FindProperty("defaultCellPrefab");
            coordinateBindingProperty = serializedObject.FindProperty("coordinateBinding");
            targetCameraProperty = serializedObject.FindProperty("targetCamera");
            autoFrameCameraProperty = serializedObject.FindProperty("autoFrameCamera");
            cameraPaddingProperty = serializedObject.FindProperty("cameraPadding");
            cameraDistanceProperty = serializedObject.FindProperty("cameraDistance");
        }

        private void OnDisable()
        {
            FinishSceneCellEdit(commitChanges: true);
            ResetHoverState();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var authoring = (MapGridAuthoring)target;

            DrawConfiguration();
            EditorGUILayout.Space();
            DrawStatus(authoring);
            EditorGUILayout.Space();
            DrawGridActions(authoring);
            EditorGUILayout.Space();
            DrawSceneCellEditSection(authoring);
            EditorGUILayout.Space();
            DrawReplaceCellSection(authoring);

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            var authoring = (MapGridAuthoring)target;
            if (!sceneCellEditEnabled)
            {
                FinishSceneCellEdit(commitChanges: true);
                ResetHoverState();
                return;
            }

            var requiresBrushText = cellBrushMode == MapGridCellBrushMode.FillText;
            if (!MapGridEditorUtility.TryValidateSceneCellEditing(authoring, requiresBrushText, brushText, out var error))
            {
                SetSceneCellEditMessage(error, MessageType.Warning);
                FinishSceneCellEdit(commitChanges: true);
                ResetHoverState();
                return;
            }

            ClearTransientSceneCellEditMessage();
            HandleSceneCellEditing(authoring, Event.current);
        }

        private void DrawConfiguration()
        {
            EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(gridWidthProperty);
            EditorGUILayout.PropertyField(gridHeightProperty);
            EditorGUILayout.PropertyField(cellSizeProperty);
            EditorGUILayout.PropertyField(chunkWidthProperty);
            EditorGUILayout.PropertyField(chunkHeightProperty);
            EditorGUILayout.PropertyField(defaultCellPrefabProperty);
            EditorGUILayout.PropertyField(coordinateBindingProperty, includeChildren: true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Camera Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targetCameraProperty);
            EditorGUILayout.PropertyField(autoFrameCameraProperty);
            EditorGUILayout.PropertyField(cameraPaddingProperty);
            EditorGUILayout.PropertyField(cameraDistanceProperty);
        }

        private static void DrawStatus(MapGridAuthoring authoring)
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Indexed Cells", $"{authoring.IndexedCellCount} / {authoring.ExpectedCellCount}");
            EditorGUILayout.LabelField("Generated Map Content", authoring.HasGeneratedContent() ? "Present" : "Empty");

            if (!MapGridEditorUtility.TryValidateAuthoring(authoring, requireDefaultPrefab: false, out var validationError))
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);
            }
        }

        private void DrawGridActions(MapGridAuthoring authoring)
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Grid"))
                {
                    ExecuteAction((out string error) => MapGridEditorUtility.GenerateGrid(authoring, out error));
                }

                if (GUILayout.Button("Clear Grid"))
                {
                    ExecuteAction((out string error) => MapGridEditorUtility.ClearGrid(authoring, out error));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Grid"))
                {
                    ExecuteAction((out string error) => MapGridEditorUtility.RebuildGrid(authoring, out error));
                }

                if (GUILayout.Button("Rebuild Index"))
                {
                    ExecuteAction((out string error) => MapGridEditorUtility.RebuildIndex(authoring, out error));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frame Camera"))
                {
                    ExecuteAction((out string error) => MapGridEditorUtility.FrameCamera(authoring, out error));
                }

                if (GUILayout.Button("Disable Empty Text Colliders"))
                {
                    ExecuteAction((out string error) => MapGridEditorUtility.DisableEmptyTextColliders(authoring, out error));
                }
            }

            if (GUILayout.Button("Sync Ground/Wall From Text"))
            {
                ExecuteAction((out string error) => MapGridEditorUtility.SyncGroundWallFromText(authoring, out error));
            }
        }

        private void DrawSceneCellEditSection(MapGridAuthoring authoring)
        {
            EditorGUILayout.LabelField("Scene Cell Edit", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            sceneCellEditEnabled = EditorGUILayout.Toggle("Enable Scene Edit", sceneCellEditEnabled);
            sceneCellSelectionMode = (MapGridSceneCellSelectionMode)EditorGUILayout.EnumPopup("Selection Mode", sceneCellSelectionMode);
            cellBrushMode = (MapGridCellBrushMode)EditorGUILayout.EnumPopup("Operation", cellBrushMode);
            if (cellBrushMode == MapGridCellBrushMode.FillText)
            {
                brushText = EditorGUILayout.TextField("Brush Text", brushText);
            }
            else if (cellBrushMode == MapGridCellBrushMode.SetColliderState)
            {
                colliderBrushEnabled = EditorGUILayout.Toggle("Enable Collider", colliderBrushEnabled);
            }

            if (EditorGUI.EndChangeCheck())
            {
                FinishSceneCellEdit(commitChanges: true);
                SceneView.RepaintAll();
                Repaint();
            }

            EditorGUILayout.HelpBox(
                "With MapRoot selected, use Paint to apply while left-dragging, or Rectangle to drag out a boxed area and apply it on mouse release. Alt / Right Mouse / Middle Mouse keep Scene navigation.",
                MessageType.Info);

            var requiresBrushText = cellBrushMode == MapGridCellBrushMode.FillText;
            if (!MapGridEditorUtility.TryValidateSceneCellEditing(authoring, requiresBrushText, brushText, out var validationError))
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(sceneCellEditMessage))
            {
                EditorGUILayout.HelpBox(sceneCellEditMessage, sceneCellEditMessageType);
            }
        }

        private void DrawReplaceCellSection(MapGridAuthoring authoring)
        {
            EditorGUILayout.LabelField("Replace Selected Cell", EditorStyles.boldLabel);

            var hasSelection = MapGridEditorUtility.TryGetSelectedCellContext(authoring, Selection.activeGameObject, out var selectedCell, out var selectionError);
            if (hasSelection)
            {
                EditorGUILayout.LabelField("Selected Coordinates", $"({selectedCell.Coordinates.x}, {selectedCell.Coordinates.y})");
            }
            else
            {
                EditorGUILayout.HelpBox(selectionError, MessageType.Info);
            }

            replacementPrefab = (GameObject)EditorGUILayout.ObjectField("Replacement Prefab", replacementPrefab, typeof(GameObject), false);

            using (new EditorGUI.DisabledScope(!hasSelection || replacementPrefab == null))
            {
                if (GUILayout.Button("Replace Selected Cell"))
                {
                    ExecuteAction((out string error) =>
                        MapGridEditorUtility.ReplaceSelectedCell(authoring, replacementPrefab, Selection.activeGameObject, out error));
                }
            }
        }

        private void HandleSceneCellEditing(MapGridAuthoring authoring, Event currentEvent)
        {
            UpdateHoveredCoordinate(authoring, currentEvent.mousePosition);

            if (currentEvent.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return;
            }

            if (currentEvent.type == EventType.MouseLeaveWindow)
            {
                ResetHoverState();
                SceneView.RepaintAll();
                return;
            }

            if (currentEvent.type == EventType.Repaint)
            {
                DrawSceneCellEditPreview(authoring);
                return;
            }

            if (currentEvent.alt)
            {
                return;
            }

            if ((currentEvent.type == EventType.MouseDown ||
                 currentEvent.type == EventType.MouseDrag ||
                 currentEvent.type == EventType.MouseUp) &&
                currentEvent.button != 0)
            {
                return;
            }

            if (sceneCellSelectionMode == MapGridSceneCellSelectionMode.Paint)
            {
                HandlePaintSceneCellEditing(authoring, currentEvent);
                return;
            }

            HandleRectangleSceneCellEditing(authoring, currentEvent);
        }

        private void HandlePaintSceneCellEditing(MapGridAuthoring authoring, Event currentEvent)
        {
            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    if (!hasHoveredCoordinate)
                    {
                        return;
                    }

                    BeginSceneCellEdit();
                    if (!ApplyStrokeToCoordinate(authoring, hoveredCoordinate))
                    {
                        currentEvent.Use();
                        return;
                    }

                    currentEvent.Use();
                    break;
                case EventType.MouseDrag:
                    if (!isStrokeActive)
                    {
                        return;
                    }

                    if (hasHoveredCoordinate && !ApplyStrokeToCoordinate(authoring, hoveredCoordinate))
                    {
                        currentEvent.Use();
                        return;
                    }

                    currentEvent.Use();
                    break;
                case EventType.MouseUp:
                    if (!isStrokeActive)
                    {
                        return;
                    }

                    FinishSceneCellEdit(commitChanges: true);
                    currentEvent.Use();
                    break;
                case EventType.MouseMove:
                    SceneView.RepaintAll();
                    break;
            }
        }

        private void HandleRectangleSceneCellEditing(MapGridAuthoring authoring, Event currentEvent)
        {
            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    if (!hasHoveredCoordinate)
                    {
                        return;
                    }

                    BeginSceneCellEdit();
                    rectangleAnchorCoordinate = hoveredCoordinate;
                    rectanglePreviewCoordinate = hoveredCoordinate;
                    SceneView.RepaintAll();
                    currentEvent.Use();
                    break;
                case EventType.MouseDrag:
                    if (!isStrokeActive)
                    {
                        return;
                    }

                    if (hasHoveredCoordinate &&
                        (!rectanglePreviewCoordinate.HasValue || rectanglePreviewCoordinate.Value != hoveredCoordinate))
                    {
                        rectanglePreviewCoordinate = hoveredCoordinate;
                        SceneView.RepaintAll();
                    }

                    currentEvent.Use();
                    break;
                case EventType.MouseUp:
                    if (!isStrokeActive)
                    {
                        return;
                    }

                    if (hasHoveredCoordinate)
                    {
                        rectanglePreviewCoordinate = hoveredCoordinate;
                    }

                    if (!ApplyRectangleSelection(authoring))
                    {
                        currentEvent.Use();
                        return;
                    }

                    FinishSceneCellEdit(commitChanges: true);
                    currentEvent.Use();
                    break;
                case EventType.MouseMove:
                    SceneView.RepaintAll();
                    break;
            }
        }

        private void BeginSceneCellEdit()
        {
            if (isStrokeActive)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            activeStrokeUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(SceneCellEditUndoName);
            isStrokeActive = true;
            lastStrokeCoordinate = null;
            rectangleAnchorCoordinate = null;
            rectanglePreviewCoordinate = null;
            strokeVisitedCoordinates.Clear();
        }

        private void FinishSceneCellEdit(bool commitChanges)
        {
            if (!isStrokeActive || activeStrokeUndoGroup < 0)
            {
                ResetSceneCellEditState();
                return;
            }

            if (commitChanges)
            {
                Undo.CollapseUndoOperations(activeStrokeUndoGroup);
            }
            else
            {
                Undo.RevertAllDownToGroup(activeStrokeUndoGroup);
            }

            ResetSceneCellEditState();
            SceneView.RepaintAll();
        }

        private bool ApplyStrokeToCoordinate(MapGridAuthoring authoring, Vector2Int coordinate)
        {
            var segment = lastStrokeCoordinate.HasValue
                ? MapGridEditorUtility.BuildStrokeCoordinates(lastStrokeCoordinate.Value, coordinate)
                : new List<Vector2Int> { coordinate };

            for (var i = 0; i < segment.Count; i++)
            {
                var point = segment[i];
                if (!strokeVisitedCoordinates.Add(point))
                {
                    continue;
                }

                string error;
                if (!TryApplyOperationToCoordinate(authoring, point, out error))
                {
                    HandleSceneCellEditFailure(error);
                    return false;
                }
            }

            lastStrokeCoordinate = coordinate;
            Repaint();
            return true;
        }

        private bool ApplyRectangleSelection(MapGridAuthoring authoring)
        {
            if (!TryGetRectanglePreviewBounds(out var start, out var end))
            {
                const string error = "Rectangle selection is incomplete.";
                HandleSceneCellEditFailure(error);
                return false;
            }

            var coordinates = MapGridEditorUtility.BuildRectangleCoordinates(start, end);
            for (var i = 0; i < coordinates.Count; i++)
            {
                var point = coordinates[i];
                if (!TryApplyOperationToCoordinate(authoring, point, out var error))
                {
                    HandleSceneCellEditFailure(error);
                    return false;
                }
            }

            Repaint();
            return true;
        }

        private bool TryApplyOperationToCoordinate(MapGridAuthoring authoring, Vector2Int coordinate, out string error)
        {
            error = null;

            switch (cellBrushMode)
            {
                case MapGridCellBrushMode.FillText:
                    return MapGridEditorUtility.TrySetCellText(authoring, coordinate, brushText, out _, out error);
                case MapGridCellBrushMode.EraseText:
                    return MapGridEditorUtility.TrySetCellText(authoring, coordinate, string.Empty, out _, out error);
                case MapGridCellBrushMode.SetColliderState:
                    return MapGridEditorUtility.TrySetCellColliderEnabled(authoring, coordinate, colliderBrushEnabled, out _, out error);
                default:
                    error = "Unknown scene cell edit mode.";
                    return false;
            }
        }

        private void HandleSceneCellEditFailure(string error)
        {
            SetSceneCellEditMessage(error, MessageType.Error);
            ShowSceneNotification(error);
            FinishSceneCellEdit(commitChanges: false);
        }

        private void UpdateHoveredCoordinate(MapGridAuthoring authoring, Vector2 mousePosition)
        {
            var previousHasHovered = hasHoveredCoordinate;
            var previousHoveredCoordinate = hoveredCoordinate;

            hasHoveredCoordinate = TryGetHoveredCoordinate(authoring, mousePosition, out hoveredCoordinate);
            if (previousHasHovered != hasHoveredCoordinate || previousHoveredCoordinate != hoveredCoordinate)
            {
                SceneView.RepaintAll();
            }
        }

        private void DrawSceneCellEditPreview(MapGridAuthoring authoring)
        {
            var previewColor = GetPreviewColor();
            if (sceneCellSelectionMode == MapGridSceneCellSelectionMode.Rectangle &&
                TryGetRectanglePreviewBounds(out var start, out var end))
            {
                DrawRectangleSelection(authoring, start, end, previewColor);
                return;
            }

            if (hasHoveredCoordinate)
            {
                DrawHoveredCell(authoring, hoveredCoordinate, previewColor);
            }
        }

        private bool TryGetRectanglePreviewBounds(out Vector2Int start, out Vector2Int end)
        {
            start = default;
            end = default;

            if (!isStrokeActive || !rectangleAnchorCoordinate.HasValue || !rectanglePreviewCoordinate.HasValue)
            {
                return false;
            }

            start = rectangleAnchorCoordinate.Value;
            end = rectanglePreviewCoordinate.Value;
            return true;
        }

        private static bool TryGetHoveredCoordinate(MapGridAuthoring authoring, Vector2 mousePosition, out Vector2Int coordinate)
        {
            coordinate = default;
            if (authoring == null)
            {
                return false;
            }

            var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            var plane = new Plane(authoring.transform.forward, authoring.transform.position);
            if (!plane.Raycast(ray, out var hitDistance))
            {
                return false;
            }

            var worldPoint = ray.GetPoint(hitDistance);
            return authoring.TryGetCellCoordinateFromWorldPoint(worldPoint, out coordinate);
        }

        private Color GetPreviewColor()
        {
            return cellBrushMode switch
            {
                MapGridCellBrushMode.FillText => new Color(0.2f, 0.85f, 1f, 1f),
                MapGridCellBrushMode.EraseText => new Color(1f, 0.4f, 0.2f, 1f),
                MapGridCellBrushMode.SetColliderState when colliderBrushEnabled => new Color(0.25f, 0.9f, 0.35f, 1f),
                MapGridCellBrushMode.SetColliderState => new Color(0.95f, 0.2f, 0.2f, 1f),
                _ => Color.white,
            };
        }

        private static void DrawHoveredCell(MapGridAuthoring authoring, Vector2Int coordinate, Color outlineColor)
        {
            DrawCoordinateRectangle(authoring, coordinate, coordinate, outlineColor);
        }

        private static void DrawRectangleSelection(MapGridAuthoring authoring, Vector2Int start, Vector2Int end, Color outlineColor)
        {
            DrawCoordinateRectangle(authoring, start, end, outlineColor);
        }

        private static void DrawCoordinateRectangle(MapGridAuthoring authoring, Vector2Int start, Vector2Int end, Color outlineColor)
        {
            var minX = Mathf.Min(start.x, end.x);
            var maxX = Mathf.Max(start.x, end.x);
            var minY = Mathf.Min(start.y, end.y);
            var maxY = Mathf.Max(start.y, end.y);
            var minCenter = authoring.GetCellLocalPosition(minX, minY);
            var maxCenter = authoring.GetCellLocalPosition(maxX, maxY);
            var halfWidth = authoring.CellSize.x * 0.5f;
            var halfHeight = authoring.CellSize.y * 0.5f;
            var vertices = new[]
            {
                new Vector3(minCenter.x - halfWidth, minCenter.y - halfHeight, 0f),
                new Vector3(minCenter.x - halfWidth, maxCenter.y + halfHeight, 0f),
                new Vector3(maxCenter.x + halfWidth, maxCenter.y + halfHeight, 0f),
                new Vector3(maxCenter.x + halfWidth, minCenter.y - halfHeight, 0f),
            };

            using (new Handles.DrawingScope(authoring.transform.localToWorldMatrix))
            {
                var fillColor = new Color(outlineColor.r, outlineColor.g, outlineColor.b, HoverFillAlpha);
                Handles.DrawSolidRectangleWithOutline(vertices, fillColor, outlineColor);
            }
        }

        private void ResetHoverState()
        {
            hasHoveredCoordinate = false;
            hoveredCoordinate = default;
        }

        private void ResetSceneCellEditState()
        {
            isStrokeActive = false;
            activeStrokeUndoGroup = -1;
            lastStrokeCoordinate = null;
            rectangleAnchorCoordinate = null;
            rectanglePreviewCoordinate = null;
            strokeVisitedCoordinates.Clear();
        }

        private void SetSceneCellEditMessage(string message, MessageType messageType)
        {
            sceneCellEditMessage = message;
            sceneCellEditMessageType = messageType;
            Repaint();
        }

        private void ClearTransientSceneCellEditMessage()
        {
            if (sceneCellEditMessageType == MessageType.Error)
            {
                return;
            }

            if (string.IsNullOrEmpty(sceneCellEditMessage))
            {
                return;
            }

            sceneCellEditMessage = null;
            sceneCellEditMessageType = MessageType.Info;
            Repaint();
        }

        private static void ShowSceneNotification(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(message));
        }

        private static void ExecuteAction(InspectorAction action)
        {
            if (action(out var error))
            {
                return;
            }

            UnityEditor.EditorUtility.DisplayDialog("Map Grid", error, "OK");
        }
    }
}
