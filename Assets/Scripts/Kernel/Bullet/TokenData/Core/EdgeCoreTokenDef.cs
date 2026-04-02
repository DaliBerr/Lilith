namespace Kernel.Bullet
{
    public class EdgeCoreTokenDef : BaseTokenData
    {
        public EdgeCoreTokenDef()
        {
            tokenName = "EdgeCore";
            description = "A core token for handling edge-based mechanics.";
            tokenType = TokenType.Core;
      
            attackSpec = new AttackSpec
            {
                coreType = AttackCoreType.Edge,
                behaviorType = AttackBehaviorType.Straight,
                valueType = AttackValueType.oneShot,
                resultType = AttackResultType.DirectDamage,
                damage = 1f,
                projectileCount = 1,
            };
        }
    }
}