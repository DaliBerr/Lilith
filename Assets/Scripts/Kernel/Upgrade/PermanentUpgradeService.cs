using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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

        private static readonly IReadOnlyList<PermanentUpgradeSectionData> EmptySections = Array.Empty<PermanentUpgradeSectionData>();

        private PermanentUpgradeCatalogData currentCatalog;
        private readonly Dictionary<string, PermanentUpgradeEntryData> entryById = new(StringComparer.Ordinal);
        private bool hasCatalogLoaded;
        private bool isCatalogLoading;
        private float cachedDamageMultiplier = 1f;
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
                rawCatalog = JsonConvert.DeserializeObject<PermanentUpgradeCatalogData>(jsonText);
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
        /// summary: 返回当前永久升级对伤害编译的总倍率；未加载目录时会同步兜底加载一次。
        /// param: 无
        /// returns: 当前应应用到编译后伤害上的总倍率
        /// </summary>
        public float GetDamageMultiplier()
        {
            EnsureCatalogLoaded();
            return Mathf.Max(1f, cachedDamageMultiplier);
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
                message: "升级目录尚未加载完成。");

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
                    message: "未找到对应的永久升级条目。");
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
                    message: "该升级已经达到上限。");
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
                    message: "残卷不足，无法购买该升级。");
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
                        message: "残卷不足，无法购买该升级。");
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
                    message: "存档服务不可用，升级购买失败。");
                return false;
            }

            RecalculateCachedDamageMultiplier();
            result = new PermanentUpgradePurchaseResult(
                succeeded: true,
                failureReason: PermanentUpgradePurchaseFailureReason.None,
                entryId: entry.Id,
                newLevel: newLevel,
                remainingRemnants: remainingRemnants,
                message: "购买成功。");
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

            RecalculateCachedDamageMultiplier();
        }

        private void RecalculateCachedDamageMultiplier()
        {
            float totalBonus = 0f;
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

                if (entry.EffectType == PermanentUpgradeEffectType.DamageMultiplierBonus)
                {
                    totalBonus += Mathf.Max(0f, entry.EffectValue) * currentLevel;
                }
            }

            cachedDamageMultiplier = 1f + Mathf.Max(0f, totalBonus);
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

            HashSet<string> sectionIds = new(StringComparer.Ordinal);
            HashSet<string> entryIds = new(StringComparer.Ordinal);
            List<PermanentUpgradeSectionData> sanitizedSections = new(rawCatalog.Sections.Count);

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

                    if (float.IsNaN(rawEntry.EffectValue) || float.IsInfinity(rawEntry.EffectValue) || rawEntry.EffectValue < 0f)
                    {
                        errorMessage = $"Permanent upgrade entry '{entryId}' has an invalid effectValue.";
                        return false;
                    }

                    sanitizedEntries.Add(new PermanentUpgradeEntryData
                    {
                        Id = entryId,
                        Title = SanitizeTitle(rawEntry.Title, entryId),
                        CostRemnants = rawEntry.CostRemnants,
                        MaxLevel = rawEntry.MaxLevel,
                        EffectType = rawEntry.EffectType,
                        EffectValue = rawEntry.EffectValue,
                    });
                }

                sanitizedSections.Add(new PermanentUpgradeSectionData
                {
                    Id = sectionId,
                    Title = SanitizeTitle(rawSection.Title, sectionId),
                    Entries = sanitizedEntries,
                });
            }

            catalog = new PermanentUpgradeCatalogData
            {
                Sections = sanitizedSections,
            };
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
