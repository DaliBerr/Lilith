using System;
using System.Collections.Generic;
using Kernel.Bullet;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(LinkedTokenData))]
public sealed class LinkedTokenDataEditor : Editor
{
    private SerializedProperty itemIdProperty;
    private SerializedProperty descriptionProperty;
    private SerializedProperty linkedTokensProperty;
    private SerializedProperty damageMultiplierProperty;
    private SerializedProperty pickupDisplayTextOverrideProperty;
    private ReorderableList linkedTokensList;

    private void OnEnable()
    {
        itemIdProperty = serializedObject.FindProperty("itemId");
        descriptionProperty = serializedObject.FindProperty("description");
        linkedTokensProperty = serializedObject.FindProperty("linkedTokens");
        damageMultiplierProperty = serializedObject.FindProperty("damageMultiplier");
        pickupDisplayTextOverrideProperty = serializedObject.FindProperty("pickupDisplayTextOverride");

        linkedTokensList = new ReorderableList(serializedObject, linkedTokensProperty, true, true, true, true);
        linkedTokensList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Linked Tokens");
        };
        linkedTokensList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
        linkedTokensList.drawElementCallback = DrawLinkedTokenElement;
        linkedTokensList.onAddDropdownCallback = DrawAddTokenDropdown;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(itemIdProperty);
        EditorGUILayout.PropertyField(descriptionProperty);
        EditorGUILayout.PropertyField(damageMultiplierProperty);
        EditorGUILayout.PropertyField(pickupDisplayTextOverrideProperty);

        EditorGUILayout.Space();
        linkedTokensList.DoLayoutList();
        DrawDropArea();
        DrawInspectorHints();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLinkedTokenElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty element = linkedTokensProperty.GetArrayElementAtIndex(index);
        rect.y += 2f;
        rect.height = EditorGUIUtility.singleLineHeight;

        EditorGUI.BeginChangeCheck();
        UnityEngine.Object next = EditorGUI.ObjectField(rect, $"Element {index}", element.objectReferenceValue, typeof(BaseTokenData), false);
        if (EditorGUI.EndChangeCheck())
        {
            element.objectReferenceValue = next;
        }
    }

    private void DrawDropArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0f, 44f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag BaseTokenData assets here to append them.");

        Event currentEvent = Event.current;
        if (!dropArea.Contains(currentEvent.mousePosition))
        {
            return;
        }

        if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
        {
            bool hasValidToken = false;
            for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
            {
                if (DragAndDrop.objectReferences[i] is BaseTokenData)
                {
                    hasValidToken = true;
                    break;
                }
            }

            DragAndDrop.visualMode = hasValidToken ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (currentEvent.type == EventType.DragPerform && hasValidToken)
            {
                DragAndDrop.AcceptDrag();
                AppendDraggedTokens();
            }

            currentEvent.Use();
        }
    }

    private void AppendDraggedTokens()
    {
        bool changed = false;
        for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
        {
            if (DragAndDrop.objectReferences[i] is not BaseTokenData token)
            {
                continue;
            }

            int insertIndex = linkedTokensProperty.arraySize;
            linkedTokensProperty.InsertArrayElementAtIndex(insertIndex);
            linkedTokensProperty.GetArrayElementAtIndex(insertIndex).objectReferenceValue = token;
            changed = true;
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }

    private void DrawAddTokenDropdown(Rect buttonRect, ReorderableList list)
    {
        GenericMenu menu = new GenericMenu();
        List<BaseTokenData> availableTokens = CollectAvailableBaseTokens();

        if (availableTokens.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No BaseTokenData assets found"));
            menu.DropDown(buttonRect);
            return;
        }

        for (int i = 0; i < availableTokens.Count; i++)
        {
            BaseTokenData token = availableTokens[i];
            string path = AssetDatabase.GetAssetPath(token);
            string displayPath = path.Replace('/', '\\');
            GUIContent label = new GUIContent($"{token.name} ({displayPath})");
            menu.AddItem(label, false, () => AppendTokenFromMenu(token));
        }

        menu.DropDown(buttonRect);
    }

    private static List<BaseTokenData> CollectAvailableBaseTokens()
    {
        string[] guids = AssetDatabase.FindAssets("t:BaseTokenData");
        List<BaseTokenData> tokens = new List<BaseTokenData>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            BaseTokenData token = AssetDatabase.LoadAssetAtPath<BaseTokenData>(path);
            if (token == null)
            {
                continue;
            }

            tokens.Add(token);
        }

        tokens.Sort((left, right) =>
        {
            string leftPath = AssetDatabase.GetAssetPath(left);
            string rightPath = AssetDatabase.GetAssetPath(right);
            return string.Compare(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
        });
        return tokens;
    }

    private void AppendTokenFromMenu(BaseTokenData token)
    {
        if (token == null)
        {
            return;
        }

        serializedObject.Update();
        int insertIndex = linkedTokensProperty.arraySize;
        linkedTokensProperty.InsertArrayElementAtIndex(insertIndex);
        linkedTokensProperty.GetArrayElementAtIndex(insertIndex).objectReferenceValue = token;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void DrawInspectorHints()
    {
        EditorGUILayout.HelpBox("Only single-cell BaseTokenData assets can be linked here. Nested LinkedTokenData items are not supported.", MessageType.Info);
    }
}
