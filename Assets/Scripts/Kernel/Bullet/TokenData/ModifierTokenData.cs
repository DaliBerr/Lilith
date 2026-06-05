using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 正式的法术修饰词元；使用 BaseTokenData.Modifiers 作为实际数值载荷，作用域由编译位置解析。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Modifier Token", fileName = "ModifierToken")]
    public class ModifierTokenData : BaseTokenData
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            SetTokenType(TokenType.Modifier);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetTokenType(TokenType.Modifier);
        }
    }
}
