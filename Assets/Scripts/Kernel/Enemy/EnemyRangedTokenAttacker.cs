using System.Collections.Generic;
using Kernel.Bullet;
using UnityEngine;
using Vocalith.Logging;

/// <summary>
/// 让敌人在攻击距离内按冷却编译并发射配置好的词元子弹。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyRangedTokenAttacker : MonoBehaviour
{
    private const float MinimumAimDirectionSqrMagnitude = 0.0001f;

    [SerializeField] private Enemy enemyData;
    [SerializeField] private EnemyStatusEffectController statusEffects;
    [SerializeField] private EnemyAIController aiController;
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private PlayerHealth targetPlayerHealth;
    [SerializeField] private Transform bulletSpawnOrigin;
    [SerializeField] private Vector3 bulletSpawnLocalOffset = new(0f, 0f, 18f);

    private float nextAttackTime;
    private EnemyDefinition compiledDefinition;
    private CompiledSpellProgram compiledProgramCache;
    private bool hasLoggedCompileFailure;

    private void Awake()
    {
        TryResolveEnemyData();
        TryResolveStatusEffects();
        TryResolveAIController();
        TryResolveTargetPlayer();
        InvalidateCompiledProgram();
    }

    private void Update()
    {
        if (EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
        {
            return;
        }

        if (TryResolveAIController() && aiController.IsProfileActive)
        {
            return;
        }

        if (TryResolveStatusEffects() && !statusEffects.CanAct)
        {
            return;
        }

        TryPerformAttack(Time.time);
    }

    private void OnValidate()
    {
        TryResolveEnemyData();
        TryResolveStatusEffects();
        TryResolveAIController();
        TryResolveTargetPlayer();
        InvalidateCompiledProgram();
    }

    /// <summary>
    /// summary: 显式设置远程攻击使用的玩家目标，并同步解析生命组件。
    /// param: player 当前敌人应瞄准的玩家 Transform
    /// returns: 成功绑定到有效玩家目标时返回 true
    /// </summary>
    public bool TrySetTarget(Transform player)
    {
        if (player == null || IsOwnTransform(player))
        {
            return false;
        }

        targetPlayer = player;
        targetPlayerHealth = ResolvePlayerHealth(player);
        return targetPlayerHealth != null;
    }

    public bool CanExecuteAIAction(EnemyAIContext context, float currentTime)
    {
        if (context.Enemy != null && context.Enemy != enemyData)
        {
            enemyData = context.Enemy;
        }

        if (context.TargetPlayer != null)
        {
            TrySetTarget(context.TargetPlayer);
        }

        if (!context.CanAct || !context.TargetAlive || !context.TargetInAttackRange || currentTime < nextAttackTime)
        {
            return false;
        }

        if (!TryResolveEnemyData() || enemyData.Definition == null)
        {
            return false;
        }

        return enemyData.Definition.RangedBulletAttack.bulletPrefab != null;
    }

    public bool TryExecuteAIAction(EnemyAIContext context, float currentTime)
    {
        if (!CanExecuteAIAction(context, currentTime))
        {
            return false;
        }

        return TryPerformAttack(currentTime);
    }

    /// <summary>
    /// summary: 在满足攻击距离、冷却与编译条件时发射配置好的词元子弹。
    /// param: currentTime 当前逻辑时钟
    /// returns: 成功发射至少一发子弹时返回 true
    /// </summary>
    private bool TryPerformAttack(float currentTime)
    {
        if (TryResolveStatusEffects() && !statusEffects.CanAct)
        {
            return false;
        }

        if (currentTime < nextAttackTime || !TryResolveEnemyData() || !TryResolveTargetPlayer())
        {
            return false;
        }

        if (targetPlayerHealth == null || targetPlayerHealth.IsDead)
        {
            return false;
        }

        float attackRange = enemyData.AttackRange;
        if (attackRange <= 0f || !IsTargetWithinRange(attackRange))
        {
            return false;
        }

        EnemyDefinition definition = enemyData.Definition;
        EnemyDefinition.RangedBulletAttackDefinition rangedAttack = definition != null ? definition.RangedBulletAttack : default;
        if (rangedAttack.bulletPrefab == null || !TryGetCompiledProgram(definition, rangedAttack, out CompiledSpellProgram compiledProgram))
        {
            return false;
        }

        if (!TryGetShotDirection(out Vector3 spawnPosition, out Vector3 shotDirection))
        {
            return false;
        }

        if (EmitConfiguredProgram(
                rangedAttack.bulletPrefab,
                transform,
                spawnPosition,
                shotDirection,
                compiledProgram,
                rangedAttack,
                rangedAttack.targetPolicy) <= 0)
        {
            return false;
        }

        nextAttackTime = currentTime + Mathf.Max(0f, enemyData.AttackCooldown);
        return true;
    }

    /// <summary>
    /// summary: 当敌人本身声明了明确战斗数值时，以派生 projectile node 的方式覆盖外层子弹的伤害与可达距离。
    /// param: compiledProgram 当前准备发射的法术程序
    /// returns: 实际成功生成的子弹数量
    /// </summary>
    private int EmitConfiguredProgram(
        CharBullet bulletPrefab,
        Transform owner,
        Vector3 spawnPosition,
        Vector3 shotDirection,
        CompiledSpellProgram compiledProgram,
        EnemyDefinition.RangedBulletAttackDefinition rangedAttack,
        BulletTargetPolicy targetPolicy)
    {
        if (compiledProgram?.PrimaryCastBlock == null)
        {
            return 0;
        }

        IReadOnlyList<SpellProjectileNode> projectiles = compiledProgram.PrimaryCastBlock.Projectiles;
        int emittedCount = 0;
        int activationCastCount = rangedAttack.spellBook != null ? rangedAttack.spellBook.CastsPerActivation : 1;
        float activationSpreadAngleStep = rangedAttack.spellBook != null ? rangedAttack.spellBook.ActivationSpreadAngleStep : 0f;
        int castCount = Mathf.Max(1, activationCastCount);
        for (int castIndex = 0; castIndex < castCount; castIndex++)
        {
            Vector3 castDirection = AttackProjectileEmitter.ResolveActivationCastDirection(
                shotDirection,
                castIndex,
                castCount,
                activationSpreadAngleStep);
            for (int i = 0; i < projectiles.Count; i++)
            {
                SpellProjectileNode runtimeProjectile = CreateConfiguredRuntimeProjectile(projectiles[i], rangedAttack);
                emittedCount += AttackProjectileEmitter.Emit(
                    bulletPrefab,
                    owner,
                    spawnPosition,
                    castDirection,
                    runtimeProjectile,
                    targetPolicy,
                    null,
                    null);
            }
        }

        return emittedCount;
    }

    private SpellProjectileNode CreateConfiguredRuntimeProjectile(
        SpellProjectileNode projectile,
        EnemyDefinition.RangedBulletAttackDefinition rangedAttack)
    {
        if (projectile == null || !TryResolveEnemyData())
        {
            return null;
        }

        float configuredAttackRange = Mathf.Max(0f, enemyData.AttackRange);
        float configuredDamage = enemyData.AttackDamage;
        float configuredProjectileSpeedMultiplier = rangedAttack.projectileSpeedMultiplier;
        if (configuredDamage <= 0f && configuredAttackRange <= 0f && configuredProjectileSpeedMultiplier <= 0f)
        {
            return projectile;
        }

        AttackSpec shotSpec = projectile.AttackSpec;
        if (configuredProjectileSpeedMultiplier > 0f)
        {
            shotSpec.projectileSpeed = Mathf.Max(0f, shotSpec.projectileSpeed * configuredProjectileSpeedMultiplier);
        }

        if (configuredDamage > 0f)
        {
            shotSpec.damage = configuredDamage;
        }

        if (configuredAttackRange > 0f)
        {
            float requiredTravelDistance = ResolveRequiredProjectileTravelDistance(configuredAttackRange);
            shotSpec.maxTravelDistance = Mathf.Max(shotSpec.maxTravelDistance, requiredTravelDistance);
            if (shotSpec.projectileSpeed > 0f)
            {
                float requiredLifetime = requiredTravelDistance / shotSpec.projectileSpeed;
                shotSpec.maxLifetime = Mathf.Max(shotSpec.maxLifetime, requiredLifetime);
            }
        }

        return SpellProjectileNode.CreateWithAttackSpecOverride(projectile, shotSpec);
    }

    /// <summary>
    /// summary: 根据敌人的攻击距离和出生偏移，估算子弹至少需要支持的飞行距离。
    /// param: attackRange 当前敌人声明的攻击距离
    /// returns: 为了命中攻击距离边缘目标而推荐使用的最小飞行距离
    /// </summary>
    private float ResolveRequiredProjectileTravelDistance(float attackRange)
    {
        Transform spawnRoot = bulletSpawnOrigin != null ? bulletSpawnOrigin : transform;
        Vector3 worldSpawnOffset = spawnRoot.TransformVector(bulletSpawnLocalOffset);
        float planarSpawnOffset = new Vector2(worldSpawnOffset.x, worldSpawnOffset.z).magnitude;
        return Mathf.Max(attackRange, attackRange + planarSpawnOffset);
    }

    private bool TryGetCompiledProgram(
        EnemyDefinition definition,
        EnemyDefinition.RangedBulletAttackDefinition rangedAttack,
        out CompiledSpellProgram compiledProgram)
    {
        compiledProgram = null;
        if (definition == null)
        {
            return false;
        }

        if (compiledProgramCache == null || compiledDefinition != definition)
        {
            List<PlaceableTokenData> executionItems = rangedAttack.BuildExecutionItems();
            if (executionItems.Count <= 0)
            {
                return false;
            }

            compiledDefinition = definition;
            compiledProgramCache = SpellProgramCompiler.Compile(executionItems, rangedAttack.spellBook);
            hasLoggedCompileFailure = false;
        }

        List<PlaceableTokenData> activationItems = rangedAttack.BuildExecutionItems();
        compiledProgram = SpellProgramCompiler.ContainsRandomModifier(activationItems)
            ? SpellProgramCompiler.CompileForActivation(activationItems, rangedAttack.spellBook)
            : compiledProgramCache;
        if (compiledProgram != null && compiledProgram.CanCast)
        {
            hasLoggedCompileFailure = false;
            return true;
        }

        if (!hasLoggedCompileFailure)
        {
            hasLoggedCompileFailure = true;
            GameDebug.LogWarning($"[EnemyRangedTokenAttacker] Enemy '{name}' failed to compile its ranged token formula.");
        }

        return false;
    }

    /// <summary>
    /// summary: 计算当前远程攻击的发射点与朝向。
    /// param: spawnPosition 输出的子弹出生点
    /// param: shotDirection 输出的平面发射方向
    /// returns: 成功得到有效平面方向时返回 true
    /// </summary>
    private bool TryGetShotDirection(out Vector3 spawnPosition, out Vector3 shotDirection)
    {
        spawnPosition = GetBulletSpawnPosition();
        shotDirection = Vector3.zero;
        Vector3 targetOffset = targetPlayer.position - spawnPosition;
        targetOffset.y = 0f;
        if (targetOffset.sqrMagnitude <= MinimumAimDirectionSqrMagnitude)
        {
            return false;
        }

        shotDirection = targetOffset.normalized;
        return true;
    }

    /// <summary>
    /// summary: 计算当前远程攻击实际使用的世界发射点。
    /// param: 无
    /// returns: 子弹出生世界坐标
    /// </summary>
    private Vector3 GetBulletSpawnPosition()
    {
        Transform spawnRoot = bulletSpawnOrigin != null ? bulletSpawnOrigin : transform;
        return spawnRoot.TransformPoint(bulletSpawnLocalOffset);
    }

    /// <summary>
    /// summary: 判断当前玩家是否仍处于敌人的远程攻击距离内。
    /// param: attackRange 当前敌人声明的攻击距离
    /// returns: 平面距离不大于攻击距离时返回 true
    /// </summary>
    private bool IsTargetWithinRange(float attackRange)
    {
        Vector3 offset = targetPlayer.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= attackRange * attackRange;
    }

    private void InvalidateCompiledProgram()
    {
        compiledDefinition = null;
        compiledProgramCache = null;
        hasLoggedCompileFailure = false;
    }

    private bool TryResolveEnemyData()
    {
        if (enemyData != null && enemyData.transform == transform)
        {
            return true;
        }

        enemyData = null;
        return TryGetComponent(out enemyData);
    }

    /// <summary>
    /// summary: 解析当前敌人根节点上的状态效果控制器，供眩晕阻断远程攻击使用。
    /// param: 无
    /// returns: 成功拿到状态控制器时返回 true
    /// </summary>
    private bool TryResolveStatusEffects()
    {
        if (statusEffects != null && statusEffects.transform == transform)
        {
            return true;
        }

        statusEffects = null;
        return TryGetComponent(out statusEffects);
    }

    private bool TryResolveAIController()
    {
        if (aiController != null && aiController.transform == transform)
        {
            return true;
        }

        aiController = null;
        return TryGetComponent(out aiController);
    }

    private bool TryResolveTargetPlayer()
    {
        if (targetPlayer != null && !IsOwnTransform(targetPlayer))
        {
            targetPlayerHealth = ResolvePlayerHealth(targetPlayer);
            return targetPlayerHealth != null;
        }

        PlayerPlaneMovement playerMovement = FindFirstObjectByType<PlayerPlaneMovement>();
        if (playerMovement == null)
        {
            return false;
        }

        targetPlayer = playerMovement.transform;
        targetPlayerHealth = ResolvePlayerHealth(targetPlayer);
        return targetPlayerHealth != null;
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

    private bool IsOwnTransform(Transform candidate)
    {
        return candidate != null && (candidate == transform || candidate.IsChildOf(transform));
    }
}
