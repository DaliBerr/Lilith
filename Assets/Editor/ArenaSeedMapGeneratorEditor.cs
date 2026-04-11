using UnityEditor;
using UnityEngine;

namespace Kernel.MapGrid.Editor
{
    [CustomEditor(typeof(ArenaSeedMapGenerator))]
    public sealed class ArenaSeedMapGeneratorEditor : UnityEditor.Editor
    {
        private SerializedProperty seedProperty;
        private SerializedProperty generateOnAwakeProperty;
        private SerializedProperty snapPlayerToNearestGroundCellProperty;
        private SerializedProperty borderWallThicknessProperty;
        private SerializedProperty obstacleCountMinProperty;
        private SerializedProperty obstacleCountMaxProperty;
        private SerializedProperty obstacleWidthRangeProperty;
        private SerializedProperty obstacleHeightRangeProperty;
        private SerializedProperty edgeClearanceCellsProperty;
        private SerializedProperty playerSafeRadiusCellsProperty;
        private SerializedProperty spawnAnnulusHalfWidthCellsProperty;
        private SerializedProperty maxPlacementAttemptsPerObstacleProperty;

        private void OnEnable()
        {
            seedProperty = serializedObject.FindProperty("seed");
            generateOnAwakeProperty = serializedObject.FindProperty("generateOnAwake");
            snapPlayerToNearestGroundCellProperty = serializedObject.FindProperty("snapPlayerToNearestGroundCell");
            borderWallThicknessProperty = serializedObject.FindProperty("borderWallThickness");
            obstacleCountMinProperty = serializedObject.FindProperty("obstacleCountMin");
            obstacleCountMaxProperty = serializedObject.FindProperty("obstacleCountMax");
            obstacleWidthRangeProperty = serializedObject.FindProperty("obstacleWidthRange");
            obstacleHeightRangeProperty = serializedObject.FindProperty("obstacleHeightRange");
            edgeClearanceCellsProperty = serializedObject.FindProperty("edgeClearanceCells");
            playerSafeRadiusCellsProperty = serializedObject.FindProperty("playerSafeRadiusCells");
            spawnAnnulusHalfWidthCellsProperty = serializedObject.FindProperty("spawnAnnulusHalfWidthCells");
            maxPlacementAttemptsPerObstacleProperty = serializedObject.FindProperty("maxPlacementAttemptsPerObstacle");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(seedProperty);
            EditorGUILayout.PropertyField(generateOnAwakeProperty);
            EditorGUILayout.PropertyField(snapPlayerToNearestGroundCellProperty);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Layout Tuning", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Increase obstacle counts, obstacle sizes, or border thickness for denser maps with more walls. Reduce them and raise clearances for more open ground.",
                MessageType.Info);
            EditorGUILayout.PropertyField(borderWallThicknessProperty);
            EditorGUILayout.PropertyField(obstacleCountMinProperty);
            EditorGUILayout.PropertyField(obstacleCountMaxProperty);
            EditorGUILayout.PropertyField(obstacleWidthRangeProperty);
            EditorGUILayout.PropertyField(obstacleHeightRangeProperty);
            EditorGUILayout.PropertyField(edgeClearanceCellsProperty);
            EditorGUILayout.PropertyField(playerSafeRadiusCellsProperty);
            EditorGUILayout.PropertyField(spawnAnnulusHalfWidthCellsProperty);
            EditorGUILayout.PropertyField(maxPlacementAttemptsPerObstacleProperty);

            serializedObject.ApplyModifiedProperties();
        }
    }
}