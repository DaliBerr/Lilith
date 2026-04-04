using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StrokeRevealUIWord : MonoBehaviour
{
    [Serializable]
    public class StrokeItem
    {
        public Sprite sprite;
        public Vector2 revealDirection = Vector2.right;
        [Min(0.01f)] public float duration = 0.12f;
        [Min(0f)] public float delayAfter = 0.03f;
    }

    public enum AutoPlayMode
    {
        None,
        Reveal,
        Hide
    }

    private const string RuntimeStrokeRootName = "__RuntimeStrokes";

    [Header("笔画列表")]
    public Material strokeBaseMaterial;
    public List<StrokeItem> strokes = new();

    [Header("播放设置")]
    public AutoPlayMode playOnEnable = AutoPlayMode.Reveal;
    public float initialDelay = 0f;
    [Range(0.0001f, 0.2f)] public float softness = 0.02f;

    [Header("隐藏设置")]
    public bool hideInReverseStrokeOrder = true;

    private readonly List<StrokeRevealUIImage> _runtimeStrokes = new();
    private Coroutine _playRoutine;
    private RectTransform _runtimeStrokeRoot;

    /// <summary>
    /// 初始化全部笔画状态。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    private void Awake()
    {
        EnsureRuntimeStrokesBuilt();
        ResetState(false);
    }

    /// <summary>
    /// 启用时按配置自动播放显现或隐藏动画。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        switch (playOnEnable)
        {
            case AutoPlayMode.Reveal:
                PlayReveal();
                break;
            case AutoPlayMode.Hide:
                PlayHide();
                break;
        }
    }

    /// <summary>
    /// 停用时停止当前播放流程。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    private void OnDisable()
    {
        Stop();
    }

    /// <summary>
    /// 从头播放“从隐到显”的逐笔动画。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    [ContextMenu("Play Reveal")]
    public void PlayReveal()
    {
        Stop();
        EnsureRuntimeStrokesBuilt();
        ResetState(false);
        _playRoutine = StartCoroutine(PlayRoutine(isReveal: true));
    }

    /// <summary>
    /// 从头播放“从显到隐”的逐笔动画。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    [ContextMenu("Play Hide")]
    public void PlayHide()
    {
        Stop();
        EnsureRuntimeStrokesBuilt();
        ResetState(true);
        _playRoutine = StartCoroutine(PlayRoutine(isReveal: false));
    }

    /// <summary>
    /// 停止当前播放流程。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    [ContextMenu("Stop")]
    public void Stop()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
    }

    /// <summary>
    /// 重置所有笔画为隐藏或完整显示。
    /// </summary>
    /// <param name="visible">是否直接显示全部笔画。</param>
    /// <returns>无。</returns>
    [ContextMenu("Reset Hidden")]
    public void ResetHidden()
    {
        ResetState(false);
    }

    /// <summary>
    /// 重置所有笔画为完整显示。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    [ContextMenu("Reset Visible")]
    public void ResetVisible()
    {
        ResetState(true);
    }

    /// <summary>
    /// 获取当前配置的笔画数量。
    /// </summary>
    public int StrokeCount => strokes.Count;

    /// <summary>
    /// 按整字归一化进度设置全部笔画的显示状态。
    /// </summary>
    /// <param name="progress">整字显示进度，范围 0 到 1。</param>
    /// <param name="stopPlayback">是否在设置前停止当前播放流程。</param>
    /// <returns>无。</returns>
    public void SetNormalizedProgress(float progress, bool stopPlayback = true)
    {
        if (stopPlayback)
        {
            Stop();
        }

        EnsureRuntimeStrokesBuilt();

        int validStrokeCount = GetValidRuntimeStrokeCount();
        if (validStrokeCount <= 0)
        {
            return;
        }

        float totalProgress = Mathf.Clamp01(progress) * validStrokeCount;
        int validStrokeIndex = 0;

        for (int i = 0; i < strokes.Count; i++)
        {
            StrokeItem item = strokes[i];
            StrokeRevealUIImage runtimeStroke = GetRuntimeStroke(i);
            if (item == null || item.sprite == null || runtimeStroke == null)
            {
                continue;
            }

            ApplyStrokeSettings(runtimeStroke, item);
            runtimeStroke.SetProgress(Mathf.Clamp01(totalProgress - validStrokeIndex));
            validStrokeIndex++;
        }
    }

    /// <summary>
    /// 读取整字当前的归一化显示进度。
    /// </summary>
    /// <param name="progress">整字当前显示进度。</param>
    /// <returns>读取是否成功。</returns>
    public bool TryGetNormalizedProgress(out float progress)
    {
        EnsureRuntimeStrokesBuilt();

        float progressSum = 0f;
        int validStrokeCount = 0;

        for (int i = 0; i < strokes.Count; i++)
        {
            StrokeItem item = strokes[i];
            StrokeRevealUIImage runtimeStroke = GetRuntimeStroke(i);
            if (item == null || item.sprite == null || runtimeStroke == null)
            {
                continue;
            }

            progressSum += runtimeStroke.progress;
            validStrokeCount++;
        }

        if (validStrokeCount <= 0)
        {
            progress = 0f;
            return false;
        }

        progress = Mathf.Clamp01(progressSum / validStrokeCount);
        return true;
    }

    /// <summary>
    /// 重置所有笔画为隐藏或完整显示。
    /// </summary>
    /// <param name="visible">是否直接显示全部笔画。</param>
    /// <returns>无。</returns>
    public void ResetState(bool visible)
    {
        EnsureRuntimeStrokesBuilt();
        float value = visible ? 1f : 0f;

        for (int i = 0; i < strokes.Count; i++)
        {
            StrokeItem item = strokes[i];
            StrokeRevealUIImage runtimeStroke = GetRuntimeStroke(i);
            if (item == null || runtimeStroke == null)
            {
                continue;
            }

            ApplyStrokeSettings(runtimeStroke, item);
            runtimeStroke.SetProgress(value);
        }
    }

    /// <summary>
    /// 单独设置指定笔画的显示进度。
    /// </summary>
    /// <param name="index">笔画索引。</param>
    /// <param name="progress">显示进度，范围 0 到 1。</param>
    /// <param name="stopPlayback">是否在设置前停止当前播放流程。</param>
    /// <returns>设置是否成功。</returns>
    public bool TrySetStrokeProgress(int index, float progress, bool stopPlayback = true)
    {
        if (stopPlayback)
        {
            Stop();
        }

        EnsureRuntimeStrokesBuilt();

        StrokeItem item = GetStrokeItem(index);
        StrokeRevealUIImage runtimeStroke = GetRuntimeStroke(index);
        if (item == null || runtimeStroke == null)
        {
            return false;
        }

        ApplyStrokeSettings(runtimeStroke, item);
        runtimeStroke.SetProgress(progress);
        return true;
    }

    /// <summary>
    /// 批量设置多笔画的显示进度。
    /// </summary>
    /// <param name="progresses">与笔画索引对应的显示进度列表。</param>
    /// <param name="stopPlayback">是否在设置前停止当前播放流程。</param>
    /// <returns>无。</returns>
    public void SetStrokeProgresses(IReadOnlyList<float> progresses, bool stopPlayback = true)
    {
        if (progresses == null)
        {
            return;
        }

        if (stopPlayback)
        {
            Stop();
        }

        EnsureRuntimeStrokesBuilt();

        int count = Mathf.Min(strokes.Count, progresses.Count);
        for (int i = 0; i < count; i++)
        {
            StrokeItem item = strokes[i];
            StrokeRevealUIImage runtimeStroke = GetRuntimeStroke(i);
            if (item == null || runtimeStroke == null)
            {
                continue;
            }

            ApplyStrokeSettings(runtimeStroke, item);
            runtimeStroke.SetProgress(progresses[i]);
        }
    }

    /// <summary>
    /// 获取指定笔画当前的显示进度。
    /// </summary>
    /// <param name="index">笔画索引。</param>
    /// <param name="progress">笔画当前显示进度。</param>
    /// <returns>读取是否成功。</returns>
    public bool TryGetStrokeProgress(int index, out float progress)
    {
        EnsureRuntimeStrokesBuilt();

        StrokeRevealUIImage runtimeStroke = GetRuntimeStroke(index);
        if (runtimeStroke == null)
        {
            progress = 0f;
            return false;
        }

        progress = runtimeStroke.progress;
        return true;
    }

    /// <summary>
    /// 按模式依次播放每一笔的显现或隐藏动画。
    /// </summary>
    /// <param name="isReveal">true 为显现，false 为隐藏。</param>
    /// <returns>协程枚举器。</returns>
    private IEnumerator PlayRoutine(bool isReveal)
    {
        EnsureRuntimeStrokesBuilt();

        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        List<int> order = BuildPlayOrder(isReveal);

        for (int n = 0; n < order.Count; n++)
        {
            StrokeItem item = strokes[order[n]];
            StrokeRevealUIImage runtimeStroke = GetRuntimeStroke(order[n]);
            if (item == null || runtimeStroke == null)
            {
                continue;
            }

            ApplyStrokeSettings(runtimeStroke, item);

            float duration = Mathf.Max(0.01f, item.duration);
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);

                float progress = isReveal
                    ? Mathf.Lerp(0f, 1f, t)
                    : Mathf.Lerp(1f, 0f, t);

                runtimeStroke.SetProgress(progress);
                yield return null;
            }

            runtimeStroke.SetProgress(isReveal ? 1f : 0f);

            if (item.delayAfter > 0f)
            {
                yield return new WaitForSeconds(item.delayAfter);
            }
        }

        _playRoutine = null;
    }

    /// <summary>
    /// 根据当前播放模式构建笔画播放顺序。
    /// </summary>
    /// <param name="isReveal">true 为显现，false 为隐藏。</param>
    /// <returns>笔画索引顺序列表。</returns>
    private List<int> BuildPlayOrder(bool isReveal)
    {
        List<int> order = new List<int>(strokes.Count);

        if (isReveal || !hideInReverseStrokeOrder)
        {
            for (int i = 0; i < strokes.Count; i++)
            {
                order.Add(i);
            }
        }
        else
        {
            for (int i = strokes.Count - 1; i >= 0; i--)
            {
                order.Add(i);
            }
        }

        return order;
    }

    /// <summary>
    /// 构建运行时笔画对象并缓存对应组件。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    private void EnsureRuntimeStrokesBuilt()
    {
        if (RuntimeStrokeCacheIsValid())
        {
            return;
        }

        _runtimeStrokeRoot = GetOrCreateRuntimeStrokeRoot();
        ClearRuntimeStrokeRootChildren();
        _runtimeStrokes.Clear();

        for (int i = 0; i < strokes.Count; i++)
        {
            StrokeItem item = strokes[i];
            if (item == null || item.sprite == null)
            {
                _runtimeStrokes.Add(null);
                continue;
            }

            StrokeRevealUIImage runtimeStroke = CreateRuntimeStroke(item, i);
            _runtimeStrokes.Add(runtimeStroke);
        }
    }

    /// <summary>
    /// 检查当前缓存的运行时笔画是否仍与配置一致。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>缓存是否可复用。</returns>
    private bool RuntimeStrokeCacheIsValid()
    {
        if (_runtimeStrokeRoot == null || _runtimeStrokes.Count != strokes.Count)
        {
            return false;
        }

        int expectedChildCount = 0;

        for (int i = 0; i < strokes.Count; i++)
        {
            StrokeItem item = strokes[i];
            StrokeRevealUIImage runtimeStroke = _runtimeStrokes[i];
            bool hasSprite = item != null && item.sprite != null;

            if (!hasSprite)
            {
                if (runtimeStroke != null)
                {
                    return false;
                }

                continue;
            }

            expectedChildCount++;
            if (runtimeStroke == null || runtimeStroke.transform.parent != _runtimeStrokeRoot)
            {
                return false;
            }

            Image image = runtimeStroke.GetComponent<Image>();
            if (image == null || image.sprite != item.sprite)
            {
                return false;
            }
        }

        return _runtimeStrokeRoot.childCount == expectedChildCount;
    }

    /// <summary>
    /// 获取或创建用于承载运行时笔画的容器节点。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>运行时笔画容器。</returns>
    private RectTransform GetOrCreateRuntimeStrokeRoot()
    {
        Transform existingChild = transform.Find(RuntimeStrokeRootName);
        RectTransform runtimeRoot;

        if (existingChild == null)
        {
            GameObject runtimeRootObject = new GameObject(RuntimeStrokeRootName, typeof(RectTransform));
            runtimeRoot = runtimeRootObject.GetComponent<RectTransform>();
            runtimeRoot.SetParent(transform, false);
        }
        else
        {
            runtimeRoot = existingChild as RectTransform;
            if (runtimeRoot == null)
            {
                runtimeRoot = existingChild.gameObject.AddComponent<RectTransform>();
            }
        }

        runtimeRoot.anchorMin = Vector2.zero;
        runtimeRoot.anchorMax = Vector2.one;
        runtimeRoot.pivot = new Vector2(0.5f, 0.5f);
        runtimeRoot.anchoredPosition = Vector2.zero;
        runtimeRoot.sizeDelta = Vector2.zero;
        runtimeRoot.localScale = Vector3.one;
        runtimeRoot.localRotation = Quaternion.identity;
        runtimeRoot.SetAsLastSibling();

        return runtimeRoot;
    }

    /// <summary>
    /// 清空运行时笔画容器下的所有子节点。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    private void ClearRuntimeStrokeRootChildren()
    {
        if (_runtimeStrokeRoot == null)
        {
            return;
        }

        for (int i = _runtimeStrokeRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = _runtimeStrokeRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    /// <summary>
    /// 为单个笔画创建运行时 Image 与动画组件。
    /// </summary>
    /// <param name="item">当前笔画配置。</param>
    /// <param name="index">笔画索引。</param>
    /// <returns>创建出的笔画组件。</returns>
    private StrokeRevealUIImage CreateRuntimeStroke(StrokeItem item, int index)
    {
        GameObject strokeObject = new GameObject($"Stroke_{index + 1}", typeof(RectTransform));
        RectTransform rectTransform = strokeObject.GetComponent<RectTransform>();
        rectTransform.SetParent(_runtimeStrokeRoot, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.SetSiblingIndex(index);

        Image image = strokeObject.AddComponent<Image>();
        image.sprite = item.sprite;
        image.overrideSprite = item.sprite;

        StrokeRevealUIImage runtimeStroke = strokeObject.AddComponent<StrokeRevealUIImage>();
        runtimeStroke.Initialize(strokeBaseMaterial, item.revealDirection, softness, 0f);
        return runtimeStroke;
    }

    /// <summary>
    /// 将共享配置写入单个运行时笔画。
    /// </summary>
    /// <param name="stroke">运行时笔画组件。</param>
    /// <param name="item">当前笔画配置。</param>
    /// <returns>无。</returns>
    private void ApplyStrokeSettings(StrokeRevealUIImage stroke, StrokeItem item)
    {
        stroke.SetBaseMaterial(strokeBaseMaterial);
        stroke.EnsureMaterialInstance();
        stroke.SetRevealDirection(item.revealDirection);
        stroke.SetSoftness(softness);
    }

    /// <summary>
    /// 获取指定索引的笔画配置。
    /// </summary>
    /// <param name="index">笔画索引。</param>
    /// <returns>对应的笔画配置。</returns>
    private StrokeItem GetStrokeItem(int index)
    {
        if (index < 0 || index >= strokes.Count)
        {
            return null;
        }

        return strokes[index];
    }

    /// <summary>
    /// 获取指定索引的运行时笔画组件。
    /// </summary>
    /// <param name="index">笔画索引。</param>
    /// <returns>对应的运行时笔画组件。</returns>
    private StrokeRevealUIImage GetRuntimeStroke(int index)
    {
        if (index < 0 || index >= _runtimeStrokes.Count)
        {
            return null;
        }

        return _runtimeStrokes[index];
    }

    /// <summary>
    /// 统计当前可参与整字进度换算的有效笔画数量。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>有效笔画数量。</returns>
    private int GetValidRuntimeStrokeCount()
    {
        int validStrokeCount = 0;

        for (int i = 0; i < strokes.Count; i++)
        {
            StrokeItem item = strokes[i];
            StrokeRevealUIImage runtimeStroke = GetRuntimeStroke(i);
            if (item == null || item.sprite == null || runtimeStroke == null)
            {
                continue;
            }

            validStrokeCount++;
        }

        return validStrokeCount;
    }
}
