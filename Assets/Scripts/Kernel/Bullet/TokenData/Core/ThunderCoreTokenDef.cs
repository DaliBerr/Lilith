namespace Kernel.Bullet
{
    public class ThunderCoreTokenDef : BaseTokenData
    {
        public ThunderCoreTokenDef()
        {
            tokenName = "ThunderCore";
            description = "A core token for handling thunder-based mechanics.";
            tokenType = TokenType.Core;

            attackSpec = new AttackSpec
            {
                coreType = AttackCoreType.Thunder,
                behaviorType = AttackBehaviorType.Straight,
                valueType = AttackValueType.oneShot,
                resultType = AttackResultType.DirectDamage,
                damage = 1f,
                projectileCount = 1,
            };
        }
    }
}