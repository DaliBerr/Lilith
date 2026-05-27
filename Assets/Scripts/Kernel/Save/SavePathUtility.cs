using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// 统一提供永久档槽位与全局设置的路径入口。
/// </summary>
internal static class SavePathUtility
{
    private const string SaveFolderName = "Saves";
    private const string ProfileFileNamePrefix = "profile-slot-";
    private const string ProfileFileNameExtension = ".json";
    private const string ProfileFileNameFormat = "profile-slot-{0}.json";
    private const string GlobalModeFileName = "global-mode.json";
    public const int DefaultProfileSlotCount = 4;
    public const int InvalidProfileSlotIndex = -1;

    /// <summary>
    /// summary: 返回当前项目在 persistentDataPath 下统一使用的存档目录。
    /// param: 无
    /// returns: 统一存档目录绝对路径
    /// </summary>
    public static string GetSaveDirectoryPath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFolderName);
    }

    /// <summary>
    /// summary: 返回指定栏位永久档文件的绝对路径。
    /// param name="slotIndex": 目标栏位索引，使用 0 起始的非负整数
    /// returns: 指定栏位 profile JSON 的绝对路径
    /// </summary>
    public static string GetProfileFilePath(int slotIndex)
    {
        if (!IsValidProfileSlotIndex(slotIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Profile slot index must be zero or greater.");
        }

        string fileName = string.Format(CultureInfo.InvariantCulture, ProfileFileNameFormat, slotIndex + 1);
        return Path.Combine(GetSaveDirectoryPath(), fileName);
    }

    /// <summary>
    /// summary: 返回全局模式配置文件的绝对路径。
    /// param: 无
    /// returns: global-mode.json 的绝对路径
    /// </summary>
    public static string GetGlobalModeFilePath()
    {
        return Path.Combine(GetSaveDirectoryPath(), GlobalModeFileName);
    }

    /// <summary>
    /// summary: 判断给定栏位索引是否为有效的非负槽位索引。
    /// param name="slotIndex": 目标栏位索引
    /// returns: 栏位索引有效时返回 true
    /// </summary>
    public static bool IsValidProfileSlotIndex(int slotIndex)
    {
        return slotIndex >= 0;
    }

    /// <summary>
    /// summary: 尝试从 profile-slot-N.json 文件名反解析 0 起始槽位索引。
    /// param name="fileName": 文件名或完整路径
    /// param name="slotIndex": 成功时输出 0 起始槽位索引
    /// returns: 文件名符合永久档槽位命名时返回 true
    /// </summary>
    public static bool TryParseProfileSlotIndex(string fileName, out int slotIndex)
    {
        slotIndex = InvalidProfileSlotIndex;
        string normalizedFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(normalizedFileName)
            || !normalizedFileName.StartsWith(ProfileFileNamePrefix, StringComparison.OrdinalIgnoreCase)
            || !normalizedFileName.EndsWith(ProfileFileNameExtension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int numberStart = ProfileFileNamePrefix.Length;
        int numberLength = normalizedFileName.Length - numberStart - ProfileFileNameExtension.Length;
        if (numberLength <= 0)
        {
            return false;
        }

        string numberText = normalizedFileName.Substring(numberStart, numberLength);
        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int oneBasedSlotNumber)
            || oneBasedSlotNumber <= 0)
        {
            return false;
        }

        slotIndex = oneBasedSlotNumber - 1;
        return true;
    }

    /// <summary>
    /// summary: 枚举当前磁盘上所有合法永久档槽位索引，并按索引升序返回。
    /// param: 无
    /// returns: 当前存在 profile JSON 的 0 起始槽位索引列表
    /// </summary>
    public static int[] EnumerateExistingProfileSlotIndices()
    {
        string saveDirectoryPath = GetSaveDirectoryPath();
        if (!Directory.Exists(saveDirectoryPath))
        {
            return Array.Empty<int>();
        }

        SortedSet<int> slotIndices = new();
        foreach (string filePath in Directory.EnumerateFiles(saveDirectoryPath, $"{ProfileFileNamePrefix}*{ProfileFileNameExtension}", SearchOption.TopDirectoryOnly))
        {
            if (TryParseProfileSlotIndex(filePath, out int slotIndex))
            {
                slotIndices.Add(slotIndex);
            }
        }

        int[] results = new int[slotIndices.Count];
        slotIndices.CopyTo(results);
        return results;
    }

    /// <summary>
    /// summary: 从 0 开始查找当前磁盘上第一个没有 profile JSON 的槽位索引。
    /// param: 无
    /// returns: 当前最小空槽位索引
    /// </summary>
    public static int FindNextEmptyProfileSlotIndex()
    {
        HashSet<int> existingSlotIndices = new(EnumerateExistingProfileSlotIndices());
        for (int slotIndex = 0; slotIndex < int.MaxValue; slotIndex++)
        {
            if (!existingSlotIndices.Contains(slotIndex))
            {
                return slotIndex;
            }
        }

        throw new InvalidOperationException("No available profile slot index remains.");
    }

    /// <summary>
    /// summary: 确保持久化目录存在，不存在时自动创建。
    /// param: 无
    /// returns: 无
    /// </summary>
    public static void EnsureSaveDirectoryExists()
    {
        string saveDirectoryPath = GetSaveDirectoryPath();
        if (!Directory.Exists(saveDirectoryPath))
        {
            Directory.CreateDirectory(saveDirectoryPath);
        }
    }
}
