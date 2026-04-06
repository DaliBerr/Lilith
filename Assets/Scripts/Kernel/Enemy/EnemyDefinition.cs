using System;
using UnityEngine;

/// <summary>
/// 定义一种敌人的 prefab、行为开关和视觉表现。
/// </summary>
[CreateAssetMenu(menuName = "Lilith/Enemy/Enemy Definition", fileName = "EnemyDefinition")]
public sealed class EnemyDefinition : ScriptableObject
{
    [Serializable]
    public struct EnemyVisualDefinition
    {
        public string glyphText;
        public Color glyphColor;
        public Sprite runeBaseSprite;
        public Color runeBaseTint;
        public Sprite groundShadowSprite;
        public Color groundShadowTint;

        /// <summary>
        /// summary: 规范化当前视觉定义中的文本与颜色字段。
        /// param: 无
        /// returns: 经过规范化后的视觉定义副本
        /// </summary>
        public EnemyVisualDefinition GetSanitized()
        {
            EnemyVisualDefinition sanitized = this;
            sanitized.glyphText ??= string.Empty;
            sanitized.glyphColor.a = Mathf.Clamp01(sanitized.glyphColor.a);
            sanitized.runeBaseTint.a = Mathf.Clamp01(sanitized.runeBaseTint.a);
            sanitized.groundShadowTint.a = Mathf.Clamp01(sanitized.groundShadowTint.a);
            return sanitized;
        }
    }

    [SerializeField] private string enemyId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private GameObject runtimePrefab;
    [SerializeField] private EnemyMovementKind movementKind = EnemyMovementKind.ChaseTarget;
    [SerializeField] private EnemyAttackKind attackKind = EnemyAttackKind.MeleeContact;
    [SerializeField] private EnemyVisualDefinition visual = new()
    {
        glyphText = string.Empty,
        glyphColor = Color.white,
        runeBaseTint = new Color(0.92f, 0.94f, 0.98f, 0.45f),
        groundShadowTint = new Color(0f, 0f, 0f, 0.28f),
    };

    public string EnemyId => string.IsNullOrWhiteSpace(enemyId) ? name : enemyId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? EnemyId : displayName.Trim();
    public GameObject RuntimePrefab => runtimePrefab;
    public EnemyMovementKind MovementKind => movementKind;
    public EnemyAttackKind AttackKind => attackKind;
    public EnemyVisualDefinition Visual => visual;

    private void OnValidate()
    {
        enemyId = enemyId != null ? enemyId.Trim() : string.Empty;
        displayName = displayName != null ? displayName.Trim() : string.Empty;
        visual = visual.GetSanitized();
    }
}

public enum EnemyMovementKind
{
    None = 0,
    ChaseTarget = 1,
}

public enum EnemyAttackKind
{
    None = 0,
    MeleeContact = 1,
}
