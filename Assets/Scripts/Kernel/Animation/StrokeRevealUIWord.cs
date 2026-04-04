using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StrokeRevealUIWord : MonoBehaviour
{
    [Serializable]
    public class StrokeItem
    {
        public StrokeRevealUIImage stroke;
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

    [Header("笔画列表")]
    public List<StrokeItem> strokes = new();

    [Header("播放设置")]
    public AutoPlayMode playOnEnable = AutoPlayMode.Reveal;
    public float initialDelay = 0f;
    [Range(0.0001f, 0.2f)] public float softness = 0.02f;

    [Header("隐藏设置")]
    public bool hideInReverseStrokeOrder = true;

    private Coroutine _playRoutine;

    /// <summary>
    /// 初始化全部笔画状态。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    private void Awake()
    {
        ResetState(false);
    }

    /// <summary>
    /// 启用时按配置自动播放显现或隐藏动画。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    private void OnEnable()
    {
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
    /// 从头播放“从隐到显”的逐笔动画。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    [ContextMenu("Play Reveal")]
    public void PlayReveal()
    {
        Stop();
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
    /// 重置所有笔画为隐藏或完整显示。
    /// </summary>
    /// <param name="visible">是否直接显示全部笔画。</param>
    /// <returns>无。</returns>
    public void ResetState(bool visible)
    {
        float value = visible ? 1f : 0f;

        for (int i = 0; i < strokes.Count; i++)
        {
            StrokeItem item = strokes[i];
            if (item == null || item.stroke == null) continue;

            item.stroke.EnsureMaterialInstance();
            item.stroke.SetRevealDirection(item.revealDirection);
            item.stroke.SetSoftness(softness);
            item.stroke.SetProgress(value);
        }
    }

    /// <summary>
    /// 按模式依次播放每一笔的显现或隐藏动画。
    /// </summary>
    /// <param name="isReveal">true 为显现，false 为隐藏。</param>
    /// <returns>协程枚举器。</returns>
    private IEnumerator PlayRoutine(bool isReveal)
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        List<int> order = BuildPlayOrder(isReveal);

        for (int n = 0; n < order.Count; n++)
        {
            StrokeItem item = strokes[order[n]];
            if (item == null || item.stroke == null) continue;

            item.stroke.EnsureMaterialInstance();
            item.stroke.SetRevealDirection(item.revealDirection);
            item.stroke.SetSoftness(softness);

            float duration = Mathf.Max(0.01f, item.duration);
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);

                float progress = isReveal
                    ? Mathf.Lerp(0f, 1f, t)
                    : Mathf.Lerp(1f, 0f, t);

                item.stroke.SetProgress(progress);
                yield return null;
            }

            item.stroke.SetProgress(isReveal ? 1f : 0f);

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
}