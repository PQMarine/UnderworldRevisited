using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The Unity side of UWSpillRules: puts the whole chain down around a point. THE ONE SCATTER CALL
/// SITE for a container's contents and a dead creature's pack (cleanup step 2.6, 2026-10-12;
/// until then Interaction.SpillWorldContainer and UWCritterRemains.fDropLoot each had the loop
/// and the scatter call). Since decision 25 the same evening a container keeps nothing either.
/// </summary>
public static class UWSpill
{
    /// <summary>
    /// Every object of the chain loses its link and goes down 6 eighths around the point
    /// (drop_link_chain's put_at, UWScatterRules.LootRadius), the open tile as the fallback when
    /// even the point is refused; the chain is cut. Returns how many went down.
    /// </summary>
    /// <param name="pbAvoidWorldContainers">A spot must keep clear of the barrels and chests
    /// of its tile (per user, 2026-09-16: nothing from a spilled barrel comes to rest on the
    /// next one); a creature's pack is not held to that.</param>
    public static int Apply(UWLevelLoader pOLoader, UWObject pOContainer, List<UWObject> pOSpilled,
        Vector3 pOCentre, UWTilePos pOFallbackTile, bool pbAvoidWorldContainers)
    {
        if (pOLoader == null || pOContainer == null || pOContainer.Contents == null || pOSpilled == null)
            return 0;

        foreach (UWObject lOItem in pOSpilled)
        {
            // THE OWNER STAYS. The reference clears it while spilling, but in the original the
            // spilled contents still read "belonging to a green goblin" (per user with a
            // screenshot, 2026-09-16) - taking them is still a theft.
            lOItem.Link = 0;
            pOLoader.SpawnScatteredAround(lOItem, pOCentre, UWScatterRules.LootRadius, pOFallbackTile,
                pbAvoidWorldContainers);
        }

        // THE CHAIN IS CUT (drop_link_chain: cont->ol.f.link = 0) - the head in the container's
        // own link field too, not only the loaded Contents list. The pick-up trigger is found
        // through that field (UWTrapRules.TryFirePickUpTrigger, GetLinkedObject), and leaving it
        // made the emptied pouch of level 3 still send its headless (per user, 2026-10-12).
        pOContainer.Contents.Clear();

        if (!pOContainer.HasQuantity)
            pOContainer.Quantity = 0;

        return pOSpilled.Count;
    }
}
