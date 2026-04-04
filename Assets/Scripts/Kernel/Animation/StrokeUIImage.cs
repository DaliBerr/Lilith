using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class StrokeRevealUIImage : MonoBehaviour
{
    [Header("基础设置")]
    public Material baseMaterial;

    [Header("显示参数")]
    [Range(0f, 1f)] public float progress = 0f;
    public Vector2 revealDirection = Vector2.right;
    [Range(0.0001f, 0.2f)] public float softness = 0.02f;

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int RevealDirId = Shader.PropertyToID("_RevealDir");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

    private Image _image;
    private Material _runtimeMaterial;

    /// <summary>
    /// 获取组件并初始化材质实例。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    private void Awake()
    {
        _image = GetComponent<Image>();
        EnsureMaterialInstance();
        ApplyToMaterial();
    }

    /// <summary>
    /// 编辑器数值变化时同步到材质。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    private void OnValidate()
    {
        if (_image == null)
        {
            _image = GetComponent<Image>();
        }

        if (Application.isPlaying)
        {
            EnsureMaterialInstance();
            ApplyToMaterial();
        }
    }

    /// <summary>
    /// 组件销毁时清理运行时材质实例。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    private void OnDestroy()
    {
        DestroyRuntimeMaterial();
    }

    /// <summary>
    /// 设置当前笔画使用的基础材质。
    /// </summary>
    /// <param name="material">共享的基础材质。</param>
    /// <returns>无。</returns>
    public void SetBaseMaterial(Material material)
    {
        if (baseMaterial == material)
        {
            return;
        }

        baseMaterial = material;

        if (_runtimeMaterial != null)
        {
            DestroyRuntimeMaterial();
        }

        if (_image == null)
        {
            _image = GetComponent<Image>();
        }

        if (_image != null && baseMaterial == null)
        {
            _image.material = null;
        }
    }

    /// <summary>
    /// 初始化当前笔画的材质与显示参数。
    /// </summary>
    /// <param name="material">共享的基础材质。</param>
    /// <param name="dir">揭示方向向量。</param>
    /// <param name="softnessValue">边缘柔和度。</param>
    /// <param name="progressValue">显示进度。</param>
    /// <returns>无。</returns>
    public void Initialize(Material material, Vector2 dir, float softnessValue, float progressValue)
    {
        SetBaseMaterial(material);
        EnsureMaterialInstance();
        SetRevealDirection(dir);
        SetSoftness(softnessValue);
        SetProgress(progressValue);
    }

    /// <summary>
    /// 设置当前笔画的显示进度。
    /// </summary>
    /// <param name="value">显示进度，范围 0 到 1。</param>
    /// <returns>无。</returns>
    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        ApplyToMaterial();
    }

    /// <summary>
    /// 设置当前笔画的揭示方向。
    /// </summary>
    /// <param name="dir">揭示方向向量。</param>
    /// <returns>无。</returns>
    public void SetRevealDirection(Vector2 dir)
    {
        revealDirection = dir;
        ApplyToMaterial();
    }

    /// <summary>
    /// 设置当前笔画的边缘柔和度。
    /// </summary>
    /// <param name="value">柔和度数值。</param>
    /// <returns>无。</returns>
    public void SetSoftness(float value)
    {
        softness = Mathf.Max(0.0001f, value);
        ApplyToMaterial();
    }

    /// <summary>
    /// 确保当前 Image 持有独立的材质实例。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    public void EnsureMaterialInstance()
    {
        if (_image == null)
        {
            _image = GetComponent<Image>();
        }

        if (baseMaterial == null)
        {
            return;
        }

        if (_runtimeMaterial == null)
        {
            _runtimeMaterial = new Material(baseMaterial);
            _runtimeMaterial.name = baseMaterial.name + "_Runtime_" + gameObject.name;
            _image.material = _runtimeMaterial;
        }
    }

    /// <summary>
    /// 将当前参数写入材质实例。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    public void ApplyToMaterial()
    {
        if (_runtimeMaterial == null)
        {
            return;
        }

        Vector2 dir = revealDirection;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector2.right;
        }

        dir.Normalize();

        _runtimeMaterial.SetFloat(ProgressId, progress);
        _runtimeMaterial.SetVector(RevealDirId, new Vector4(dir.x, dir.y, 0f, 0f));
        _runtimeMaterial.SetFloat(SoftnessId, softness);
    }

    /// <summary>
    /// 销毁当前运行时材质实例。
    /// </summary>
    /// <param name="无">无。</param>
    /// <returns>无。</returns>
    private void DestroyRuntimeMaterial()
    {
        if (_runtimeMaterial == null)
        {
            return;
        }

        if (_image == null)
        {
            _image = GetComponent<Image>();
        }

        if (_image != null && _image.material == _runtimeMaterial)
        {
            _image.material = null;
        }

        if (Application.isPlaying)
        {
            Destroy(_runtimeMaterial);
        }
        else
        {
            DestroyImmediate(_runtimeMaterial);
        }

        _runtimeMaterial = null;
    }
}
