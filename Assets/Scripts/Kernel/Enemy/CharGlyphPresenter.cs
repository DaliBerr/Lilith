using TMPro;
using UnityEngine;

/// <summary>
/// 负责缓存并刷新文字敌人的字形显示。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharGlyphPresenter : MonoBehaviour
{
    [SerializeField] private string defaultDisplayText = string.Empty;
    [SerializeField] private TMP_Text glyphText;

    public string DefaultDisplayText => defaultDisplayText ?? string.Empty;
    public TMP_Text GlyphText => glyphText;

    private void Awake()
    {
        TryCacheBindings();
        TryInitializeDefaultDisplayTextFromGlyph();
        RefreshDisplay();
    }

    private void OnValidate()
    {
        TryCacheBindings();
        TryInitializeDefaultDisplayTextFromGlyph();
        RefreshDisplay();
    }

    private void Reset()
    {
        TryCacheBindings(overwriteExisting: true);
        TryInitializeDefaultDisplayTextFromGlyph();
        RefreshDisplay();
    }

    /// <summary>
    /// summary: 尝试缓存当前角色层级里的字形文本组件；优先使用 Text/Glyph 路径。
    /// param: overwriteExisting 为 true 时即使已有有效引用也会强制重新解析
    /// returns: 成功解析到有效字形文本组件时返回 true
    /// </summary>
    public bool TryCacheBindings(bool overwriteExisting = false)
    {
        if (overwriteExisting || !IsGlyphTextReferenceValid())
        {
            glyphText = FindPreferredGlyphText();
        }

        return IsGlyphTextReferenceValid();
    }

    /// <summary>
    /// summary: 设置当前角色的默认显示文字，并立即刷新到字形节点。
    /// param: content 需要显示的新文字；空引用会被规范化为空字符串
    /// returns: 成功刷新到有效字形节点时返回 true
    /// </summary>
    public bool SetDisplayText(string content)
    {
        defaultDisplayText = content ?? string.Empty;
        RefreshDisplay();
        return IsGlyphTextReferenceValid();
    }

    /// <summary>
    /// summary: 把当前默认显示文字写回已绑定的字形文本组件。
    /// param: 无
    /// returns: 成功完成显示刷新时返回 true
    /// </summary>
    public bool RefreshDisplay()
    {
        if (!TryCacheBindings())
        {
            return false;
        }

        glyphText.text = DefaultDisplayText;
        return true;
    }

    private TMP_Text FindPreferredGlyphText()
    {
        Transform explicitGlyph = transform.Find("Text/Glyph");
        if (explicitGlyph != null && explicitGlyph.TryGetComponent(out TMP_Text explicitGlyphText))
        {
            return explicitGlyphText;
        }

        return GetComponentInChildren<TMP_Text>(includeInactive: true);
    }

    private bool IsGlyphTextReferenceValid()
    {
        return glyphText != null &&
               (glyphText.transform == transform || glyphText.transform.IsChildOf(transform));
    }

    private void TryInitializeDefaultDisplayTextFromGlyph()
    {
        if (!string.IsNullOrEmpty(defaultDisplayText) || glyphText == null || string.IsNullOrEmpty(glyphText.text))
        {
            return;
        }

        defaultDisplayText = glyphText.text;
    }
}
