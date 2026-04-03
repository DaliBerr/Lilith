namespace Kernel.Bullet
{
    /// <summary>
    /// 预留给 Result 之后使用的后置修饰词元基类。
    /// </summary>
    public abstract class PostTokenData : BaseTokenData
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            SetTokenType(TokenType.Post);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetTokenType(TokenType.Post);
        }
    }
}
