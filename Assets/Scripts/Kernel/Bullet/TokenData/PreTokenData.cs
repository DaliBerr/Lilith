namespace Kernel.Bullet
{
    /// <summary>
    /// 预留给 Core 之前使用的前置修饰词元基类。
    /// </summary>
    public abstract class PreTokenData : BaseTokenData
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            SetTokenType(TokenType.Pre);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetTokenType(TokenType.Pre);
        }
    }
}
