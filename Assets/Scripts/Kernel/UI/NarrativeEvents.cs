namespace Kernel.UI
{
    public readonly struct NarrativeStartBattleRequestedEvent
    {
        public NarrativeStartBattleRequestedEvent(string entryId, string chapterId)
        {
            EntryId = entryId ?? string.Empty;
            ChapterId = chapterId ?? string.Empty;
        }

        public string EntryId { get; }
        public string ChapterId { get; }
    }
}
