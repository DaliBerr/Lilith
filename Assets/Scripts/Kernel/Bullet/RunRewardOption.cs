namespace Kernel.Bullet
{
    public enum RunRewardOptionKind
    {
        None = 0,
        Token = 1,
        SpellBook = 2,
    }

    /// <summary>
    /// 表示一次 Run 内选择界面可提供的奖励项。
    /// </summary>
    public readonly struct RunRewardOption
    {
        private RunRewardOption(RunRewardOptionKind kind, PlaceableTokenData token, SpellBookData spellBook)
        {
            Kind = kind;
            Token = token;
            SpellBook = spellBook;
        }

        public RunRewardOptionKind Kind { get; }
        public PlaceableTokenData Token { get; }
        public SpellBookData SpellBook { get; }

        public bool IsValid
        {
            get
            {
                return Kind switch
                {
                    RunRewardOptionKind.Token => Token != null,
                    RunRewardOptionKind.SpellBook => SpellBook != null,
                    _ => false,
                };
            }
        }

        public static RunRewardOption None => default;

        public static RunRewardOption FromToken(PlaceableTokenData token)
        {
            return token != null
                ? new RunRewardOption(RunRewardOptionKind.Token, token, null)
                : None;
        }

        public static RunRewardOption FromSpellBook(SpellBookData spellBook)
        {
            return spellBook != null
                ? new RunRewardOption(RunRewardOptionKind.SpellBook, null, spellBook)
                : None;
        }

        public string GetDisplayText()
        {
            return Kind switch
            {
                RunRewardOptionKind.Token => Token != null ? Token.GetPickupDisplayText() : string.Empty,
                RunRewardOptionKind.SpellBook => SpellBook != null ? SpellBook.DisplayName : string.Empty,
                _ => string.Empty,
            };
        }

        public string GetSelectionDescription()
        {
            return Kind switch
            {
                RunRewardOptionKind.Token => Token != null ? Token.GetSelectionDescription() : string.Empty,
                RunRewardOptionKind.SpellBook => SpellBook != null ? SpellBook.GetSelectionDescription() : string.Empty,
                _ => string.Empty,
            };
        }
    }
}
