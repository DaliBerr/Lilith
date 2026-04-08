using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// summary: 批量替换项目中的TMP字体引用工具窗口
/// param: 无
/// return: 无
/// </summary>
public class ReplaceTMPFontReferences : EditorWindow
{
    private TMP_FontAsset _oldFont;
    private TMP_FontAsset _newFont;

    private bool _replaceInPrefabs = true;
    private bool _replaceInScenes = true;
    private bool _replaceFallbacks = true;

    [MenuItem("Tools/TMP/批量替换TMP字体引用")]
    /// <summary>
    /// summary: 打开批量替换TMP字体引用窗口
    /// param: 无
    /// return: 无
    /// </summary>
    private static void OpenWindow()
    {
        GetWindow<ReplaceTMPFontReferences>("替换TMP字体");
    }

    /// <summary>
    /// summary: 绘制编辑器窗口界面
    /// param: 无
    /// return: 无
    /// </summary>
    private void OnGUI()
    {
        GUILayout.Label("TMP 字体批量替换", EditorStyles.boldLabel);

        _oldFont = (TMP_FontAsset)EditorGUILayout.ObjectField("旧字体", _oldFont, typeof(TMP_FontAsset), false);
        _newFont = (TMP_FontAsset)EditorGUILayout.ObjectField("新字体", _newFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space();

        _replaceInPrefabs = EditorGUILayout.Toggle("替换预制体", _replaceInPrefabs);
        _replaceInScenes = EditorGUILayout.Toggle("替换场景", _replaceInScenes);
        _replaceFallbacks = EditorGUILayout.Toggle("替换Fallback列表中的引用", _replaceFallbacks);

        EditorGUILayout.Space();

        GUI.enabled = _oldFont != null && _newFont != null && _oldFont != _newFont;

        if (GUILayout.Button("开始替换", GUILayout.Height(32)))
        {
            ExecuteReplace();
        }

        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "建议先提交Git或备份工程。该工具会修改场景和预制体中的 TMP_Text.font 引用，并可选替换 Fallback Font Asset 列表中的旧字体。",
            MessageType.Info
        );
    }

    /// <summary>
    /// summary: 执行整个项目的字体替换流程
    /// param: 无
    /// return: 无
    /// </summary>
    private void ExecuteReplace()
    {
        int prefabReplaceCount = 0;
        int sceneReplaceCount = 0;
        int fallbackReplaceCount = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            if (_replaceInPrefabs)
            {
                prefabReplaceCount = ReplaceInAllPrefabs();
            }

            if (_replaceInScenes)
            {
                sceneReplaceCount = ReplaceInAllScenes();
            }

            if (_replaceFallbacks)
            {
                fallbackReplaceCount = ReplaceInAllFontAssetsFallbacks();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "替换完成",
            $"预制体替换数量：{prefabReplaceCount}\n场景替换数量：{sceneReplaceCount}\nFallback替换数量：{fallbackReplaceCount}",
            "确定"
        );

        // Debug.Log($"[TMP字体替换] 完成：预制体={prefabReplaceCount}，场景={sceneReplaceCount}，Fallback={fallbackReplaceCount}");
    }

    /// <summary>
    /// summary: 扫描并替换所有预制体中的TMP字体引用
    /// param: 无
    /// return: 替换成功的文本组件数量
    /// </summary>
    private int ReplaceInAllPrefabs()
    {
        int replaceCount = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            EditorUtility.DisplayProgressBar("替换预制体中的TMP字体", path, (float)i / prefabGuids.Length);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            replaceCount += ReplaceFontsInTMPTexts(texts, ref changed);

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        EditorUtility.ClearProgressBar();
        return replaceCount;
    }

    /// <summary>
    /// summary: 扫描并替换所有场景中的TMP字体引用
    /// param: 无
    /// return: 替换成功的文本组件数量
    /// </summary>
    private int ReplaceInAllScenes()
    {
        int replaceCount = 0;
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");

        Scene currentScene = SceneManager.GetActiveScene();
        string currentScenePath = currentScene.path;

        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            EditorUtility.DisplayProgressBar("替换场景中的TMP字体", path, (float)i / sceneGuids.Length);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;

            List<TMP_Text> sceneTexts = new List<TMP_Text>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                sceneTexts.AddRange(root.GetComponentsInChildren<TMP_Text>(true));
            }

            replaceCount += ReplaceFontsInTMPTexts(sceneTexts.ToArray(), ref changed);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        if (!string.IsNullOrEmpty(currentScenePath))
        {
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
        }

        EditorUtility.ClearProgressBar();
        return replaceCount;
    }

    /// <summary>
    /// summary: 替换所有TMP字体资源中的Fallback列表引用
    /// param: 无
    /// return: 替换成功的Fallback引用数量
    /// </summary>
    private int ReplaceInAllFontAssetsFallbacks()
    {
        int replaceCount = 0;
        string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");

        for (int i = 0; i < fontGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(fontGuids[i]);
            EditorUtility.DisplayProgressBar("替换字体资源Fallback引用", path, (float)i / fontGuids.Length);

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fontAsset == null)
            {
                continue;
            }

            bool changed = false;
            List<TMP_FontAsset> fallbacks = fontAsset.fallbackFontAssetTable;

            for (int j = 0; j < fallbacks.Count; j++)
            {
                if (fallbacks[j] == _oldFont)
                {
                    fallbacks[j] = _newFont;
                    replaceCount++;
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(fontAsset);
            }
        }

        EditorUtility.ClearProgressBar();
        return replaceCount;
    }

    /// <summary>
    /// summary: 替换一组TMP文本组件上的字体引用
    /// param: texts 要处理的TMP文本组件数组
    /// param: changed 是否有资源发生变化
    /// return: 替换成功的文本组件数量
    /// </summary>
    private int ReplaceFontsInTMPTexts(TMP_Text[] texts, ref bool changed)
    {
        int replaceCount = 0;

        foreach (TMP_Text text in texts)
        {
            if (text == null)
            {
                continue;
            }

            if (text.font == _oldFont)
            {
                Undo.RecordObject(text, "Replace TMP Font");
                text.font = _newFont;
                EditorUtility.SetDirty(text);
                changed = true;
                replaceCount++;
            }
        }

        return replaceCount;
    }
}