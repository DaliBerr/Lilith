using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Vocalith.Localization;
using Vocalith.Logging;

namespace Kernel.Upgrade
{
    /// <summary>
    /// 负责加载永久升级目录、读写玩家升级等级，并暴露永久升级带来的运行时数值加成。
    /// </summary>
    [DefaultExecutionOrder(-930)]
    [DisallowMultipleComponent]
    public sealed class PermanentUpgradeService : MonoBehaviour
    {
        private const string DefaultCatalogAddress = "Assets/Data/Upgrades/PermanentUpgradeCatalog";
        private const string UpgradeLifetimeStatPrefix = "upgrade.";
        private const float DefaultCanvasWidth = 1800f;
        private const float DefaultCanvasHeight = 1200f;
        private const float DefaultNodeWidth = 100f;
        private const float DefaultNodeHeight = 100f;
        private const float DefaultBorderWidth = 4f;
        private const float DefaultEdgeWidth = 8f;
        private const string DefaultNodeBackgroundColor = "#1F2937";
        private const string DefaultNodeBorderColor = "#66E35F";
        private const string DefaultEdgeColor = "#66E35F";

        private static readonly IReadOnlyList<PermanentUpgradeSectionData> EmptySections = Array.Empty<PermanentUpgradeSectionData>();
        private static readonly IReadOnlyList<PermanentUpgradeEdgeData> EmptyEdges = Array.Empty<PermanentUpgradeEdgeData>();

        private PermanentUpgradeCatalogData currentCatalog;
        private readonly Dictionary<string, PermanentUpgradeEntryData> entryById = new(StringComparer.Ordinal);
        private readonly Dictionary<PermanentUpgradeStatId, PermanentUpgradeStatModifiers> cachedModifiersByStat = new();
        private bool hasCatalogLoaded;
        private bool isCatalogLoading;
        private AsyncOperationHandle<TextAsset> activeCatalogHandle;
        private bool hasActiveCatalogHandle;

        public static PermanentUpgradeService Instance { get; private set; }

        [SerializeField] private string catalogAddress = DefaultCatalogAddress;

        public bool HasCatalogLoaded => hasCatalogLoaded && currentCatalog != null;

        /// <summary>
        /// summary: 清空永久升级服务的静态实例，避免域重载后残留旧引用。
        /// param: 无
        /// returns: 无
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        /// <summary>
        /// summary: 在首个场景加载前确保永久升级服务实例存在，并尽早开始预热目录。
        /// param: 无
        /// returns: 无
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            GetOrCreateInstance();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine(LoadCatalogIfNeededCo());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// summary: 获取当前可用的永久升级服务实例；若场景中缺失则自动创建。
        /// param: 无
        /// returns: 可用的 PermanentUpgradeService 实例
        /// </summary>
        public static PermanentUpgradeService GetOrCreateInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            PermanentUpgradeService existing = FindFirstObjectByType<PermanentUpgradeService>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject bootstrapObject = new(nameof(PermanentUpgradeService));
            return bootstrapObject.AddComponent<PermanentUpgradeService>();
        }

        /// <summary>
        /// summary: 解析并校验永久升级目录 JSON 文本；只要存在非法条目则整体拒绝。
        /// param name="jsonText": 需要解析的原始 JSON 文本
        /// param name="catalog": 输出的已规范化目录数据
        /// param name="errorMessage": 解析或校验失败时的错误原因
        /// returns: 解析和校验都成功时返回 true
        /// </summary>
        public static bool TryDeserializeCatalogJson(string jsonText, out PermanentUpgradeCatalogData catalog, out string errorMessage)
        {
            catalog = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                errorMessage = "Permanent upgrade catalog JSON is empty.";
                return false;
            }

            PermanentUpgradeCatalogData rawCatalog;
            try
            {
                rawCatalog = LocalizedJsonUtility.DeserializeLocalized<PermanentUpgradeCatalogData>(
                    jsonText,
                    "PermanentUpgradeCatalog",
                    settings: null);
            }
            catch (JsonException exception)
            {
                errorMessage = $"Permanent upgrade catalog JSON is invalid: {exception.Message}";
                return false;
            }

            return TryBuildValidatedCatalog(rawCatalog, out catalog, out errorMessage);
        }

        /// <summary>
        /// summary: 让服务直接采用一份已解析的目录数据；测试和运行时加载共用这条校验路径。
        /// param name="catalog": 候选目录数据
        /// param name="errorMessage": 校验失败时的错误原因
        /// returns: 成功采用目录数据时返回 true
        /// </summary>
        public bool TryUseCatalog(PermanentUpgradeCatalogData catalog, out string errorMessage)
        {
            errorMessage = null;
            if (!TryBuildValidatedCatalog(catalog, out PermanentUpgradeCatalogData validatedCatalog, out errorMessage))
            {
                return false;
            }

            ApplyCatalog(validatedCatalog);
            return true;
        }

        /// <summary>
        /// summary: 预加载永久升级目录；若已经加载完成或正在加载则直接复用当前状态。
        /// param: 无
        /// returns: 可供协程等待的枚举器
        /// </summary>
        public IEnumerator LoadCatalogIfNeededCo()
        {
            if (HasCatalogLoaded)
            {
                yield break;
            }

            while (isCatalogLoading)
            {
                yield return null;
            }

            if (HasCatalogLoaded)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(catalogAddress))
            {
                GameDebug.LogError("[PermanentUpgradeService] Catalog address is empty.");
                yield break;
            }

            isCatalogLoading = true;
            activeCatalogHandle = Addressables.LoadAssetAsync<TextAsset>(catalogAddress.Trim());
            hasActiveCatalogHandle = true;
            yield return activeCatalogHandle;

            try
            {
                if (HasCatalogLoaded || !hasActiveCatalogHandle)
                {
                    yield break;
                }

                if (activeCatalogHandle.Status != AsyncOperationStatus.Succeeded || activeCatalogHandle.Result == null)
                {
                    GameDebug.LogError($"[PermanentUpgradeService] Failed to load upgrade catalog at '{catalogAddress}'.");
                    yield break;
                }

                if (!TryDeserializeCatalogJson(activeCatalogHandle.Result.text, out PermanentUpgradeCatalogData catalog, out string errorMessage))
                {
                    GameDebug.LogError($"[PermanentUpgradeService] {errorMessage}");
                    yield break;
                }

                ApplyCatalog(catalog);
            }
            finally
            {
                ReleaseActiveCatalogHandle();
                isCatalogLoading = false;
            }
        }

        /// <summary>
        /// summary: 返回当前目录中的 section 列表；目录尚未就绪时会尝试同步加载一次。
        /// param: 无
        /// returns: 当前可展示的 section 只读列表
        /// </summary>
        public IReadOnlyList<PermanentUpgradeSectionData> GetSections()
        {
            EnsureCatalogLoaded();
            return currentCatalog != null ? currentCatalog.Sections : EmptySections;
        }

        /// <summary>
        /// summary: 返回当前科技树画布尺寸；旧 JSON 未声明时使用默认尺寸。
        /// param: 无
        /// returns: 当前目录声明的科技树画布尺寸
        /// </summary>
        public PermanentUpgradeVector2Data GetCanvasSize()
        {
            EnsureCatalogLoaded();
            return currentCatalog?.CanvasSize ?? CreateVector2(DefaultCanvasWidth, DefaultCanvasHeight);
        }

        /// <summary>
        /// summary: 返回当前科技树的连线列表；旧 JSON 未声明时返回空列表。
        /// param: 无
        /// returns: 当前目录声明的连线只读列表
        /// </summary>
        public IReadOnlyList<PermanentUpgradeEdgeData> GetEdges()
        {
            EnsureCatalogLoaded();
            return currentCatalog != null ? currentCatalog.Edges : EmptyEdges;
        }

        /// <summary>
        /// summary: 查询一个升级条目是否存在于当前目录中。
        /// param name="entryId": 需要查询的条目 ID
        /// param name="entry": 输出命中的条目数据
        /// returns: 条目存在时返回 true
        /// </summary>
        public bool TryGetEntry(string entryId, out PermanentUpgradeEntryData entry)
        {
            entry = null;
            if (!EnsureCatalogLoaded())
            {
                return false;
            }

            string sanitizedEntryId = SanitizeIdentifier(entryId);
            return !string.IsNullOrEmpty(sanitizedEntryId) && entryById.TryGetValue(sanitizedEntryId, out entry);
        }

        /// <summary>
        /// summary: 读取指定永久升级条目的当前购买等级。
        /// param name="entryId": 需要查询的条目 ID
        /// returns: 当前 profile 中记录的购买等级；无效条目返回 0
        /// </summary>
        public int GetPurchasedLevel(string entryId)
        {
            string sanitizedEntryId = SanitizeIdentifier(entryId);
            if (string.IsNullOrEmpty(sanitizedEntryId))
            {
                return 0;
            }

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            return saveService != null ? Mathf.Max(0, saveService.GetLifetimeStat(BuildLifetimeStatKey(sanitizedEntryId))) : 0;
        }

        /// <summary>
        /// summary: 判断指定条目的所有前置升级是否已经至少购买 1 级。
        /// param name="entryId": 需要查询的条目 ID
        /// returns: 条目存在且所有前置满足时返回 true；条目无效或目录未加载时返回 false
        /// </summary>
        public bool HasPrerequisitesMet(string entryId)
        {
            if (!TryGetEntry(entryId, out PermanentUpgradeEntryData entry))
            {
                return false;
            }

            return HasPurchasedRequiredEntries(entry);
        }

        /// <summary>
        /// summary: 返回当前永久升级对伤害编译的总倍率；未加载目录时会同步兜底加载一次。
        /// param: 无
        /// returns: 当前应应用到编译后伤害上的总倍率
        /// </summary>
        public float GetDamageMultiplier()
        {
            return GetStatMultiplier(PermanentUpgradeStatId.OutgoingDamage);
        }

        /// <summary>
        /// summary: 返回指定玩家数值接口当前累计的永久升级 modifier。
        /// param name="statId": 需要查询的数值接口
        /// returns: 当前已购买升级带来的聚合 modifier；没有加成时返回 Identity
        /// </summary>
        public PermanentUpgradeStatModifiers GetStatModifiers(PermanentUpgradeStatId statId)
        {
            EnsureCatalogLoaded();
            return cachedModifiersByStat.TryGetValue(statId, out PermanentUpgradeStatModifiers modifiers)
                ? modifiers
                : PermanentUpgradeStatModifiers.Identity;
        }

        /// <summary>
        /// summary: 按固定公式解析指定玩家数值的永久升级后结果。
        /// param name="statId": 需要解析的数值接口
        /// param name="baseValue": 未受永久升级影响前的基础值
        /// returns: 应用于永久升级后的结果，公式为 (base + flat) * (1 + additiveMultiplier) * multiplicativeMultiplier
        /// </summary>
        public float ResolveStat(PermanentUpgradeStatId statId, float baseValue)
        {
            return GetStatModifiers(statId).Resolve(baseValue);
        }

        /// <summary>
        /// summary: 返回指定玩家数值在基础值为 1 时的倍率结果。
        /// param name="statId": 需要查询的数值接口
        /// returns: 当前永久升级对该数值的倍率式结果
        /// </summary>
        public float GetStatMultiplier(PermanentUpgradeStatId statId)
        {
            return Mathf.Max(0f, ResolveStat(statId, 1f));
        }

        /// <summary>
        /// summary: 静态读取当前永久升级对伤害编译的总倍率，供攻击编译链直接调用。
        /// param: 无
        /// returns: 当前应应用到编译后伤害上的总倍率
        /// </summary>
        public static float GetCurrentDamageMultiplier()
        {
            PermanentUpgradeService service = GetOrCreateInstance();
            return service != null ? service.GetDamageMultiplier() : 1f;
        }

        /// <summary>
        /// summary: 尝试购买一个永久升级条目；成功时会扣除残卷、写入存档并刷新缓存倍率。
        /// param name="entryId": 需要购买的条目 ID
        /// param name="result": 输出本次购买的结果
        /// returns: 成功完成购买时返回 true
        /// </summary>
        public bool TryPurchase(string entryId, out PermanentUpgradePurchaseResult result)
        {
            result = new PermanentUpgradePurchaseResult(
                succeeded: false,
                failureReason: PermanentUpgradePurchaseFailureReason.CatalogNotReady,
                entryId: entryId,
                newLevel: 0,
                remainingRemnants: PlayerRemnantWallet.GetCurrentRemnants(),
                message: LocalizationManager.TranslateOrDefault(
                    "ui.upgrade.purchase.catalog_not_ready",
                    "升级目录尚未加载完成。"));

            if (!EnsureCatalogLoaded())
            {
                return false;
            }

            if (!TryGetEntry(entryId, out PermanentUpgradeEntryData entry))
            {
                result = new PermanentUpgradePurchaseResult(
                    succeeded: false,
                    failureReason: PermanentUpgradePurchaseFailureReason.InvalidEntry,
                    entryId: entryId,
                    newLevel: 0,
                    remainingRemnants: PlayerRemnantWallet.GetCurrentRemnants(),
                    message: LocalizationManager.TranslateOrDefault(
                        "ui.upgrade.purchase.invalid_entry",
                        "未找到对应的永久升级条目。"));
                return false;
            }

            int currentLevel = GetPurchasedLevel(entry.Id);
            if (currentLevel >= entry.MaxLevel)
            {
                result = new PermanentUpgradePurchaseResult(
                    succeeded: false,
                    failureReason: PermanentUpgradePurchaseFailureReason.MaxLevelReached,
                    entryId: entry.Id,
                    newLevel: currentLevel,
                    remainingRemnants: PlayerRemnantWallet.GetCurrentRemnants(),
                    message: LocalizationManager.TranslateOrDefault(
                        "ui.upgrade.purchase.max_level",
                        "该升级已经达到上限。"));
                return false;
            }

            if (!HasPurchasedRequiredEntries(entry))
            {
                result = new PermanentUpgradePurchaseResult(
                    succeeded: false,
                    failureReason: PermanentUpgradePurchaseFailureReason.PrerequisiteMissing,
                    entryId: entry.Id,
                    newLevel: currentLevel,
                    remainingRemnants: PlayerRemnantWallet.GetCurrentRemnants(),
                    message: LocalizationManager.TranslateOrDefault(
                        "ui.upgrade.purchase.prerequisite_missing",
                        "前置升级尚未解锁。"));
                return false;
            }

            int currentRemnants = PlayerRemnantWallet.GetCurrentRemnants();
            if (entry.CostRemnants > currentRemnants)
            {
                result = new PermanentUpgradePurchaseResult(
                    succeeded: false,
                    failureReason: PermanentUpgradePurchaseFailureReason.InsufficientRemnants,
                    entryId: entry.Id,
                    newLevel: currentLevel,
                    remainingRemnants: currentRemnants,
                    message: LocalizationManager.TranslateOrDefault(
                        "ui.upgrade.purchase.insufficient_remnants",
                        "残卷不足，无法购买该升级。"));
                return false;
            }

            bool spentRemnants = false;
            int remainingRemnants = currentRemnants;
            if (entry.CostRemnants > 0)
            {
                if (!PlayerRemnantWallet.TrySpendCurrentRemnants(entry.CostRemnants, out remainingRemnants))
                {
                    result = new PermanentUpgradePurchaseResult(
                        succeeded: false,
                        failureReason: PermanentUpgradePurchaseFailureReason.InsufficientRemnants,
                        entryId: entry.Id,
                        newLevel: currentLevel,
                        remainingRemnants: currentRemnants,
                        message: LocalizationManager.TranslateOrDefault(
                            "ui.upgrade.purchase.insufficient_remnants",
                            "残卷不足，无法购买该升级。"));
                    return false;
                }

                spentRemnants = true;
            }

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            if (saveService == null || !saveService.IncrementLifetimeStat(BuildLifetimeStatKey(entry.Id), 1, out int newLevel))
            {
                if (spentRemnants)
                {
                    PlayerRemnantWallet.TryAddCurrentRemnants(entry.CostRemnants, out remainingRemnants);
                }

                result = new PermanentUpgradePurchaseResult(
                    succeeded: false,
                    failureReason: PermanentUpgradePurchaseFailureReason.SaveUnavailable,
                    entryId: entry.Id,
                    newLevel: currentLevel,
                    remainingRemnants: PlayerRemnantWallet.GetCurrentRemnants(),
                    message: LocalizationManager.TranslateOrDefault(
                        "ui.upgrade.purchase.save_unavailable",
                        "存档服务不可用，升级购买失败。"));
                return false;
            }

            RecalculateCachedStatModifiers();
            result = new PermanentUpgradePurchaseResult(
                succeeded: true,
                failureReason: PermanentUpgradePurchaseFailureReason.None,
                entryId: entry.Id,
                newLevel: newLevel,
                remainingRemnants: remainingRemnants,
                message: LocalizationManager.TranslateOrDefault(
                    "ui.upgrade.purchase.success",
                    "购买成功。"));
            return true;
        }

        /// <summary>
        /// summary: 把一个条目 ID 转成永久档里统一使用的 LifetimeStats 键名。
        /// param name="entryId": 原始条目 ID
        /// returns: 形如 upgrade.xxx 的稳定键名；无效输入返回空字符串
        /// </summary>
        public static string BuildLifetimeStatKey(string entryId)
        {
            string sanitizedEntryId = SanitizeIdentifier(entryId);
            return string.IsNullOrEmpty(sanitizedEntryId)
                ? string.Empty
                : $"{UpgradeLifetimeStatPrefix}{sanitizedEntryId}";
        }

        private bool EnsureCatalogLoaded()
        {
            if (HasCatalogLoaded)
            {
                return true;
            }

            if (isCatalogLoading && hasActiveCatalogHandle)
            {
                try
                {
                    TextAsset pendingTextAsset = activeCatalogHandle.WaitForCompletion();
                    if (activeCatalogHandle.Status != AsyncOperationStatus.Succeeded || pendingTextAsset == null)
                    {
                        GameDebug.LogError($"[PermanentUpgradeService] Failed to synchronously finish loading upgrade catalog at '{catalogAddress}'.");
                        return false;
                    }

                    if (!TryDeserializeCatalogJson(pendingTextAsset.text, out PermanentUpgradeCatalogData pendingCatalog, out string pendingErrorMessage))
                    {
                        GameDebug.LogError($"[PermanentUpgradeService] {pendingErrorMessage}");
                        return false;
                    }

                    ApplyCatalog(pendingCatalog);
                    return true;
                }
                finally
                {
                    ReleaseActiveCatalogHandle();
                    isCatalogLoading = false;
                }
            }

            if (string.IsNullOrWhiteSpace(catalogAddress))
            {
                return false;
            }

            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(catalogAddress.Trim());
            try
            {
                TextAsset textAsset = handle.WaitForCompletion();
                if (handle.Status != AsyncOperationStatus.Succeeded || textAsset == null)
                {
                    GameDebug.LogError($"[PermanentUpgradeService] Failed to synchronously load upgrade catalog at '{catalogAddress}'.");
                    return false;
                }

                if (!TryDeserializeCatalogJson(textAsset.text, out PermanentUpgradeCatalogData catalog, out string errorMessage))
                {
                    GameDebug.LogError($"[PermanentUpgradeService] {errorMessage}");
                    return false;
                }

                ApplyCatalog(catalog);
                return true;
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private void ApplyCatalog(PermanentUpgradeCatalogData catalog)
        {
            currentCatalog = catalog;
            hasCatalogLoaded = currentCatalog != null;
            entryById.Clear();

            if (currentCatalog != null && currentCatalog.Sections != null)
            {
                for (int sectionIndex = 0; sectionIndex < currentCatalog.Sections.Count; sectionIndex++)
                {
                    PermanentUpgradeSectionData section = currentCatalog.Sections[sectionIndex];
                    if (section?.Entries == null)
                    {
                        continue;
                    }

                    for (int entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
                    {
                        PermanentUpgradeEntryData entry = section.Entries[entryIndex];
                        if (entry != null)
                        {
                            entryById[entry.Id] = entry;
                        }
                    }
                }
            }

            RecalculateCachedStatModifiers();
        }

        private void RecalculateCachedStatModifiers()
        {
            Dictionary<PermanentUpgradeStatId, MutableStatModifiers> mutableModifiersByStat = new();
            foreach (KeyValuePair<string, PermanentUpgradeEntryData> pair in entryById)
            {
                PermanentUpgradeEntryData entry = pair.Value;
                if (entry == null)
                {
                    continue;
                }

                int currentLevel = GetPurchasedLevel(entry.Id);
                if (currentLevel <= 0)
                {
                    continue;
                }

                List<PermanentUpgradeEffectData> effects = entry.Effects ?? new List<PermanentUpgradeEffectData>();
                for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    PermanentUpgradeEffectData effect = effects[effectIndex];
                    if (effect == null)
                    {
                        continue;
                    }

                    if (!mutableModifiersByStat.TryGetValue(effect.StatId, out MutableStatModifiers modifiers))
                    {
                        modifiers = MutableStatModifiers.Identity;
                    }

                    float scaledValue = effect.Value * currentLevel;
                    switch (effect.Operation)
                    {
                        case PermanentUpgradeStatOperation.AddFlat:
                            modifiers.flatBonus += scaledValue;
                            break;
                        case PermanentUpgradeStatOperation.AddMultiplier:
                            modifiers.additiveMultiplier += scaledValue;
                            break;
                        case PermanentUpgradeStatOperation.Multiply:
                            modifiers.multiplicativeMultiplier *= Mathf.Pow(effect.Value, currentLevel);
                            break;
                    }

                    mutableModifiersByStat[effect.StatId] = modifiers;
                }
            }

            cachedModifiersByStat.Clear();
            foreach (KeyValuePair<PermanentUpgradeStatId, MutableStatModifiers> pair in mutableModifiersByStat)
            {
                cachedModifiersByStat[pair.Key] = pair.Value.ToImmutable();
            }
        }

        private bool HasPurchasedRequiredEntries(PermanentUpgradeEntryData entry)
        {
            if (entry?.Requires == null || entry.Requires.Count <= 0)
            {
                return true;
            }

            for (int index = 0; index < entry.Requires.Count; index++)
            {
                string requiredEntryId = SanitizeIdentifier(entry.Requires[index]);
                if (string.IsNullOrEmpty(requiredEntryId) || GetPurchasedLevel(requiredEntryId) < 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryBuildValidatedCatalog(PermanentUpgradeCatalogData rawCatalog, out PermanentUpgradeCatalogData catalog, out string errorMessage)
        {
            catalog = null;
            errorMessage = null;

            if (rawCatalog?.Sections == null || rawCatalog.Sections.Count <= 0)
            {
                errorMessage = "Permanent upgrade catalog must contain at least one section.";
                return false;
            }

            if (!TryBuildPositiveVector2(
                    rawCatalog.CanvasSize,
                    DefaultCanvasWidth,
                    DefaultCanvasHeight,
                    "Permanent upgrade catalog canvasSize",
                    out PermanentUpgradeVector2Data sanitizedCanvasSize,
                    out errorMessage))
            {
                return false;
            }

            HashSet<string> sectionIds = new(StringComparer.Ordinal);
            HashSet<string> entryIds = new(StringComparer.Ordinal);
            List<PermanentUpgradeSectionData> sanitizedSections = new(rawCatalog.Sections.Count);
            Dictionary<string, PermanentUpgradeEntryData> sanitizedEntryById = new(StringComparer.Ordinal);

            for (int sectionIndex = 0; sectionIndex < rawCatalog.Sections.Count; sectionIndex++)
            {
                PermanentUpgradeSectionData rawSection = rawCatalog.Sections[sectionIndex];
                string sectionId = SanitizeIdentifier(rawSection?.Id);
                if (string.IsNullOrEmpty(sectionId))
                {
                    errorMessage = $"Permanent upgrade section #{sectionIndex} is missing a valid id.";
                    return false;
                }

                if (!sectionIds.Add(sectionId))
                {
                    errorMessage = $"Permanent upgrade section id '{sectionId}' is duplicated.";
                    return false;
                }

                List<PermanentUpgradeEntryData> rawEntries = rawSection?.Entries ?? new List<PermanentUpgradeEntryData>();
                List<PermanentUpgradeEntryData> sanitizedEntries = new(rawEntries.Count);
                for (int entryIndex = 0; entryIndex < rawEntries.Count; entryIndex++)
                {
                    PermanentUpgradeEntryData rawEntry = rawEntries[entryIndex];
                    string entryId = SanitizeIdentifier(rawEntry?.Id);
                    if (string.IsNullOrEmpty(entryId))
                    {
                        errorMessage = $"Permanent upgrade entry #{entryIndex} in section '{sectionId}' is missing a valid id.";
                        return false;
                    }

                    if (!entryIds.Add(entryId))
                    {
                        errorMessage = $"Permanent upgrade entry id '{entryId}' is duplicated.";
                        return false;
                    }

                    if (!TryBuildFiniteVector2(
                            rawEntry.Position,
                            0f,
                            0f,
                            $"Permanent upgrade entry '{entryId}' position",
                            out PermanentUpgradeVector2Data sanitizedPosition,
                            out errorMessage))
                    {
                        return false;
                    }

                    if (!TryBuildPositiveVector2(
                            rawEntry.Size,
                            DefaultNodeWidth,
                            DefaultNodeHeight,
                            $"Permanent upgrade entry '{entryId}' size",
                            out PermanentUpgradeVector2Data sanitizedSize,
                            out errorMessage))
                    {
                        return false;
                    }

                    if (rawEntry.CostRemnants < 0)
                    {
                        errorMessage = $"Permanent upgrade entry '{entryId}' has a negative Remnant cost.";
                        return false;
                    }

                    if (rawEntry.MaxLevel < 1)
                    {
                        errorMessage = $"Permanent upgrade entry '{entryId}' must have maxLevel >= 1.";
                        return false;
                    }

                    if (!Enum.IsDefined(typeof(PermanentUpgradeNodeShape), rawEntry.Shape))
                    {
                        errorMessage = $"Permanent upgrade entry '{entryId}' has an invalid shape.";
                        return false;
                    }

                    if (!TryBuildEffects(entryId, rawEntry.Effects, out List<PermanentUpgradeEffectData> sanitizedEffects, out errorMessage))
                    {
                        return false;
                    }

                    if (!TryBuildRequires(entryId, rawEntry.Requires, out List<string> sanitizedRequires, out errorMessage))
                    {
                        return false;
                    }

                    if (!TryBuildHtmlColor(
                            rawEntry.BackgroundColor,
                            DefaultNodeBackgroundColor,
                            $"Permanent upgrade entry '{entryId}' backgroundColor",
                            out string sanitizedBackgroundColor,
                            out errorMessage))
                    {
                        return false;
                    }

                    if (!TryBuildHtmlColor(
                            rawEntry.BorderColor,
                            DefaultNodeBorderColor,
                            $"Permanent upgrade entry '{entryId}' borderColor",
                            out string sanitizedBorderColor,
                            out errorMessage))
                    {
                        return false;
                    }

                    if (!TryBuildNonNegativeOrDefault(
                            rawEntry.BorderWidth,
                            DefaultBorderWidth,
                            $"Permanent upgrade entry '{entryId}' borderWidth",
                            out float sanitizedBorderWidth,
                            out errorMessage))
                    {
                        return false;
                    }

                    PermanentUpgradeEntryData sanitizedEntry = new()
                    {
                        Id = entryId,
                        Title = SanitizeTitle(rawEntry.Title, entryId),
                        CostRemnants = rawEntry.CostRemnants,
                        MaxLevel = rawEntry.MaxLevel,
                        Effects = sanitizedEffects,
                        Requires = sanitizedRequires,
                        Position = sanitizedPosition,
                        Size = sanitizedSize,
                        Shape = rawEntry.Shape,
                        IconAddress = SanitizeOptionalText(rawEntry.IconAddress),
                        BackgroundColor = sanitizedBackgroundColor,
                        BorderColor = sanitizedBorderColor,
                        BorderWidth = sanitizedBorderWidth,
                    };
                    sanitizedEntries.Add(sanitizedEntry);
                    sanitizedEntryById[entryId] = sanitizedEntry;
                }

                sanitizedSections.Add(new PermanentUpgradeSectionData
                {
                    Id = sectionId,
                    Title = SanitizeTitle(rawSection.Title, sectionId),
                    Entries = sanitizedEntries,
                });
            }

            foreach (KeyValuePair<string, PermanentUpgradeEntryData> pair in sanitizedEntryById)
            {
                List<string> requires = pair.Value.Requires;
                for (int index = 0; index < requires.Count; index++)
                {
                    string requiredEntryId = requires[index];
                    if (!entryIds.Contains(requiredEntryId))
                    {
                        errorMessage = $"Permanent upgrade entry '{pair.Key}' requires unknown entry '{requiredEntryId}'.";
                        return false;
                    }
                }
            }

            if (HasRequirementCycle(sanitizedEntryById, out string cycleEntryId))
            {
                errorMessage = $"Permanent upgrade requirements contain a cycle at '{cycleEntryId}'.";
                return false;
            }

            if (!TryBuildEdges(rawCatalog.Edges, entryIds, out List<PermanentUpgradeEdgeData> sanitizedEdges, out errorMessage))
            {
                return false;
            }

            catalog = new PermanentUpgradeCatalogData
            {
                CanvasSize = sanitizedCanvasSize,
                Edges = sanitizedEdges,
                Sections = sanitizedSections,
            };
            return true;
        }

        private static bool TryBuildEffects(
            string entryId,
            List<PermanentUpgradeEffectData> rawEffects,
            out List<PermanentUpgradeEffectData> sanitizedEffects,
            out string errorMessage)
        {
            errorMessage = null;
            sanitizedEffects = null;

            if (rawEffects == null || rawEffects.Count <= 0)
            {
                errorMessage = $"Permanent upgrade entry '{entryId}' must contain at least one effect.";
                return false;
            }

            sanitizedEffects = new List<PermanentUpgradeEffectData>(rawEffects.Count);
            for (int effectIndex = 0; effectIndex < rawEffects.Count; effectIndex++)
            {
                PermanentUpgradeEffectData rawEffect = rawEffects[effectIndex];
                if (rawEffect == null)
                {
                    errorMessage = $"Permanent upgrade entry '{entryId}' effect #{effectIndex} is missing.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(PermanentUpgradeStatId), rawEffect.StatId))
                {
                    errorMessage = $"Permanent upgrade entry '{entryId}' effect #{effectIndex} has an unknown statId.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(PermanentUpgradeStatOperation), rawEffect.Operation))
                {
                    errorMessage = $"Permanent upgrade entry '{entryId}' effect #{effectIndex} has an unknown operation.";
                    return false;
                }

                if (!IsFinite(rawEffect.Value))
                {
                    errorMessage = $"Permanent upgrade entry '{entryId}' effect #{effectIndex} has a non-finite value.";
                    return false;
                }

                if (rawEffect.Operation == PermanentUpgradeStatOperation.Multiply)
                {
                    if (rawEffect.Value <= 0f)
                    {
                        errorMessage = $"Permanent upgrade entry '{entryId}' effect #{effectIndex} multiply value must be positive.";
                        return false;
                    }
                }
                else if (rawEffect.Value < 0f)
                {
                    errorMessage = $"Permanent upgrade entry '{entryId}' effect #{effectIndex} value cannot be negative.";
                    return false;
                }

                sanitizedEffects.Add(new PermanentUpgradeEffectData
                {
                    StatId = rawEffect.StatId,
                    Operation = rawEffect.Operation,
                    Value = rawEffect.Value,
                });
            }

            return true;
        }

        private static bool TryBuildEdges(
            List<PermanentUpgradeEdgeData> rawEdges,
            HashSet<string> entryIds,
            out List<PermanentUpgradeEdgeData> sanitizedEdges,
            out string errorMessage)
        {
            errorMessage = null;
            rawEdges ??= new List<PermanentUpgradeEdgeData>();
            sanitizedEdges = new List<PermanentUpgradeEdgeData>(rawEdges.Count);

            for (int edgeIndex = 0; edgeIndex < rawEdges.Count; edgeIndex++)
            {
                PermanentUpgradeEdgeData rawEdge = rawEdges[edgeIndex];
                string from = SanitizeIdentifier(rawEdge?.From);
                string to = SanitizeIdentifier(rawEdge?.To);
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
                {
                    errorMessage = $"Permanent upgrade edge #{edgeIndex} is missing a valid endpoint.";
                    return false;
                }

                if (!entryIds.Contains(from))
                {
                    errorMessage = $"Permanent upgrade edge #{edgeIndex} uses unknown from endpoint '{from}'.";
                    return false;
                }

                if (!entryIds.Contains(to))
                {
                    errorMessage = $"Permanent upgrade edge #{edgeIndex} uses unknown to endpoint '{to}'.";
                    return false;
                }

                if (string.Equals(from, to, StringComparison.Ordinal))
                {
                    errorMessage = $"Permanent upgrade edge #{edgeIndex} cannot connect '{from}' to itself.";
                    return false;
                }

                if (!TryBuildHtmlColor(
                        rawEdge?.Color,
                        DefaultEdgeColor,
                        $"Permanent upgrade edge #{edgeIndex} color",
                        out string color,
                        out errorMessage))
                {
                    return false;
                }

                if (!TryBuildPositiveOrDefault(
                        rawEdge != null ? rawEdge.Width : 0f,
                        DefaultEdgeWidth,
                        $"Permanent upgrade edge #{edgeIndex} width",
                        out float width,
                        out errorMessage))
                {
                    return false;
                }

                sanitizedEdges.Add(new PermanentUpgradeEdgeData
                {
                    From = from,
                    To = to,
                    Color = color,
                    Width = width,
                });
            }

            return true;
        }

        private static bool TryBuildRequires(
            string entryId,
            List<string> rawRequires,
            out List<string> sanitizedRequires,
            out string errorMessage)
        {
            errorMessage = null;
            rawRequires ??= new List<string>();
            sanitizedRequires = new List<string>(rawRequires.Count);
            HashSet<string> uniqueRequires = new(StringComparer.Ordinal);

            for (int index = 0; index < rawRequires.Count; index++)
            {
                string requiredEntryId = SanitizeIdentifier(rawRequires[index]);
                if (string.IsNullOrEmpty(requiredEntryId))
                {
                    errorMessage = $"Permanent upgrade entry '{entryId}' has an empty requires entry.";
                    return false;
                }

                if (string.Equals(entryId, requiredEntryId, StringComparison.Ordinal))
                {
                    errorMessage = $"Permanent upgrade entry '{entryId}' cannot require itself.";
                    return false;
                }

                if (uniqueRequires.Add(requiredEntryId))
                {
                    sanitizedRequires.Add(requiredEntryId);
                }
            }

            return true;
        }

        private static bool HasRequirementCycle(
            Dictionary<string, PermanentUpgradeEntryData> entries,
            out string cycleEntryId)
        {
            cycleEntryId = null;
            Dictionary<string, int> visitStateByEntryId = new(StringComparer.Ordinal);

            foreach (string entryId in entries.Keys)
            {
                if (Visit(entryId))
                {
                    cycleEntryId = entryId;
                    return true;
                }
            }

            return false;

            bool Visit(string entryId)
            {
                if (!entries.TryGetValue(entryId, out PermanentUpgradeEntryData entry))
                {
                    return false;
                }

                if (visitStateByEntryId.TryGetValue(entryId, out int state))
                {
                    return state == 1;
                }

                visitStateByEntryId[entryId] = 1;
                List<string> requires = entry.Requires ?? new List<string>();
                for (int index = 0; index < requires.Count; index++)
                {
                    if (Visit(requires[index]))
                    {
                        return true;
                    }
                }

                visitStateByEntryId[entryId] = 2;
                return false;
            }
        }

        private static bool TryBuildFiniteVector2(
            PermanentUpgradeVector2Data rawVector,
            float defaultX,
            float defaultY,
            string fieldName,
            out PermanentUpgradeVector2Data vector,
            out string errorMessage)
        {
            errorMessage = null;
            float x = rawVector != null ? rawVector.X : defaultX;
            float y = rawVector != null ? rawVector.Y : defaultY;
            if (!IsFinite(x) || !IsFinite(y))
            {
                errorMessage = $"{fieldName} must contain finite x/y values.";
                vector = null;
                return false;
            }

            vector = CreateVector2(x, y);
            return true;
        }

        private static bool TryBuildPositiveVector2(
            PermanentUpgradeVector2Data rawVector,
            float defaultX,
            float defaultY,
            string fieldName,
            out PermanentUpgradeVector2Data vector,
            out string errorMessage)
        {
            if (!TryBuildFiniteVector2(rawVector, defaultX, defaultY, fieldName, out vector, out errorMessage))
            {
                return false;
            }

            if (vector.X <= 0f || vector.Y <= 0f)
            {
                errorMessage = $"{fieldName} must contain positive x/y values.";
                vector = null;
                return false;
            }

            return true;
        }

        private static bool TryBuildHtmlColor(
            string rawColor,
            string defaultColor,
            string fieldName,
            out string color,
            out string errorMessage)
        {
            errorMessage = null;
            color = SanitizeOptionalText(rawColor);
            if (string.IsNullOrEmpty(color))
            {
                color = defaultColor;
            }

            if (!ColorUtility.TryParseHtmlString(color, out _))
            {
                errorMessage = $"{fieldName} must be a valid HTML color.";
                return false;
            }

            return true;
        }

        private static bool TryBuildNonNegativeOrDefault(
            float rawValue,
            float defaultValue,
            string fieldName,
            out float value,
            out string errorMessage)
        {
            errorMessage = null;
            if (!IsFinite(rawValue))
            {
                value = 0f;
                errorMessage = $"{fieldName} must be finite.";
                return false;
            }

            if (rawValue < 0f)
            {
                value = 0f;
                errorMessage = $"{fieldName} cannot be negative.";
                return false;
            }

            value = rawValue > 0f ? rawValue : defaultValue;
            return true;
        }

        private static bool TryBuildPositiveOrDefault(
            float rawValue,
            float defaultValue,
            string fieldName,
            out float value,
            out string errorMessage)
        {
            if (!TryBuildNonNegativeOrDefault(rawValue, defaultValue, fieldName, out value, out errorMessage))
            {
                return false;
            }

            if (value <= 0f)
            {
                errorMessage = $"{fieldName} must be positive.";
                return false;
            }

            return true;
        }

        private static string SanitizeIdentifier(string rawIdentifier)
        {
            return rawIdentifier != null ? rawIdentifier.Trim() : string.Empty;
        }

        private static string SanitizeTitle(string rawTitle, string fallbackTitle)
        {
            string trimmedTitle = rawTitle != null ? rawTitle.Trim() : string.Empty;
            return string.IsNullOrEmpty(trimmedTitle) ? fallbackTitle : trimmedTitle;
        }

        private static string SanitizeOptionalText(string rawText)
        {
            return rawText != null ? rawText.Trim() : string.Empty;
        }

        private static PermanentUpgradeVector2Data CreateVector2(float x, float y)
        {
            return new PermanentUpgradeVector2Data
            {
                X = x,
                Y = y,
            };
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private struct MutableStatModifiers
        {
            public float flatBonus;
            public float additiveMultiplier;
            public float multiplicativeMultiplier;

            public static MutableStatModifiers Identity => new()
            {
                flatBonus = 0f,
                additiveMultiplier = 0f,
                multiplicativeMultiplier = 1f,
            };

            public PermanentUpgradeStatModifiers ToImmutable()
            {
                return new PermanentUpgradeStatModifiers(flatBonus, additiveMultiplier, multiplicativeMultiplier);
            }
        }

        private void ReleaseActiveCatalogHandle()
        {
            if (!hasActiveCatalogHandle)
            {
                return;
            }

            if (activeCatalogHandle.IsValid())
            {
                Addressables.Release(activeCatalogHandle);
            }

            activeCatalogHandle = default;
            hasActiveCatalogHandle = false;
        }
    }
}
