namespace Kernel.Bullet
{
    public class IceCoreTokenDef : BaseTokenData
    {
        public IceCoreTokenDef()
        {
            tokenName = "IceCore";
            description = "A core token for handling ice-based mechanics.";
            tokenType = TokenType.Core;

            attackSpec = new AttackSpec
            {
                coreType = AttackCoreType.Ice,
                behaviorType = AttackBehaviorType.Straight,
                valueType = AttackValueType.oneShot,
                resultType = AttackResultType.DirectDamage,
                damage = 1f,
                projectileCount = 1,
            };
        }
    }
}