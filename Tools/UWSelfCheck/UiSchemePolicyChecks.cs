using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.19 (2026-10-23): the decisions each interface scheme makes without an engine
    /// as UWUiSchemePolicy - the two tables, Classic and Modern, that IUWUiScheme hands the game
    /// logic in place of its own scheme tests. Every field of both is pinned: a value here changes
    /// what the player sees in one scheme, and no step intended that. Data-free.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckUiSchemePolicy()
        {
            Check.Group("UI scheme policy (UWUiSchemePolicy - cleanup step 5.19)");

            UWUiSchemePolicy lOClassic = UWUiSchemePolicy.Classic;
            UWUiSchemePolicy lOModern = UWUiSchemePolicy.Modern;

            Check.Bool("scheme policy: two different tables", ReferenceEquals(lOClassic, lOModern), false);

            // The prompts of the message scroll: the classic scheme answers in the scroll with the
            // original's input line, the modern one in its box (UWModernQuestion).
            Check.Bool("classic: the mantra line stands in the scroll", lOClassic.MantraLineInScroll, true);
            Check.Bool("modern: the box shows the mantra, not the scroll", lOModern.MantraLineInScroll, false);
            Check.Bool("classic: a prompt hides the pointer", lOClassic.PromptHidesPointer, true);
            Check.Bool("modern: the box shows the pointer", lOModern.PromptHidesPointer, false);
            Check.Bool("classic: a mouse button ends the mantra", lOClassic.MouseButtonEndsMantra, true);
            Check.Bool("modern: the box's OK and Cancel take the mouse", lOModern.MouseButtonEndsMantra, false);
            Check.Bool("classic: a cancelled mantra chants the dash", lOClassic.CancelledMantraChantsDash, true);
            Check.Bool("modern: a cancelled mantra leaves no trace", lOModern.CancelledMantraChantsDash, false);
            Check.Bool("classic: the pad's A and B answer yes/no", lOClassic.PadButtonsAnswerYesNo, true);
            Check.Bool("modern: the box is clicked with the pointer", lOModern.PadButtonsAnswerYesNo, false);
            Check.Bool("classic: the mouse buttons answer yes/no", lOClassic.MouseButtonsAnswerYesNo, true);
            Check.Bool("modern: the mouse buttons do not answer yes/no", lOModern.MouseButtonsAnswerYesNo, false);

            // The conversation.
            Check.Int("classic: answers 1 to 4", lOClassic.ConversationChoiceKeys, UWConversationView.ChoiceKeys(false));
            Check.Int("modern: answers 1 to 9", lOModern.ConversationChoiceKeys, UWConversationView.ChoiceKeys(true));
            Check.Bool("classic: the history waits with [MORE]", lOClassic.ConversationPausesForMore, true);
            Check.Bool("modern: the history scrolls", lOModern.ConversationPausesForMore, false);
            Check.Bool("classic: the right button chooses an answer too", lOClassic.ConversationRightClickChooses, true);
            Check.Bool("modern: the left button only", lOModern.ConversationRightClickChooses, false);
            Check.Bool("classic: the conversation is on the frame", lOClassic.ConversationOnFrame, true);
            Check.Bool("modern: the modern conversation draws it", lOModern.ConversationOnFrame, false);
            Check.Bool("classic: the typed answer stands on the scroll", lOClassic.AnswerTypedOnScroll, true);
            Check.Bool("modern: the typed answer has no scroll under it", lOModern.AnswerTypedOnScroll, false);

            // The world.
            Check.Bool("classic: the right button's gesture", lOClassic.WorldInput == UWWorldInputKind.RightButtonGesture, true);
            Check.Bool("modern: the keys and the free pointer", lOModern.WorldInput == UWWorldInputKind.KeysAndFreePointer, true);
            Check.Bool("classic: the right button attacks", lOClassic.RightButtonAttacks, true);
            Check.Bool("modern: the left button attacks", lOModern.RightButtonAttacks, false);
            Check.Bool("classic: a drag from the world picks up", lOClassic.WorldDragPicksUp, true);
            Check.Bool("modern: no drag from the world", lOModern.WorldDragPicksUp, false);
            Check.Bool("classic: the inventory is on the frame", lOClassic.InventoryOnFrame, true);
            Check.Bool("modern: the bags are the inventory", lOModern.InventoryOnFrame, false);
            Check.Bool("classic: a gift goes onto the pointer", lOClassic.PickupGoesToBags, false);
            Check.Bool("modern: a gift goes into the bags", lOModern.PickupGoesToBags, true);
            Check.Bool("classic: the key is applied by hand", lOClassic.KeyUsedAutomatically, false);
            Check.Bool("modern: the key is used automatically", lOModern.KeyUsedAutomatically, true);
            Check.Bool("classic: a trap is found in look mode only", lOClassic.TrapSearchNeedsLookMode, true);
            Check.Bool("modern: every look searches", lOModern.TrapSearchNeedsLookMode, false);
            Check.Bool("classic: the aim from the pointer in the view", lOClassic.AimSource == UWAimSource.PointerInView, true);
            Check.Bool("modern: the aim along a ray", lOModern.AimSource == UWAimSource.AimRay, true);
            Check.Bool("classic: the blow hits the area in front", lOClassic.MeleeByHitArea, true);
            Check.Bool("modern: the blow follows the ray", lOModern.MeleeByHitArea, false);
            Check.Bool("classic: a cast projectile waits for the click", lOClassic.ProjectileFliesWhenPointerLocked, false);
            Check.Bool("modern: a cast projectile flies when locked", lOModern.ProjectileFliesWhenPointerLocked, true);
            Check.Bool("classic: Escape does not end the Roaming Sight", lOClassic.RoamingSightEndsOnEscape, false);
            Check.Bool("modern: Escape ends the Roaming Sight", lOModern.RoamingSightEndsOnEscape, true);
            Check.Bool("classic: the pointer's thirds steer the sight", lOClassic.PointerThirdsSteerSight, true);
            Check.Bool("modern: the keys steer the sight", lOModern.PointerThirdsSteerSight, false);

            // The screen.
            Check.Bool("classic: the original's screen always", lOClassic.ClassicScreenOnlyUnderMap, false);
            Check.Bool("modern: the original's screen under the big map only", lOModern.ClassicScreenOnlyUnderMap, true);
            Check.Bool("classic: the eyes on the frame", lOClassic.EyesInHeadingStrip, false);
            Check.Bool("modern: the eyes in the heading strip", lOModern.EyesInHeadingStrip, true);
            Check.Bool("classic: a flash in the view window", lOClassic.FlashFillsScreen, false);
            Check.Bool("modern: a flash over the whole screen", lOModern.FlashFillsScreen, true);
            Check.Bool("classic: a cutscene in the view window", lOClassic.CutsceneInLeatherFrame, false);
            Check.Bool("modern: a cutscene in the leather frame", lOModern.CutsceneInLeatherFrame, true);
            Check.Bool("classic: the help beside the sliding frame", lOClassic.HelpOverPicture, false);
            Check.Bool("modern: the help over the picture", lOModern.HelpOverPicture, true);

            // The attack kind's sources, named for IUWUiScheme.AttackKindSource.
            Check.Int("attack kind: three sources", System.Enum.GetValues(typeof(UWAttackKindSource)).Length, 3);
        }
    }
}
