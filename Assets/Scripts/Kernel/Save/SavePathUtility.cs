using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// 统一提供四个永久档槽位与全局设置的路径入口。
/// </summary>
internal static class SavePathUtility
{
    private const string SaveFolderName = "Saves";
    private const string ProfileFileNameFormat = "profile-slot-{0}.json";
    private const string GlobalModeFileName = "global-mode.json";
    public const int ProfileSlotCount = 4;
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
    /// param name="slotIndex": 目标栏位索引，使用 0 到 3
    /// returns: 指定栏位 profile JSON 的绝对路径
    /// </summary>
    public static string GetProfileFilePath(int slotIndex)
    {
        if (!IsValidProfileSlotIndex(slotIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Profile slot index must be between 0 and {ProfileSlotCount - 1}.");
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
    /// summary: 判断给定栏位索引是否落在当前固定四个栏位范围内。
    /// param name="slotIndex": 目标栏位索引
    /// returns: 栏位索引有效时返回 true
    /// </summary>
    public static bool IsValidProfileSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < ProfileSlotCount;
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
