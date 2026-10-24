/// <summary>
/// ONE ENTRY PER GAME ACTION (cleanup 5.17, 2026-10-23; D37 of the cleanup reviews): the same
/// action was started from several places, each calling into its executor on its own - camp from
/// two key routings and the modern Sleep button, Track likewise, the cast of the hollow from F8,
/// the pad's LT, the classic hollow, the modern rune hollow and the rune panel's hollow (through
/// two names, CastByKey and CastShelf), the status from the classic compass and the modern
/// heading. Each trigger now calls its action here, and the action calls its one executor.
///
/// Only the start is shared: every trigger keeps its own guards where they differ (the classic
/// keys are refused while the view roams, the panel's buttons not with a thing on the pointer, the
/// hollow's click not while the view roams), and the executors keep their rules, messages and the
/// order of their random draws - nothing of what an action does moved.
/// </summary>
public sealed class UWGameActions
{
    public UWGameActions(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private readonly UWGameUI mOUi;

    /// <summary>Whether camp can be made here at all: the item drag that makes it is in the scene
    /// (the triggers' own test before Camp, kept as each wrote it).</summary>
    public static bool CanCamp => UWScene.ItemDrag != null;

    /// <summary>
    /// MAKE CAMP: F10 in both schemes (UWGameUI's key routing) and the Sleep button beside the
    /// modern paperdoll's feet (UWModernPanel) - with a bedroll carried, else on the ground
    /// (UWItemDrag.CampByKey, UWSleep).
    /// </summary>
    public void Camp()
    {
        UWItemDrag lODrag = UWScene.ItemDrag;

        if (lODrag != null)
            lODrag.CampByKey();
    }

    /// <summary>
    /// TRACK: F9 in both schemes (UWGameUI's key routing) and the Track button beside the modern
    /// paperdoll's feet (UWModernPanel) - the Detect Monsters search with the player's Track skill
    /// (UWHudRunes.TrackByKey, UWMiscSpellRules.Track).
    /// </summary>
    public void Track()
    {
        mOUi.Runes.TrackByKey();
    }

    /// <summary>
    /// CAST WHAT LIES IN THE HOLLOW: F8 (the classic key routing), the gamepad's LT tapped
    /// (UWModernRunePanel), a click into the classic hollow (UWHudRunes.fUpdateSpellCast), into the
    /// modern rune hollow (UWModernHudInput) and into the rune panel's hollow (UWModernRunePanel) -
    /// the original's one handler for the click and the key (PlayerAttemptsSpellCast;
    /// UWHudRunes.CastShelf, UWSpellCasting.CastRunes).
    /// </summary>
    public void CastShelf()
    {
        mOUi.Runes.CastShelf();
    }

    /// <summary>
    /// CAST A RUNE SEQUENCE without touching the hollow: the modern action bar's spell slot
    /// (UWModernActionBar; UWHudRunes.CastSequence, by the same rules as the hollow).
    /// </summary>
    public void CastSequence(int piSequence)
    {
        mOUi.Runes.CastSequence(piSequence);
    }

    /// <summary>
    /// THE STATUS (hunger, fatigue, level, day, time of day): a click on the classic compass
    /// (UWClassicWorldGesture.TryReportStatus), on the modern heading strip or the original's
    /// compass in the modern scheme (UWModernHudInput) - UWHudCompass.ReportStatus.
    /// </summary>
    public void ReportStatus()
    {
        mOUi.Compass.ReportStatus();
    }
}
