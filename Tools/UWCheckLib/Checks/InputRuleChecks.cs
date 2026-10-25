using System;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The click rules against the user's measurements on the original and the easy movement's
    /// arrows (UWClickRules, UWEasyMovement).
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// The click rules against the user's measurements on the original (2026-09-18 and 19),
        /// one check per measurement - so a change in UWClickRules that contradicts a measurement
        /// fails here instead of in the next play. Needs no data.
        /// </summary>
        private static void fCheckClickRules()
        {
            Console.WriteLine("Click rules");

            const bool Inside = true;
            const bool Outside = false;

            // The default state: look on the press, drag of a pickable at the threshold, use on
            // the release after a drag, nothing on a plain release.
            fExpectClick("default press on a thing looks", UWClickRules.OnPress(UWCommandMode.None, UWClickRules.TargetKind.Thing, Inside),
                UWClickRules.ActionKind.Look, UWClickRules.GestureKind.Default);
            fExpectClick("default press on nothing looks (You see nothing.)", UWClickRules.OnPress(UWCommandMode.None, UWClickRules.TargetKind.Nothing, Inside),
                UWClickRules.ActionKind.Look, UWClickRules.GestureKind.Default);
            fExpectClick("press over the UI does nothing", UWClickRules.OnPress(UWCommandMode.None, UWClickRules.TargetKind.Nothing, Outside),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Default);
            fExpectClick("default threshold on a pickable drags", UWClickRules.OnDragThreshold(UWClickRules.GestureKind.Default, UWClickRules.TargetKind.Pickable),
                UWClickRules.ActionKind.BeginDrag, UWClickRules.GestureKind.Default);
            fExpectClick("default threshold on a door waits", UWClickRules.OnDragThreshold(UWClickRules.GestureKind.Default, UWClickRules.TargetKind.Thing),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Default);
            fExpectClick("default release after a drag uses", UWClickRules.OnRelease(UWClickRules.GestureKind.Default, true, false),
                UWClickRules.ActionKind.Use, UWClickRules.GestureKind.None);
            fExpectClick("default release after a consumed drag does nothing", UWClickRules.OnRelease(UWClickRules.GestureKind.Default, true, true),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.None);
            fExpectClick("default release as a click does nothing", UWClickRules.OnRelease(UWClickRules.GestureKind.Default, false, false),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.None);

            // Talk: the creature is remembered, the release talks; anything else is refused on
            // the press - "You cannot talk to that." for geometry, "...that!" for a thing.
            fExpectClick("talk press on a creature remembers", UWClickRules.OnPress(UWCommandMode.Talk, UWClickRules.TargetKind.Creature, Inside),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Talk);
            fExpectClick("talk release talks", UWClickRules.OnRelease(UWClickRules.GestureKind.Talk, true, false),
                UWClickRules.ActionKind.Talk, UWClickRules.GestureKind.None);
            fExpectClick("talk press on a wall: You cannot talk to that.", UWClickRules.OnPress(UWCommandMode.Talk, UWClickRules.TargetKind.Geometry, Inside),
                UWClickRules.ActionKind.Message, UWClickRules.GestureKind.None, UWClickRules.CannotTalkToThatMessage);
            fExpectClick("talk press on a door asks the thing", UWClickRules.OnPress(UWCommandMode.Talk, UWClickRules.TargetKind.Thing, Inside),
                UWClickRules.ActionKind.TalkToThing, UWClickRules.GestureKind.None);
            fExpectClick("talk press on a pickable asks the thing", UWClickRules.OnPress(UWCommandMode.Talk, UWClickRules.TargetKind.Pickable, Inside),
                UWClickRules.ActionKind.TalkToThing, UWClickRules.GestureKind.None);

            // What the thing answers (TalkTo_ovr100_0): the shrine chants, Arial does not react,
            // any other special decoration says nothing, the rest refuses.
            fExpectBool("talk to a door: You cannot talk to that!",
                UWClickRules.TalkToThing(UWWorldCapture.FirstClosedDoorId, false) == UWClickRules.ThingTalkAnswer.CannotTalk, true);
            fExpectBool("talk to the shrine chants",
                UWClickRules.TalkToThing(UWShrineRules.ShrineObjectId, false) == UWClickRules.ThingTalkAnswer.ChantMantra, true);
            fExpectBool("talk to Arial: no reaction from the princess",
                UWClickRules.TalkToThing(UWClickRules.SpecialDecorationObjectId, true) == UWClickRules.ThingTalkAnswer.NoReactionFromPrincess, true);
            fExpectBool("talk to another special decoration says nothing",
                UWClickRules.TalkToThing(UWClickRules.SpecialDecorationObjectId, false) == UWClickRules.ThingTalkAnswer.Silent, true);
            fExpectBool("talk to the plain decoration (367) refuses",
                UWClickRules.TalkToThing(UWClickRules.SpecialDecorationObjectId + 1, true) == UWClickRules.ThingTalkAnswer.CannotTalk, true);

            // Get: the press decides at once.
            fExpectClick("get press on a pickable takes it to the pointer", UWClickRules.OnPress(UWCommandMode.Get, UWClickRules.TargetKind.Pickable, Inside),
                UWClickRules.ActionKind.TakeToPointer, UWClickRules.GestureKind.None);
            fExpectClick("get press on a wall: Nothing to get.", UWClickRules.OnPress(UWCommandMode.Get, UWClickRules.TargetKind.Geometry, Inside),
                UWClickRules.ActionKind.Message, UWClickRules.GestureKind.None, UWClickRules.NothingToGetMessage);
            fExpectClick("get press on a creature: You cannot pick that up.", UWClickRules.OnPress(UWCommandMode.Get, UWClickRules.TargetKind.Creature, Inside),
                UWClickRules.ActionKind.Message, UWClickRules.GestureKind.None, UWClickRules.CannotPickThatUpMessage);
            fExpectClick("get press on a door: You cannot pick that up.", UWClickRules.OnPress(UWCommandMode.Get, UWClickRules.TargetKind.Thing, Inside),
                UWClickRules.ActionKind.Message, UWClickRules.GestureKind.None, UWClickRules.CannotPickThatUpMessage);

            // Look: the look on the press, and that is all.
            fExpectClick("look press looks and ends the gesture", UWClickRules.OnPress(UWCommandMode.Look, UWClickRules.TargetKind.Pickable, Inside),
                UWClickRules.ActionKind.Look, UWClickRules.GestureKind.None);

            // Use: geometry is refused, a creature ignored, a thing remembered and used on the release.
            fExpectClick("use press on a wall: You cannot use that.", UWClickRules.OnPress(UWCommandMode.Use, UWClickRules.TargetKind.Geometry, Inside),
                UWClickRules.ActionKind.Message, UWClickRules.GestureKind.None, UWClickRules.CannotUseThatMessage);
            fExpectClick("use press on a creature does nothing", UWClickRules.OnPress(UWCommandMode.Use, UWClickRules.TargetKind.Creature, Inside),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.None);
            fExpectClick("use press on a door remembers", UWClickRules.OnPress(UWCommandMode.Use, UWClickRules.TargetKind.Thing, Inside),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Use);
            fExpectClick("use threshold does not drag", UWClickRules.OnDragThreshold(UWClickRules.GestureKind.Use, UWClickRules.TargetKind.Pickable),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Use);
            fExpectClick("use release uses, wherever the pointer is", UWClickRules.OnRelease(UWClickRules.GestureKind.Use, false, false),
                UWClickRules.ActionKind.Use, UWClickRules.GestureKind.None);
            fExpectClick("a mode changes nothing over the UI", UWClickRules.OnPress(UWCommandMode.Use, UWClickRules.TargetKind.Nothing, Outside),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Default);

            // Which button carries the use over the UI.
            fExpectBool("left click over the UI uses", UWClickRules.IsUseClick(false, UWCommandMode.None, false), true);
            fExpectBool("right click over the UI looks", UWClickRules.IsUseClick(true, UWCommandMode.None, false), false);
            fExpectBool("right click over the UI uses in use mode", UWClickRules.IsUseClick(true, UWCommandMode.Use, false), true);
            fExpectBool("right click in a conversation looks even in use mode", UWClickRules.IsUseClick(true, UWCommandMode.Use, true), false);

            // The icons: the lit one switches off, another replaces it.
            fExpectBool("clicking the lit icon switches the mode off", UWClickRules.NextMode(UWCommandMode.Talk, UWCommandMode.Talk) == UWCommandMode.None, true);
            fExpectBool("clicking another icon replaces the mode", UWClickRules.NextMode(UWCommandMode.Talk, UWCommandMode.Look) == UWCommandMode.Look, true);
            fExpectBool("fight is not a mode here", UWClickRules.NextMode(UWCommandMode.Talk, UWCommandMode.Fight) == UWCommandMode.Talk, true);

            // The panel's areas (seg024_24DC_26F and the handlers, read 2026-09-26).
            fExpectBool("command column: the bottom 16 rows are use", UWClickRules.CommandIconAt(15) == UWCommandMode.Use, true);
            fExpectBool("command column: from 16 up fight", UWClickRules.CommandIconAt(16) == UWCommandMode.Fight, true);
            fExpectBool("command column: talk at 70 to 87", UWClickRules.CommandIconAt(70) == UWCommandMode.Talk && UWClickRules.CommandIconAt(87) == UWCommandMode.Talk, true);
            fExpectBool("command column: 88 to 105 the options", UWClickRules.CommandIconAt(105) == UWCommandMode.Options, true);
            fExpectBool("command column: above that nothing", UWClickRules.CommandIconAt(106) == UWCommandMode.None, true);
            fExpectBool("active spells: the first slot on the right", UWClickRules.ActiveSpellSlotAt(47) == 0 && UWClickRules.ActiveSpellSlotAt(32) == 0, true);
            fExpectBool("active spells: the third on the left", UWClickRules.ActiveSpellSlotAt(0) == 2, true);
            fExpectBool("active spells: the last three columns answer nothing", UWClickRules.ActiveSpellSlotAt(48) < 0, true);
            fExpectBool("flasks: vitality left, mana right", UWClickRules.FlaskAt(25, 0) == UWClickRules.FlaskPart.Vitality && UWClickRules.FlaskAt(40, 30) == UWClickRules.FlaskPart.Mana, true);
            fExpectBool("flasks: above row 30 nothing", UWClickRules.FlaskAt(10, 31) == UWClickRules.FlaskPart.None, true);
            fExpectBool("flasks: the chain above row 13 turns the panel", UWClickRules.FlaskAt(26, 14) == UWClickRules.FlaskPart.Chain && UWClickRules.FlaskAt(39, 36) == UWClickRules.FlaskPart.Chain, true);
            fExpectBool("flasks: the chain's foot does nothing", UWClickRules.FlaskAt(30, 13) == UWClickRules.FlaskPart.None, true);
            fExpectBool("poison degree: none, barely, egregiously", UWClickRules.PoisonDegree(0) == -1 && UWClickRules.PoisonDegree(3) == 0 && UWClickRules.PoisonDegree(15) == 4, true);
            fExpectBool("areas include both ends", UWClickRules.RuneShelf.Contains(176, 45) && UWClickRules.RuneShelf.Contains(222, 61) && !UWClickRules.RuneShelf.Contains(223, 61), true);
            fExpectBool("inventory: the helmet at the top middle", UWClickRules.InventorySpotAt(277, 199 - 15) == UWClickRules.InventorySpot.Helmet, true);
            fExpectBool("inventory: row 55 is the legs, not the gloves (first record wins)", UWClickRules.InventorySpotAt(275, 199 - 55) == UWClickRules.InventorySpot.Legs, true);
            fExpectBool("inventory: rows 52-53 on the left are the hand, not the ring", UWClickRules.InventorySpotAt(250, 199 - 53) == UWClickRules.InventorySpot.HandLeft, true);
            fExpectBool("inventory: backpack top left and bottom right", UWClickRules.BackpackSlotOf(UWClickRules.InventorySpotAt(240, 199 - 81)) == 0 && UWClickRules.BackpackSlotOf(UWClickRules.InventorySpotAt(314, 199 - 116)) == 7, true);
            fExpectBool("inventory: the gap between two backpack slots answers nothing", UWClickRules.InventorySpotAt(258, 199 - 90) == UWClickRules.InventorySpot.None, true);
            fExpectBool("inventory: container icon and the two arrows", UWClickRules.InventorySpotAt(248, 199 - 70) == UWClickRules.InventorySpot.ContainerIcon && UWClickRules.InventorySpotAt(300, 199 - 75) == UWClickRules.InventorySpot.ScrollLeft && UWClickRules.InventorySpotAt(310, 199 - 75) == UWClickRules.InventorySpot.ScrollRight, true);

            Console.WriteLine();
        }

        private static void fExpectClick(string psName, UWClickRules.Decision pODecision,
            UWClickRules.ActionKind peAction, UWClickRules.GestureKind peGesture, int piMessage = 0)
        {
            bool lbOk = pODecision.Action == peAction && pODecision.Gesture == peGesture
                && (piMessage == 0 || pODecision.MessageIndex == piMessage);

            if (lbOk)
                fPass(psName, pODecision.ToString());
            else
                fFail(psName, "got " + pODecision + ", expected " + peAction + (piMessage != 0 ? " " + piMessage : "") + ", gesture " + peGesture);
        }

        /// <summary>
        /// The easy movement, built 2026-09-21: seg008_1B2A_216 (55427-55876) behind the three
        /// arrows under the compass and, in the original, the uppercase codes of A, D, S, W
        /// and X.
        /// </summary>
        private static void fCheckEasyMovement()
        {
            Check.Group("Easy movement");

            fExpectInt("easy: an eighth of the circle is 0x2000", UWEasyMovement.EighthStep, 0x2000);
            fExpectInt("easy: a tile is 0x100 of PLAYER.DAT 0x54", UWEasyMovement.UnitsPerTile, 0x100);
            fExpectBool("easy: the step forward is half a tile",
                UWEasyMovement.ForwardDistance * 2 == UWEasyMovement.UnitsPerTile, true);
            fExpectBool("easy: the step back is a quarter",
                UWEasyMovement.BackwardDistance * 4 == UWEasyMovement.UnitsPerTile, true);

            // From a 45 degree line it simply turns by an eighth, both ways and around zero.
            fExpectInt("easy: north turns right to the next eighth",
                UWEasyMovement.Turn(0, UWEasyMovement.CommandTurnRight), 0x2000);
            fExpectInt("easy: north turns left over zero",
                UWEasyMovement.Turn(0, UWEasyMovement.CommandTurnLeft), 0xE000);
            fExpectInt("easy: and back again",
                UWEasyMovement.Turn(0xE000, UWEasyMovement.CommandTurnRight), 0);

            // Off a line the first click SNAPS - forward onto the next, back onto the last.
            fExpectInt("easy: off the line the right turn snaps to the next",
                UWEasyMovement.Turn(0x2500, UWEasyMovement.CommandTurnRight), 0x4000);
            fExpectInt("easy: and the left turn back to the last",
                UWEasyMovement.Turn(0x2500, UWEasyMovement.CommandTurnLeft), 0x2000);
            fExpectInt("easy: one unit short of a line still snaps up",
                UWEasyMovement.Turn(0x1FFF, UWEasyMovement.CommandTurnRight), 0x2000);
            fExpectInt("easy: and down to zero",
                UWEasyMovement.Turn(0x1FFF, UWEasyMovement.CommandTurnLeft), 0);

            // Which command carries how far.
            fExpectInt("easy: the middle arrow steps forward",
                UWEasyMovement.GetStepDistance(UWEasyMovement.CommandStepForward), 0x80);
            fExpectInt("easy: the W command goes the same distance",
                UWEasyMovement.GetStepDistance(UWEasyMovement.CommandRunForward), 0x80);
            fExpectInt("easy: backward is half of that",
                UWEasyMovement.GetStepDistance(UWEasyMovement.CommandStepBack), 0x40);
            fExpectInt("easy: a turn carries nothing",
                UWEasyMovement.GetStepDistance(UWEasyMovement.CommandTurnLeft), 0);

            // Backwards is the yaw turned by half a circle.
            fExpectInt("easy: the step back goes the other way",
                UWEasyMovement.GetStepYaw(0x2000, UWEasyMovement.CommandStepBack), 0xA000);
            fExpectInt("easy: forward keeps the yaw",
                UWEasyMovement.GetStepYaw(0x2000, UWEasyMovement.CommandStepForward), 0x2000);

            fExpectInt("easy: the flag of the tile check is Levitate and Fly",
                UWEasyMovement.FloatingAbilities,
                UWPlayerVitals.LevitateBit | UWPlayerVitals.FlyBit);

            // What the flag does: step off a ledge. The user found it by trying in the
            // original, 2026-09-21 - Shift W falls, Shift S does not.
            fExpectBool("easy: the W command may step off a ledge",
                UWEasyMovement.AllowsDrop(UWEasyMovement.CommandRunForward), true);
            fExpectBool("easy: the careful step may not",
                UWEasyMovement.AllowsDrop(UWEasyMovement.CommandStepForward), false);
            fExpectBool("easy: going backwards may not either",
                UWEasyMovement.AllowsDrop(UWEasyMovement.CommandStepBack), false);
            fExpectInt("easy: without it the ground may be one floor step lower",
                UWEasyMovement.DropAllowanceZPos, 8);

            fExpectInt("easy: one step is as loud as the base plus four",
                UWCritterRules.PlayerNoiseEasyMove, 4);

            // Holding repeats. The pace is ours, not the original's - a quarter of a second
            // after the user tried it out (2026-09-21).
            fExpectBool("easy: the held key repeats every 250 ms by default",
                Math.Abs(UWEasyMovement.DefaultRepeatSeconds - 0.25f) < 0.0001f, true);
            fExpectBool("easy: and the default sits on the arrows' grid",
                Math.Abs(Math.Round(UWEasyMovement.DefaultRepeatSeconds / UWEasyMovement.RepeatStepSeconds)
                    * UWEasyMovement.RepeatStepSeconds - UWEasyMovement.DefaultRepeatSeconds) < 0.0001f, true);
            fExpectBool("easy: and the slider stays inside its ends",
                UWEasyMovement.MinRepeatSeconds < UWEasyMovement.DefaultRepeatSeconds
                    && UWEasyMovement.DefaultRepeatSeconds < UWEasyMovement.MaxRepeatSeconds, true);
        }
    }
}
