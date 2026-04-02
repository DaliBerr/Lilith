namespace Kernel.Bullet
{
    public class FireCoreTokenDef : BaseTokenData
    {
        public FireCoreTokenDef()
        {
            tokenName = "FireCore";
            description = "A core token for handling fire-based mechanics.";
            tokenType = TokenType.Core;
            attackSpec = new AttackSpec
            {
                coreType = AttackCoreType.Fire,
                behaviorType = AttackBehaviorType.Straight,
                valueType = AttackValueType.oneShot,
                resultType = AttackResultType.DirectDamage,
                damage = 1f,
                projectileCount = 1,
            };
        }
    }
}