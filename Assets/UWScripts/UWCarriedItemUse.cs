using UWDataImport.UWData;

/// <summary>
/// USING A CARRIED THING - the one chain behind the classic panel's use click (UWItemDrag), the
/// modern bags' E and their menu's Use (UWModernBags) and the action bar's slots
/// (UWModernActionBar) (cleanup step 2.4, 2026-10-12; until then four copies: the classic
/// click's tail, UWItemDrag.UseCarriedItem, UWModernBags.TryUseAt and the menu, and the action
/// bar's own order).
///
/// THE CHAIN: the map opens (on a LEFT click the original opens the overview map in full
/// screen, with no message at all - per user, 2026-09-03); a book or scroll is read; what works
/// at once is used (food, potions, wands, lights, the bedroll ... - UWItemUse); what is applied
/// to something else asks for its target (UWItemTargetRules.Classify - the classic use mode
/// or the modern bags' use mode). What precedes it stays with each caller, as it differs by
/// screen: the classic trade, the rune bag's shelf, opening a container, putting on a thing to
/// wear.
///
/// WHAT IS LEFT WHEN NOTHING WAS USED differs by context: the classic left click toggles the
/// combat mode on the weapon hand; the bags' menu says "You cannot use that." (general message
/// 153, until 2026-10-12 an English literal of the same wording); the bags' E and the action
/// bar stay silent.
/// </summary>
public static class UWCarriedItemUse
{
    /// <summary>Where the use comes from.</summary>
    public enum Context
    {
        /// <summary>The classic panel's use click (backpack, open container, paperdoll).</summary>
        ClassicPanel,

        /// <summary>E over a slot of the modern bags or the character panel.</summary>
        ModernBags,

        /// <summary>The Use entry of the bags' context menu.</summary>
        ModernMenu,

        /// <summary>A slot of the modern action bar.</summary>
        ActionBar
    }

    /// <summary>
    /// Uses pOItem (null: an empty slot was clicked) from peContext through the item drag
    /// pODrag, the host of every use. True when something happened.
    /// </summary>
    public static bool Use(UWItemDrag pODrag, UWObject pOItem, Context peContext)
    {
        if (pODrag == null)
            return false;

        Interaction lOInteraction = UWScene.Interaction;
        bool lbModern = peContext != Context.ClassicPanel;
        UWItemTargetRules.TargetKind leTarget = UWItemTargetRules.Classify(pOItem, pODrag.GameData);

        // THE ACTION BAR'S TOOLS act on the crosshair's target at once (per user, 2026-10-03):
        // there is no pointer to choose a target with.
        if (peContext == Context.ActionBar && leTarget == UWItemTargetRules.TargetKind.World)
        {
            UWItemTargetUse.OnCrosshair(lOInteraction, pOItem);
            return true;
        }

        if (pOItem != null && (!lbModern || pODrag.HasInventory))
        {
            if (pOItem.ID == UWObjectMechanics.MapObjectId)
            {
                pODrag.OpenMap();
                return true;
            }

            if (lOInteraction != null && lOInteraction.TryReadBook(pOItem, false))
                return true;

            // Wand, light source and food take effect IMMEDIATELY - they need no target. Only
            // what is applied to something else, above all the key, goes into use mode (per
            // user, 2026-09-05).
            bool lbWasTargeting = lOInteraction != null && lOInteraction.IsSpellTargeting;

            if (pODrag.UseAtOnce(pOItem, lbModern))
            {
                // A wand's spell takes the modern aim as the runes' do (Interaction.AfterModernCast).
                if (lbModern && !lbWasTargeting && lOInteraction != null)
                    lOInteraction.AfterModernCast();

                return true;
            }
        }

        if (pOItem != null && leTarget != UWItemTargetRules.TargetKind.None)
        {
            // TAKING UP THE POLE is a status pass (UseUtil, USEITEMS.C: UsingPole, then
            // FixPlayerEquips), where a worn curse strikes (2026-10-13).
            if (pOItem.ID == UWObjectMechanics.PoleObjectId)
                UWScene.Character?.OnStatusPassDue();

            switch (peContext)
            {
                case Context.ClassicPanel:
                    pODrag.BeginUseMode(pOItem);
                    return true;

                case Context.ModernBags:
                case Context.ModernMenu:
                    if (UWModernBags.Instance != null)
                        UWModernBags.Instance.BeginUse(pOItem, false);

                    return true;
            }

            // The action bar's oil flask: its target is a carried thing, which the bar cannot
            // point at - nothing happens, as before.
            return false;
        }

        if (peContext == Context.ClassicPanel)
            pODrag.ToggleCombatFromClick();
        else if (peContext == Context.ModernMenu && lOInteraction != null)
            lOInteraction.AddGeneralMessage(UWClickRules.CannotUseThatMessage);

        return false;
    }
}
