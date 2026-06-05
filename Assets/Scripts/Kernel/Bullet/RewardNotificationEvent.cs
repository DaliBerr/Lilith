namespace Kernel.Bullet
{
    public enum RewardNotificationKind
    {
        Item = 0,
        Token = 1,
        SpellBook = 2,
    }

    /// <summary>
    /// 表示一次玩家成功获得奖励后需要在 HUD 上展示的轻量通知。
    /// </summary>
    public readonly struct RewardNotificationEvent
    {
        private const string TokenFallbackDescription = "已收入背包";
        private const string SpellBookFallbackDescription = "已装备";
        private const string ItemFallbackDescription = "已获得";

        public readonly string title;
        public readonly string description;
        public readonly RewardNotificationKind kind;

        public RewardNotificationEvent(string title, string description, RewardNotificationKind kind)
        {
            this.title = title ?? string.Empty;
            this.description = description ?? string.Empty;
            this.kind = kind;
        }

        public static RewardNotificationEvent FromToken(PlaceableTokenData token)
        {
            string title = token != null ? token.GetPickupDisplayText() : string.Empty;
            string description = token != null ? token.GetSelectionDescription() : string.Empty;
            return new RewardNotificationEvent(title, ResolveFallback(description, TokenFallbackDescription), RewardNotificationKind.Token);
        }

        public static RewardNotificationEvent FromSpellBook(SpellBookData spellBook)
        {
            string title = spellBook != null ? spellBook.DisplayName : string.Empty;
            string description = spellBook != null ? spellBook.GetSelectionDescription() : string.Empty;
            return new RewardNotificationEvent(title, ResolveFallback(description, SpellBookFallbackDescription), RewardNotificationKind.SpellBook);
        }

        public static RewardNotificationEvent FromItem(PlaceableTokenData item, string descriptionOverride = null)
        {
            string title = item != null ? item.GetPickupDisplayText() : string.Empty;
            string description = !string.IsNullOrWhiteSpace(descriptionOverride)
                ? descriptionOverride
                : item != null ? item.GetSelectionDescription() : string.Empty;
            return new RewardNotificationEvent(title, ResolveFallback(description, ItemFallbackDescription), RewardNotificationKind.Item);
        }

        private static string ResolveFallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
