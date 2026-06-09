using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Lilith/Enemy/AI Profile", fileName = "EnemyAIProfile")]
public sealed class EnemyAIProfile : ScriptableObject
{
    private const float MinimumTickIntervalSeconds = 0.02f;

    [SerializeField, Min(MinimumTickIntervalSeconds)] private float tickIntervalSeconds = 0.2f;
    [SerializeField, Min(0f)] private float perceptionRadius = 64f;
    [SerializeField] private List<EnemyAIActionDefinition> actions = new();
    [SerializeField] private EnemyAIActionDefinition fallbackAction = EnemyAIActionDefinition.CreateMovementFallback(EnemyMovementKind.ChaseTarget);

    public float TickIntervalSeconds => Mathf.Max(MinimumTickIntervalSeconds, tickIntervalSeconds);
    public float PerceptionRadius => Mathf.Max(0f, perceptionRadius);
    public IReadOnlyList<EnemyAIActionDefinition> Actions => actions != null
        ? actions
        : Array.Empty<EnemyAIActionDefinition>();
    public EnemyAIActionDefinition FallbackAction => fallbackAction.GetSanitized();

    private void OnValidate()
    {
        tickIntervalSeconds = Mathf.Max(MinimumTickIntervalSeconds, tickIntervalSeconds);
        perceptionRadius = Mathf.Max(0f, perceptionRadius);
        actions ??= new List<EnemyAIActionDefinition>();
        for (int i = 0; i < actions.Count; i++)
        {
            actions[i] = actions[i].GetSanitized();
        }

        fallbackAction = fallbackAction.GetSanitized();
        if (fallbackAction.ActionKind == EnemyAIActionKind.None)
        {
            fallbackAction = EnemyAIActionDefinition.CreateMovementFallback(EnemyMovementKind.ChaseTarget);
        }
    }

    public EnemyAIActionDefinition GetAction(int index)
    {
        if (actions == null || index < 0 || index >= actions.Count)
        {
            return default;
        }

        return actions[index].GetSanitized();
    }
}

[Serializable]
public struct EnemyAIActionDefinition
{
    [SerializeField] private string actionId;
    [SerializeField] private EnemyAIActionKind actionKind;
    [SerializeField] private EnemyMovementKind movementKind;
    [SerializeField] private EnemyAttackKind attackKind;
    [SerializeField] private EnemySkillKind skillKind;
    [SerializeField] private int skillSlotIndex;
    [SerializeField, Min(0f)] private float weight;
    [SerializeField, Min(0f)] private float cooldownSeconds;
    [SerializeField, Min(0f)] private float actionLockSeconds;
    [SerializeField] private List<EnemyAIConsiderationDefinition> considerations;

    public string ActionId => string.IsNullOrWhiteSpace(actionId) ? actionKind.ToString() : actionId.Trim();
    public EnemyAIActionKind ActionKind => actionKind;
    public EnemyMovementKind MovementKind => movementKind;
    public EnemyAttackKind AttackKind => attackKind;
    public EnemySkillKind SkillKind => skillKind;
    public int SkillSlotIndex => skillSlotIndex;
    public float Weight => SanitizePositive(weight, 1f);
    public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);
    public float ActionLockSeconds => Mathf.Max(0f, actionLockSeconds);
    public IReadOnlyList<EnemyAIConsiderationDefinition> Considerations => considerations != null
        ? considerations
        : Array.Empty<EnemyAIConsiderationDefinition>();

    public static EnemyAIActionDefinition CreateMovementFallback(EnemyMovementKind fallbackMovementKind)
    {
        return new EnemyAIActionDefinition
        {
            actionId = "fallback_move",
            actionKind = EnemyAIActionKind.Movement,
            movementKind = fallbackMovementKind,
            attackKind = EnemyAttackKind.None,
            skillKind = EnemySkillKind.None,
            skillSlotIndex = -1,
            weight = 1f,
            cooldownSeconds = 0f,
            actionLockSeconds = 0f,
            considerations = new List<EnemyAIConsiderationDefinition>(),
        };
    }

    public EnemyAIActionDefinition GetSanitized()
    {
        EnemyAIActionDefinition sanitized = this;
        sanitized.actionId = sanitized.actionId != null ? sanitized.actionId.Trim() : string.Empty;
        sanitized.skillSlotIndex = Mathf.Max(-1, sanitized.skillSlotIndex);
        sanitized.weight = SanitizePositive(sanitized.weight, 1f);
        sanitized.cooldownSeconds = Mathf.Max(0f, sanitized.cooldownSeconds);
        sanitized.actionLockSeconds = Mathf.Max(0f, sanitized.actionLockSeconds);
        sanitized.considerations ??= new List<EnemyAIConsiderationDefinition>();
        for (int i = 0; i < sanitized.considerations.Count; i++)
        {
            sanitized.considerations[i] = sanitized.considerations[i].GetSanitized();
        }

        return sanitized;
    }

    public float Evaluate(EnemyAIContext context)
    {
        EnemyAIActionDefinition sanitized = GetSanitized();
        if (sanitized.actionKind == EnemyAIActionKind.None)
        {
            return 0f;
        }

        float score = sanitized.weight;
        IReadOnlyList<EnemyAIConsiderationDefinition> resolvedConsiderations = sanitized.Considerations;
        for (int i = 0; i < resolvedConsiderations.Count; i++)
        {
            score *= resolvedConsiderations[i].Evaluate(context);
        }

        if (float.IsNaN(score) || float.IsInfinity(score))
        {
            return 0f;
        }

        return Mathf.Max(0f, score);
    }

    private static float SanitizePositive(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            return fallback;
        }

        return value;
    }
}

[Serializable]
public struct EnemyAIConsiderationDefinition
{
    [SerializeField] private EnemyAIConsiderationInput input;
    [SerializeField] private float minInput;
    [SerializeField] private float maxInput;
    [SerializeField] private bool invert;
    [SerializeField, Min(0f)] private float minScore;
    [SerializeField, Min(0f)] private float maxScore;

    public EnemyAIConsiderationInput Input => input;
    public float MinInput => minInput;
    public float MaxInput => maxInput;
    public bool Invert => invert;
    public float MinScore => Mathf.Max(0f, minScore);
    public float MaxScore => SanitizeScore(maxScore, 1f);

    public EnemyAIConsiderationDefinition GetSanitized()
    {
        EnemyAIConsiderationDefinition sanitized = this;
        sanitized.minScore = Mathf.Max(0f, sanitized.minScore);
        sanitized.maxScore = SanitizeScore(sanitized.maxScore, 1f);
        if (Mathf.Approximately(sanitized.minInput, sanitized.maxInput))
        {
            sanitized.maxInput = sanitized.minInput + 1f;
        }

        return sanitized;
    }

    public float Evaluate(EnemyAIContext context)
    {
        EnemyAIConsiderationDefinition sanitized = GetSanitized();
        float value = ResolveInputValue(sanitized.input, context);
        float denominator = sanitized.maxInput - sanitized.minInput;
        float normalized = Mathf.Approximately(denominator, 0f)
            ? 1f
            : Mathf.Clamp01((value - sanitized.minInput) / denominator);
        if (sanitized.invert)
        {
            normalized = 1f - normalized;
        }

        return Mathf.Lerp(sanitized.MinScore, sanitized.MaxScore, normalized);
    }

    private static float ResolveInputValue(EnemyAIConsiderationInput input, EnemyAIContext context)
    {
        return input switch
        {
            EnemyAIConsiderationInput.DistanceToTarget => context.DistanceToTarget,
            EnemyAIConsiderationInput.HealthRatio => context.HealthRatio,
            EnemyAIConsiderationInput.TargetInAttackRange => context.TargetInAttackRange ? 1f : 0f,
            EnemyAIConsiderationInput.NearbyAliveFriendCount => context.NearbyAliveFriendCount,
            EnemyAIConsiderationInput.CanMove => context.CanMove ? 1f : 0f,
            EnemyAIConsiderationInput.CanAct => context.CanAct ? 1f : 0f,
            EnemyAIConsiderationInput.TargetAlive => context.TargetAlive ? 1f : 0f,
            EnemyAIConsiderationInput.HasTarget => context.HasTarget ? 1f : 0f,
            EnemyAIConsiderationInput.Constant => 1f,
            _ => 0f,
        };
    }

    private static float SanitizeScore(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            return fallback;
        }

        return value;
    }
}

public readonly struct EnemyAIContext
{
    public EnemyAIContext(
        Enemy enemy,
        EnemyDefinition definition,
        EnemyStatusEffectController statusEffects,
        Transform targetPlayer,
        PlayerHealth targetPlayerHealth,
        Vector3 selfPosition,
        Vector3 targetPosition,
        float distanceToTarget,
        float healthRatio,
        bool hasTarget,
        bool targetAlive,
        bool targetInAttackRange,
        bool canMove,
        bool canAct,
        int nearbyAliveFriendCount)
    {
        Enemy = enemy;
        Definition = definition;
        StatusEffects = statusEffects;
        TargetPlayer = targetPlayer;
        TargetPlayerHealth = targetPlayerHealth;
        SelfPosition = selfPosition;
        TargetPosition = targetPosition;
        DistanceToTarget = distanceToTarget;
        HealthRatio = healthRatio;
        HasTarget = hasTarget;
        TargetAlive = targetAlive;
        TargetInAttackRange = targetInAttackRange;
        CanMove = canMove;
        CanAct = canAct;
        NearbyAliveFriendCount = nearbyAliveFriendCount;
    }

    public Enemy Enemy { get; }
    public EnemyDefinition Definition { get; }
    public EnemyStatusEffectController StatusEffects { get; }
    public Transform TargetPlayer { get; }
    public PlayerHealth TargetPlayerHealth { get; }
    public Vector3 SelfPosition { get; }
    public Vector3 TargetPosition { get; }
    public float DistanceToTarget { get; }
    public float HealthRatio { get; }
    public bool HasTarget { get; }
    public bool TargetAlive { get; }
    public bool TargetInAttackRange { get; }
    public bool CanMove { get; }
    public bool CanAct { get; }
    public int NearbyAliveFriendCount { get; }

    public static EnemyAIContext Create(
        Enemy enemy,
        EnemyStatusEffectController statusEffects,
        Transform targetPlayer,
        float perceptionRadius)
    {
        EnemyDefinition definition = enemy != null ? enemy.Definition : null;
        PlayerHealth targetHealth = ResolvePlayerHealth(targetPlayer);
        Vector3 selfPosition = enemy != null ? enemy.transform.position : Vector3.zero;
        Vector3 targetPosition = targetPlayer != null ? targetPlayer.position : selfPosition;
        bool hasTarget = targetPlayer != null;
        Vector3 targetOffset = targetPosition - selfPosition;
        targetOffset.y = 0f;
        float distanceToTarget = targetPlayer != null ? targetOffset.magnitude : float.PositiveInfinity;
        float maxHealth = enemy != null ? enemy.MaxHealth : 0f;
        float healthRatio = maxHealth > 0f && enemy != null ? Mathf.Clamp01(enemy.CurrentHealth / maxHealth) : 0f;
        bool targetAlive = hasTarget && (targetHealth == null || !targetHealth.IsDead);
        float attackRange = enemy != null ? Mathf.Max(0f, enemy.AttackRange) : 0f;
        bool targetInAttackRange = targetAlive && attackRange > 0f && distanceToTarget <= attackRange;
        bool canMove = statusEffects == null || statusEffects.CanMove;
        bool canAct = statusEffects == null || statusEffects.CanAct;
        int nearbyFriendCount = CountNearbyAliveFriends(enemy, selfPosition, perceptionRadius);
        return new EnemyAIContext(
            enemy,
            definition,
            statusEffects,
            targetPlayer,
            targetHealth,
            selfPosition,
            targetPosition,
            distanceToTarget,
            healthRatio,
            hasTarget,
            targetAlive,
            targetInAttackRange,
            canMove,
            canAct,
            nearbyFriendCount);
    }

    private static PlayerHealth ResolvePlayerHealth(Transform player)
    {
        if (player == null)
        {
            return null;
        }

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            return playerHealth;
        }

        return player.GetComponentInParent<PlayerHealth>();
    }

    private static int CountNearbyAliveFriends(Enemy self, Vector3 selfPosition, float perceptionRadius)
    {
        if (self == null || perceptionRadius <= 0f)
        {
            return 0;
        }

        float radiusSqr = perceptionRadius * perceptionRadius;
        Enemy[] enemies = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            Enemy candidate = enemies[i];
            if (candidate == null || candidate == self || candidate.IsDead)
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - selfPosition;
            offset.y = 0f;
            if (offset.sqrMagnitude <= radiusSqr)
            {
                count++;
            }
        }

        return count;
    }
}

public enum EnemyAIActionKind
{
    None = 0,
    Movement = 1,
    MeleeAttack = 2,
    RangedAttack = 3,
    ProximityExplosion = 4,
    CastSkill = 5,
}

public enum EnemyAIConsiderationInput
{
    Constant = 0,
    DistanceToTarget = 1,
    HealthRatio = 2,
    TargetInAttackRange = 3,
    NearbyAliveFriendCount = 4,
    CanMove = 5,
    CanAct = 6,
    TargetAlive = 7,
    HasTarget = 8,
}
