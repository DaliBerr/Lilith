using Vocalith.Scribe;

/// <summary>
/// 用于将玩家残卷计数接入 Scribe 存档系统的适配器。
/// </summary>
public sealed class SavePlayerRemnants : ISaveItem
{
    public string TypeId => "PlayerRemnants";

    public int remnantCount = PlayerRemnantWallet.GetCurrentRemnants();

    /// <summary>
    /// summary: Scribe 回调，在存档/读档时序列化或反序列化残卷数量。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void ExposeData()
    {
        Scribe_Values.Look("remnantCount", ref remnantCount, 0);

        if (Scribe.mode == ScribeMode.Loading)
        {
            PlayerRemnantWallet.TrySetCurrentRemnants(remnantCount, out _);
        }
    }
}
