

namespace Kernel.Bullet
{
    public abstract class BaseTokenData
    {
        public string tokenName { get; set; }
        public string description { get; set; }
        public TokenType tokenType { get; set; }
        public AttackSpec attackSpec { get; set; }
    }
    public enum TokenType
    {
        None = 0,
        Pre = 1,
        Core = 2,
        Behavior = 3,
        Value = 4,
        Result = 5,
        Post = 6,
    }
    
}