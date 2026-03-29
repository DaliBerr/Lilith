using System.Collections.Generic;
using Kernel.MapGrid;
using UnityEditor;
using UnityEngine;

namespace Kernel.MapGrid.Editor
{
    internal enum MapGridTextBrushMode
    {
        FillText,
        EraseText,
    }

    [CustomEditor(typeof(MapGridAuthoring))]
    public sealed class MapGridAuthoringEditor : UnityEditor.Editor
    {
        private const string SceneTextEditUndoName = "Scene Text Edit";
        private const float HoverFillAlpha = 0.08f;

        private delegate bool InspectorAction(out string error);

        private readonly HashSet<Vector2Int> strokeVisitedCoordinates = new();

        private GameObject replacementPrefab;
        private bool sceneTextEditEnabled;
        private MapGridTextBrushMode textBrushMode = MapGridTextBrushMode.FillText;
        private string brushText = string.Empty;
        private bool isStrokeActive;
        private int activeStrokeUndoGroup = -1;
        private bool hasHoveredCoordinate;
        private Vector2Int hoveredCoordinate;
        private Vector2Int? lastStrokeCoordinate;
        private string sceneTextEditMessage;
        private MessageType sceneTextEditMessageType = MessageType.Info;

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
            FinishTextStroke(commitChanges: true);
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
            DrawSceneTextEditSection(authoring);
            EditorGUILayout.Space();
            DrawReplaceCellSection(authoring);

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            var authoring = (MapGridAuthoring)target;
            if (!sceneTextEditEnabled)
            {
                FinishTextStroke(commitChanges: true);
                ResetHoverState();
                return;
            }

            var requiresBrushText = textBrushMode == MapGridTextBrushMode.FillText;
            if (!MapGridEditorUtility.TryValidateTextEditing(authoring, requiresBrushText, brushText, out var error))
            {
                SetSceneTextMessage(error, MessageType.Warning);
                FinishTextStroke(commitChanges: true);
                ResetHoverState();
                return;
            }

            ClearTransientSceneTextMessage();
            HandleSceneTextEditing(authoring, Event.current);
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
            EditorGUILayout.LabelField("Generated Content", authoring.HasGeneratedContent() ? "Present" : "Empty");

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

            if (GUILayout.Button("Frame Camera"))
            {
                ExecuteAction((out string error) => MapGridEditorUtility.FrameCamera(authoring, out error));
            }
        }

        private void DrawSceneTextEditSection(MapGridAuthoring authoring)
        {
            EditorGUILayout.LabelField("Scene Text Edit", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            sceneTextEditEnabled = EditorGUILayout.Toggle("Enable Scene Edit", sceneTextEditEnabled);
            textBrushMode = (MapGridTextBrushMode)EditorGUILayout.EnumPopup("Mode", textBrushMode);
            if (textBrushMode == MapGridTextBrushMode.FillText)
            {
                brushText = EditorGUILayout.TextField("Brush Text", brushText);
            }

            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
                Repaint();
            }

            EditorGUILayout.HelpBox(
                "With MapRoot selected, left-drag in Scene view to batch edit the text on generated cells. Alt / Right Mouse / Middle Mouse keep Scene navigation.",
                MessageType.Info);

            var requiresBrushText = textBrushMode == MapGridTextBrushMode.FillText;
            if (!MapGridEditorUtility.TryValidateTextEditing(authoring, requiresBrushText, brushText, out var validationError))
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(sceneTextEditMessage))
            {
                EditorGUILayout.HelpBox(sceneTextEditMessage, sceneTextEditMessageType);
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

        private void HandleSceneTextEditing(MapGridAuthoring authoring, Event currentEvent)
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

            if (currentEvent.type == EventType.Repaint && hasHoveredCoordinate)
            {
                DrawHoveredCell(authoring, hoveredCoordinate, GetPreviewColor());
                return;
            }

            if (currentEvent.alt || currentEvent.button != 0)
            {
                return;
            }

            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    if (!hasHoveredCoordinate)
                    {
                        return;
                    }

                    BeginTextStroke();
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

                    FinishTextStroke(commitChanges: true);
                    currentEvent.Use();
                    break;
                case EventType.MouseMove:
                    SceneView.RepaintAll();
                    break;
            }
        }

        private void BeginTextStroke()
        {
            if (isStrokeActive)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            activeStrokeUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(SceneTextEditUndoName);
            isStrokeActive = true;
            lastStrokeCoordinate = null;
            strokeVisitedCoordinates.Clear();
        }

        private void FinishTextStroke(bool commitChanges)
        {
            if (!isStrokeActive || activeStrokeUndoGroup < 0)
            {
                ResetStrokeState();
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

            ResetStrokeState();
            SceneView.RepaintAll();
        }

        private bool ApplyStrokeToCoordinate(MapGridAuthoring authoring, Vector2Int coordinate)
        {
            var segment = lastStrokeCoordinate.HasValue
                ? MapGridEditorUtility.BuildStrokeCoordinates(lastStrokeCoordinate.Value, coordinate)
                : new List<Vector2Int> { coordinate };

            var newText = textBrushMode == MapGridTextBrushMode.FillText ? brushText : string.Empty;
            for (var i = 0; i < segment.Count; i++)
            {
                var point = segment[i];
                if (!strokeVisitedCoordinates.Add(point))
                {
                    continue;
                }

                if (!MapGridEditorUtility.TrySetCellText(authoring, point, newText, out _, out var error))
                {
                    SetSceneTextMessage(error, MessageType.Error);
                    ShowSceneNotification(error);
                    FinishTextStroke(commitChanges: false);
                    return false;
                }
            }

            lastStrokeCoordinate = coordinate;
            Repaint();
            return true;
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
            return textBrushMode == MapGridTextBrushMode.FillText
                ? new Color(0.2f, 0.85f, 1f, 1f)
                : new Color(1f, 0.4f, 0.2f, 1f);
        }

        private static void DrawHoveredCell(MapGridAuthoring authoring, Vector2Int coordinate, Color outlineColor)
        {
            var center = authoring.GetCellLocalPosition(coordinate.x, coordinate.y);
            var halfWidth = authoring.CellSize.x * 0.5f;
            var halfHeight = authoring.CellSize.y * 0.5f;
            var vertices = new[]
            {
                new Vector3(center.x - halfWidth, center.y - halfHeight, 0f),
                new Vector3(center.x - halfWidth, center.y + halfHeight, 0f),
                new Vector3(center.x + halfWidth, center.y + halfHeight, 0f),
                new Vector3(center.x + halfWidth, center.y - halfHeight, 0f),
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

        private void ResetStrokeState()
        {
            isStrokeActive = false;
            activeStrokeUndoGroup = -1;
            lastStrokeCoordinate = null;
            strokeVisitedCoordinates.Clear();
        }

        private void SetSceneTextMessage(string message, MessageType messageType)
        {
            sceneTextEditMessage = message;
            sceneTextEditMessageType = messageType;
            Repaint();
        }

        private void ClearTransientSceneTextMessage()
        {
            if (sceneTextEditMessageType == MessageType.Error)
            {
                return;
            }

            if (string.IsNullOrEmpty(sceneTextEditMessage))
            {
                return;
            }

            sceneTextEditMessage = null;
            sceneTextEditMessageType = MessageType.Info;
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
