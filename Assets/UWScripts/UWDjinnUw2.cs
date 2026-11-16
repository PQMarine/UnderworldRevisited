using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// do_filanium's engine side (UWDjinnRulesUw2.OilInMud): both water landings call it, as the original's mob_to_static
/// and obj_deal do - UWMotionHost.Splash for a thrown object at rest in water, UWWorldEdit.PutInWater for an object
/// put down on a water tile (UWLevelLoader.fPutInWater, after its splash). Only the second game has the record it needs;
/// the message goes to its scroll.
/// </summary>
public static class UWDjinnUw2
{
    public static void OilInWater(UWLevelLoader pOLoader, UWObject pOObject, int piTileX, int piTileY)
    {
        DataImport lOData = pOLoader != null ? pOLoader.UWDataImporter : null;
        UWPlayerFile lOFile = lOData != null && lOData.Session != null ? lOData.Session.PlayerFileUw2 : null;
        UWLevel lOLevel = lOFile != null ? pOLoader.WorldEdit.Level : null;
        UWTile lOTile = lOLevel != null ? lOLevel.GetTile(piTileX, piTileY) : null;

        if (lOTile == null || !UWDjinnRulesUw2.OilInMud(pOObject, lOTile.TextureFloor, lOLevel.Masterlist, lOFile))
            return;

        UWWorld lOWorld = UWWorld.Of(pOLoader);
        IUWPresentation lOScroll = UWSleepUw2.Current != null ? UWSleepUw2.Current.ScrollPresentation(lOWorld)
            : lOWorld != null ? lOWorld.Presentation : null;

        if (lOScroll != null)
            lOScroll.AddGeneralMessage(UWDjinnRulesUw2.OilPermeatesString);
    }

    /// <summary>The flash's length, as the second game's trap flashes (UWTrapRulesUw2).</summary>
    private const float ReleaseFlashSeconds = 0.25f;

    /// <summary>
    /// remove_object's bottle branch (UWDjinnRulesUw2.BottleBroken) for a thing destroyed on square piTileX/piTileY of the
    /// current level - the thrown thing's impact (UWMissiles.BecomeDebrisInFlight) and the blast (UWTileBlast). Before
    /// step 5 the daemon rends the player; from 5 the view flashes, and at the Sigil of Binding the daemon is absorbed.
    /// Absorbed: the bottle is gone; Rends and Wasted: the caller makes plain debris (no fire branch, no smoke);
    /// NotTheBottle: anything else, as before.
    /// </summary>
    public static UWDjinnRulesUw2.BottleOutcome BottleBroken(UWLevelLoader pOLoader, int piItemId, int piTileX, int piTileY)
    {
        if (piItemId != UWDjinnRulesUw2.BottleId || pOLoader == null)
            return UWDjinnRulesUw2.BottleOutcome.NotTheBottle;

        DataImport lOData = pOLoader.UWDataImporter;
        UWPlayerFile lOFile = lOData != null && lOData.Session != null ? lOData.Session.PlayerFileUw2 : null;
        UWDjinnRulesUw2.BottleOutcome leOutcome = UWDjinnRulesUw2.BottleBroken(piItemId, lOFile, pOLoader.CurrentLevelIndex + 1, piTileX, piTileY);

        if (leOutcome == UWDjinnRulesUw2.BottleOutcome.NotTheBottle)
            return leOutcome;

        UWWorld lOWorld = UWWorld.Of(pOLoader);
        IUWPresentation lOScroll = UWSleepUw2.Current != null && lOWorld != null ? UWSleepUw2.Current.ScrollPresentation(lOWorld)
            : lOWorld != null ? lOWorld.Presentation : null;

        if (leOutcome == UWDjinnRulesUw2.BottleOutcome.Rends)
        {
            if (lOScroll != null)
                lOScroll.AddGeneralMessage(UWDjinnRulesUw2.DaemonRendsString);

            if (lOWorld != null)
                lOWorld.Damage(UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.Plain(UWDjinnRulesUw2.RendDamage, UWDamageTypes.None)));

            return leOutcome;
        }

        if (lOScroll != null)
            lOScroll.FlashWindow(UWDjinnRulesUw2.ReleaseFlashColour, ReleaseFlashSeconds);

        if (leOutcome == UWDjinnRulesUw2.BottleOutcome.Absorbed && lOScroll != null)
            lOScroll.AddGeneralMessage(UWDjinnRulesUw2.DaemonAbsorbedString);

        return leOutcome;
    }
}
