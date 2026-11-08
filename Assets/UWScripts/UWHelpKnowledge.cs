using UWDataImport.UWData;

/// <summary>The help's line under a learned mantra or spell: where the character learned it (UWKnowledge,
/// GitHub issue 19, 2026-11-08).</summary>
public static class UWHelpKnowledge
{
    public static string SourceText(UWKnowledge.Entry pOEntry)
    {
        switch (pOEntry.Source)
        {
            case UWKnowledge.SourceEnum.Book: return UWText.T("help.knowledge.from_book");
            case UWKnowledge.SourceEnum.Writing: return UWText.T("help.knowledge.from_writing");
            case UWKnowledge.SourceEnum.Talk:
                // The talking door, a brilliant mage once (per user 2026-11-08; "Door" as kept before that).
                if (pOEntry.Who == UWKnowledgeSources.DoorWho || pOEntry.Who == "Door")
                    return UWText.T("help.knowledge.from_door");

                return string.IsNullOrEmpty(pOEntry.Who) ? UWText.T("help.knowledge.from_talk") : UWText.F("help.knowledge.from_named", pOEntry.Who);
            case UWKnowledge.SourceEnum.Chant: return UWText.T("help.knowledge.from_chant");
            case UWKnowledge.SourceEnum.Cast: return UWText.T("help.knowledge.from_cast");
            default: return string.Empty;
        }
    }
}
