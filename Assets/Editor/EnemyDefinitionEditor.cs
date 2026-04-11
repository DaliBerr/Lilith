using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(EnemyDefinition))]
public sealed class EnemyDefinitionEditor : UnityEditor.Editor
{
    private SerializedProperty enemyIdProperty;
    private SerializedProperty displayNameProperty;
    private SerializedProperty runtimePrefabProperty;
    private SerializedProperty movementKindProperty;
    private SerializedProperty attackKindProperty;
    private SerializedProperty dashMovementProperty;
    private SerializedProperty keepDistanceMovementProperty;
    private SerializedProperty aggroOnHitMovementProperty;
    private SerializedProperty orbitTargetMovementProperty;
    private SerializedProperty visualProperty;
    private SerializedProperty rangedBulletAttackProperty;
    private SerializedProperty skillCastingProperty;
    private SerializedProperty skillSlotsProperty;

    private ReorderableList skillSlotsList;

    private void OnEnable()
    {
        enemyIdProperty = serializedObject.FindProperty("enemyId");
        displayNameProperty = serializedObject.FindProperty("displayName");
        runtimePrefabProperty = serializedObject.FindProperty("runtimePrefab");
        movementKindProperty = serializedObject.FindProperty("movementKind");
        attackKindProperty = serializedObject.FindProperty("attackKind");
        dashMovementProperty = serializedObject.FindProperty("dashMovement");
        keepDistanceMovementProperty = serializedObject.FindProperty("keepDistanceMovement");
        aggroOnHitMovementProperty = serializedObject.FindProperty("aggroOnHitMovement");
        orbitTargetMovementProperty = serializedObject.FindProperty("orbitTargetMovement");
        visualProperty = serializedObject.FindProperty("visual");
        rangedBulletAttackProperty = serializedObject.FindProperty("rangedBulletAttack");
        skillCastingProperty = serializedObject.FindProperty("skillCasting");
        skillSlotsProperty = serializedObject.FindProperty("skillSlots");

        skillSlotsList = new ReorderableList(serializedObject, skillSlotsProperty, draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);
        skillSlotsList.drawHeaderCallback = DrawSkillSlotsHeader;
        skillSlotsList.drawElementCallback = DrawSkillSlotElement;
        skillSlotsList.elementHeightCallback = GetSkillSlotElementHeight;
        skillSlotsList.onAddCallback = AddSkillSlotElement;
    }

    /// <summary>
    /// summary: 绘制 EnemyDefinition 的分组化 inspector，并让 skillSlots 使用专用的可重排列表。
    /// param: 无
    /// returns: 无
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawIdentitySection();
        EditorGUILayout.Space();
        DrawMovementSection();
        EditorGUILayout.Space();
        DrawAttackSection();
        EditorGUILayout.Space();
        DrawSkillSection();
        EditorGUILayout.Space();
        DrawVisualSection();

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// summary: 绘制敌人身份与运行时 prefab 绑定区域。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void DrawIdentitySection()
    {
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enemyIdProperty);
        EditorGUILayout.PropertyField(displayNameProperty);
        EditorGUILayout.PropertyField(runtimePrefabProperty);
    }

    /// <summary>
    /// summary: 根据当前 movement kind 只显示对应的移动配置。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void DrawMovementSection()
    {
        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(movementKindProperty);

        switch ((EnemyMovementKind)movementKindProperty.enumValueIndex)
        {
            case EnemyMovementKind.ChaseThenDash:
                EditorGUILayout.PropertyField(dashMovementProperty, includeChildren: true);
                break;

            case EnemyMovementKind.KeepDistance:
                EditorGUILayout.PropertyField(keepDistanceMovementProperty, includeChildren: true);
                break;

            case EnemyMovementKind.AggroOnHit:
                EditorGUILayout.PropertyField(aggroOnHitMovementProperty, includeChildren: true);
                break;

            case EnemyMovementKind.OrbitTarget:
                EditorGUILayout.PropertyField(orbitTargetMovementProperty, includeChildren: true);
                break;
        }
    }

    /// <summary>
    /// summary: 绘制攻击配置，并只展开当前 attack kind 需要的子配置。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void DrawAttackSection()
    {
        EditorGUILayout.LabelField("Attack", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(attackKindProperty);

        if ((EnemyAttackKind)attackKindProperty.enumValueIndex == EnemyAttackKind.RangedBulletToken)
        {
            EditorGUILayout.PropertyField(rangedBulletAttackProperty, includeChildren: true);
        }
    }

    /// <summary>
    /// summary: 绘制技能槽调度区域，并用可重排列表展示每个技能槽的详细配置。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void DrawSkillSection()
    {
        EditorGUILayout.LabelField("Skills", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(skillCastingProperty, includeChildren: true);
        EditorGUILayout.HelpBox(
            "Skill slots are executed in list order. Each slot has its own cooldown, and multiple ready skills can be cast in the same tick.",
            MessageType.Info);
        skillSlotsList.DoLayoutList();
    }

    /// <summary>
    /// summary: 绘制视觉配置区域。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void DrawVisualSection()
    {
        EditorGUILayout.LabelField("Visual", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(visualProperty, includeChildren: true);
    }

    /// <summary>
    /// summary: 绘制 skillSlots 列表的折叠标题。
    /// param: rect 当前列表头的绘制区域
    /// returns: 无
    /// </summary>
    private static void DrawSkillSlotsHeader(Rect rect)
    {
        EditorGUI.LabelField(rect, "Skill Slots");
    }

    /// <summary>
    /// summary: 绘制单个技能槽条目，并根据技能类型展开对应的子配置。
    /// param: rect 当前条目的绘制区域
    /// param: index 当前条目的索引
    /// param: isActive 当前条目是否被选中
    /// param: isFocused 当前条目是否获得焦点
    /// returns: 无
    /// </summary>
    private void DrawSkillSlotElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (index < 0 || index >= skillSlotsProperty.arraySize)
        {
            return;
        }

        SerializedProperty element = skillSlotsProperty.GetArrayElementAtIndex(index);
        SerializedProperty skillKindProperty = element.FindPropertyRelative("skillKind");
        SerializedProperty cooldownSecondsProperty = element.FindPropertyRelative("cooldownSeconds");
        SerializedProperty castRangeProperty = element.FindPropertyRelative("castRange");
        SerializedProperty summonSkillProperty = element.FindPropertyRelative("summonSkill");
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float currentY = rect.y + 2f;

        rect.height = lineHeight;
        element.isExpanded = EditorGUI.Foldout(
            new Rect(rect.x, currentY, rect.width, lineHeight),
            element.isExpanded,
            BuildSkillSlotLabel(index, skillKindProperty),
            true);

        if (!element.isExpanded)
        {
            return;
        }

        currentY += lineHeight + spacing;
        EditorGUI.indentLevel++;
        EditorGUI.PropertyField(new Rect(rect.x, currentY, rect.width, lineHeight), skillKindProperty);
        currentY += lineHeight + spacing;
        EditorGUI.PropertyField(new Rect(rect.x, currentY, rect.width, lineHeight), cooldownSecondsProperty);
        currentY += lineHeight + spacing;
        EditorGUI.PropertyField(new Rect(rect.x, currentY, rect.width, lineHeight), castRangeProperty);
        currentY += lineHeight + spacing;

        if ((EnemySkillKind)skillKindProperty.enumValueIndex == EnemySkillKind.SummonEnemy)
        {
            float summonHeight = EditorGUI.GetPropertyHeight(summonSkillProperty, includeChildren: true);
            EditorGUI.PropertyField(
                new Rect(rect.x, currentY, rect.width, summonHeight),
                summonSkillProperty,
                includeChildren: true);
        }

        EditorGUI.indentLevel--;
    }

    /// <summary>
    /// summary: 计算单个技能槽条目的高度。
    /// param: index 当前条目的索引
    /// returns: 当前条目需要的绘制高度
    /// </summary>
    private float GetSkillSlotElementHeight(int index)
    {
        if (index < 0 || index >= skillSlotsProperty.arraySize)
        {
            return EditorGUIUtility.singleLineHeight + 6f;
        }

        SerializedProperty element = skillSlotsProperty.GetArrayElementAtIndex(index);
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float height = lineHeight + 6f;
        if (!element.isExpanded)
        {
            return height;
        }

        height += lineHeight + spacing;
        height += lineHeight + spacing;
        height += lineHeight + spacing;

        SerializedProperty skillKindProperty = element.FindPropertyRelative("skillKind");
        if ((EnemySkillKind)skillKindProperty.enumValueIndex == EnemySkillKind.SummonEnemy)
        {
            SerializedProperty summonSkillProperty = element.FindPropertyRelative("summonSkill");
            height += EditorGUI.GetPropertyHeight(summonSkillProperty, includeChildren: true) + spacing;
        }

        return height;
    }

    /// <summary>
    /// summary: 处理列表新增动作，给新技能槽填入一份可直接编辑的默认内容。
    /// param: list 当前 ReorderableList
    /// returns: 无
    /// </summary>
    private void AddSkillSlotElement(ReorderableList list)
    {
        skillSlotsProperty.arraySize++;
        SerializedProperty element = skillSlotsProperty.GetArrayElementAtIndex(skillSlotsProperty.arraySize - 1);
        InitializeDefaultSkillSlot(element);
        element.isExpanded = true;
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// summary: 为新增的技能槽初始化一份默认的召唤技能配置。
    /// param: element 当前新增的技能槽元素
    /// returns: 无
    /// </summary>
    private static void InitializeDefaultSkillSlot(SerializedProperty element)
    {
        if (element == null)
        {
            return;
        }

        SerializedProperty skillKindProperty = element.FindPropertyRelative("skillKind");
        SerializedProperty cooldownSecondsProperty = element.FindPropertyRelative("cooldownSeconds");
        SerializedProperty castRangeProperty = element.FindPropertyRelative("castRange");
        SerializedProperty summonSkillProperty = element.FindPropertyRelative("summonSkill");
        skillKindProperty.enumValueIndex = (int)EnemySkillKind.SummonEnemy;
        cooldownSecondsProperty.floatValue = 1f;
        castRangeProperty.floatValue = 12f;
        if (summonSkillProperty != null)
        {
            summonSkillProperty.FindPropertyRelative("summonedEnemyDefinition").objectReferenceValue = null;
            SerializedProperty summonedEnemyConfigProperty = summonSkillProperty.FindPropertyRelative("summonedEnemyConfig");
            if (summonedEnemyConfigProperty != null)
            {
                summonedEnemyConfigProperty.FindPropertyRelative("maxHealth").floatValue = 1f;
                summonedEnemyConfigProperty.FindPropertyRelative("moveSpeed").floatValue = 0f;
                summonedEnemyConfigProperty.FindPropertyRelative("attackRange").floatValue = 0f;
                summonedEnemyConfigProperty.FindPropertyRelative("attackCooldown").floatValue = 0f;
                summonedEnemyConfigProperty.FindPropertyRelative("attackDamage").floatValue = 0f;
                summonedEnemyConfigProperty.FindPropertyRelative("tokenDrops").ClearArray();
            }

            summonSkillProperty.FindPropertyRelative("summonCountPerCast").intValue = 1;
            summonSkillProperty.FindPropertyRelative("summonRadius").floatValue = 8f;
            summonSkillProperty.FindPropertyRelative("maxAliveSummons").intValue = 1;
        }
    }

    /// <summary>
    /// summary: 根据当前条目的技能类型拼出列表中显示的标签文本。
    /// param: index 当前条目的索引
    /// param: skillKindProperty 当前条目的技能类型属性
    /// returns: 用于折叠标题的文本
    /// </summary>
    private static string BuildSkillSlotLabel(int index, SerializedProperty skillKindProperty)
    {
        string skillKindName = GetEnumDisplayName(skillKindProperty);
        return $"{index + 1}. {skillKindName}";
    }

    /// <summary>
    /// summary: 读取枚举属性当前选中的显示名称。
    /// param: enumProperty 当前枚举属性
    /// returns: 当前枚举项的显示名称
    /// </summary>
    private static string GetEnumDisplayName(SerializedProperty enumProperty)
    {
        if (enumProperty == null || enumProperty.propertyType != SerializedPropertyType.Enum || enumProperty.enumDisplayNames.Length == 0)
        {
            return "Unknown";
        }

        int index = Mathf.Clamp(enumProperty.enumValueIndex, 0, enumProperty.enumDisplayNames.Length - 1);
        return enumProperty.enumDisplayNames[index];
    }
}
