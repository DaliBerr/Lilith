using TMPro;
using UnityEngine;

/// <summary>
/// 把敌人定义资产应用到运行时 prefab 壳上的既有行为与视觉组件。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyDefinitionBinder : MonoBehaviour
{
    [SerializeField] private EnemyDefinition definition;
    [SerializeField] private Enemy enemyData;
    [SerializeField] private CharEnemyMovement movement;
    [SerializeField] private EnemyMeleeAttacker meleeAttacker;
    [SerializeField] private CharGlyphPresenter glyphPresenter;
    [SerializeField] private CharEnemyVisualPresenter visualPresenter;

    public EnemyDefinition Definition => definition;

    private void Awake()
    {
        TryCacheBindings();
        if (definition != null)
        {
            ApplyDefinition(definition);
        }
    }

    private void Reset()
    {
        TryCacheBindings(overwriteExisting: true);
    }

    private void OnValidate()
    {
        TryCacheBindings();
        if (definition != null)
        {
            ApplyDefinition(definition);
        }
    }

    /// <summary>
    /// summary: 解析并缓存当前敌人壳上的运行时组件引用。
    /// param: overwriteExisting 为 true 时强制重新解析当前所有绑定
    /// returns: 成功拿到 Enemy 根数据组件时返回 true
    /// </summary>
    public bool TryCacheBindings(bool overwriteExisting = false)
    {
        if (overwriteExisting || enemyData == null || enemyData.transform != transform)
        {
            enemyData = GetComponent<Enemy>();
        }

        if (overwriteExisting || movement == null || movement.transform != transform)
        {
            movement = GetComponent<CharEnemyMovement>();
        }

        if (overwriteExisting || meleeAttacker == null || meleeAttacker.transform != transform)
        {
            meleeAttacker = GetComponent<EnemyMeleeAttacker>();
        }

        if (overwriteExisting || glyphPresenter == null || glyphPresenter.transform != transform)
        {
            glyphPresenter = GetComponent<CharGlyphPresenter>();
        }

        if (overwriteExisting || visualPresenter == null || visualPresenter.transform != transform)
        {
            visualPresenter = GetComponent<CharEnemyVisualPresenter>();
        }

        return enemyData != null;
    }

    /// <summary>
    /// summary: 把给定敌人定义写入当前 prefab 壳，并同步行为开关和视觉表现。
    /// param: nextDefinition 当前实例应绑定的敌人定义
    /// returns: 成功完成定义绑定时返回 true
    /// </summary>
    public bool ApplyDefinition(EnemyDefinition nextDefinition)
    {
        if (nextDefinition == null || !TryCacheBindings())
        {
            return false;
        }

        definition = nextDefinition;
        if (!enemyData.TryBindDefinition(definition))
        {
            return false;
        }

        if (!ApplyMovementKind(definition.MovementKind) || !ApplyAttackKind(definition.AttackKind))
        {
            return false;
        }

        ApplyVisuals(definition.Visual);
        return true;
    }

    private bool ApplyMovementKind(EnemyMovementKind movementKind)
    {
        bool shouldEnableMovement = movementKind == EnemyMovementKind.ChaseTarget;
        if (movement == null)
        {
            return !shouldEnableMovement;
        }

        movement.enabled = shouldEnableMovement;
        return true;
    }

    private bool ApplyAttackKind(EnemyAttackKind attackKind)
    {
        bool shouldEnableAttack = attackKind == EnemyAttackKind.MeleeContact;
        if (meleeAttacker == null)
        {
            return !shouldEnableAttack;
        }

        meleeAttacker.enabled = shouldEnableAttack;
        return true;
    }

    private void ApplyVisuals(EnemyDefinition.EnemyVisualDefinition visual)
    {
        if (glyphPresenter != null)
        {
            glyphPresenter.SetDisplayText(visual.glyphText);
            TMP_Text glyphText = glyphPresenter.GlyphText;
            if (glyphText != null)
            {
                glyphText.color = visual.glyphColor;
            }
        }

        if (visualPresenter != null)
        {
            visualPresenter.ApplyVisualDefinition(visual);
        }
    }
}
