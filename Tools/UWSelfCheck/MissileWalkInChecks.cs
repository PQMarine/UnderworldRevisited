using System.Linq;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// WALKING INTO A MISSILE (2026-10-19, the user's decision "as the original"): a creature's or
    /// the player's own motion step meets a hanging or slow missile, and do_objhit uses the usable
    /// missile on the mover (UseOnMover: missile_newhit with the mover as what it hit), then
    /// bounce_obj pushes the missile on in the mover's heading at the speed 0xEB with the vertical
    /// speed scaled by the masses - a missile of no mass is not pushed (UnderworldExhumed
    /// motion/OBJPHYS.C:69-95, 141-200). Real steps on the small map of the motion checks, the push's
    /// write-back into the flight's record, and the reach that ends the player's touch.
    /// </summary>
    public static partial class Program
    {
        private const int ArrowKind = 0x12;

        private const int MagicMissileKind = 0x17;

        private const int HangingMissileIndex = 1030;

        private static void fCheckMissileWalkIn(DataImport pOData)
        {
            Check.Group("Missile walk-in (do_objhit with the missile as the struck object, bounce_obj)");

            fCheckFlightPush();
            fCheckTouchReach();

            // EVERY MEETING STRIKES, at a fixed rate (2026-10-19, per user on the original: pressing
            // against a frozen arrow hurt again and again).
            Check.Bool("walk-in: the first meeting strikes", UWMissileHitRules.WalkInHitDue(1000, -1), true);
            Check.Bool("walk-in: ... not again within 1/32 s", UWMissileHitRules.WalkInHitDue(1007, 1000), false);
            Check.Bool("walk-in: ... again after 8 PIT ticks", UWMissileHitRules.WalkInHitDue(1008, 1000), true);

            if (pOData == null || pOData.CommonObjectProperties == null || pOData.ObjectClassProperties == null)
            {
                fFail("missile walk-in", "no COMOBJ.DAT or OBJECTS.DAT");

                return;
            }

            UWCommonObjectProperties.Entry lOArrow;
            UWCommonObjectProperties.Entry lOMagic;

            pOData.CommonObjectProperties.TryGet(ArrowKind, out lOArrow);
            pOData.CommonObjectProperties.TryGet(MagicMissileKind, out lOMagic);
            fExpectBool("walk-in: the arrow is usable and has mass, the magic missile is usable without",
                lOArrow.UsedWhenThrown && lOArrow.MassTenthStones > 0 && lOMagic.UsedWhenThrown && lOMagic.MassTenthStones == 0, true);

            fCheckCreatureWalksIntoMissile(pOData);
            fCheckPlayerWalksIntoMissile(pOData);
        }

        /// <summary>set_phys_data on a mobile: byte 9 the heading, the speed byte 0xEB / 0x2F, the
        /// pitch field 16 + vz / 64 clamped; the gravity bit and the position stay.</summary>
        private static void fCheckFlightPush()
        {
            UWMobileRecord lORecord = new UWMobileRecord
            {
                HeadingByte = 0x80, SpeedByte = 0x14, VzField = 10, GravityBit = true, TileX = 8, TileY = 9, XPos = 3, YPos = 2, ZPos = 26
            };

            UWMobileObjectMotion.Push(lORecord, 0x4000, UWCreatureMotion.PushSpeed, 0x80);
            fExpectInt("push: the flight takes the pusher's heading byte", lORecord.HeadingByte, 0x40);
            fExpectInt("push: ... the speed byte 0xEB / 0x2F = 5", lORecord.SpeedByte, 5);
            fExpectInt("push: ... the pitch field 16 + 0x80 / 64", lORecord.VzField, 18);
            fExpectBool("push: ... keeps its gravity bit and its place",
                lORecord.GravityBit && lORecord.TileY == 9 && lORecord.XPos == 3 && lORecord.YPos == 2 && lORecord.ZPos == 26, true);

            UWMobileObjectMotion.Push(lORecord, 0, UWCreatureMotion.PushSpeed, -5000);
            fExpectInt("push: a steep fall clamps the pitch field at 0", lORecord.VzField, 0);
            UWMobileObjectMotion.Push(lORecord, 0, UWCreatureMotion.PushSpeed, 5000);
            fExpectInt("push: ... a steep climb at 31", lORecord.VzField, 31);
        }

        /// <summary>The player (radius 2, 23 high, feet 16) and a missile of radius 1 and height 0:
        /// the reach is 2 + 1 + 1 eighths across, the heights must overlap.</summary>
        private static void fCheckTouchReach()
        {
            fExpectBool("touch: four eighths off is still the touch",
                UWMissileHitRules.InTouchReach(68, 68, 16, 2, 23, 72, 68, 26, 1, 0), true);
            fExpectBool("touch: ... five eighths off ends it",
                UWMissileHitRules.InTouchReach(68, 68, 16, 2, 23, 73, 68, 26, 1, 0), false);
            fExpectBool("touch: ... diagonally four and four still touches",
                UWMissileHitRules.InTouchReach(68, 68, 16, 2, 23, 64, 64, 26, 1, 0), true);
            fExpectBool("touch: a missile above the head is out of reach",
                UWMissileHitRules.InTouchReach(68, 68, 16, 2, 23, 69, 68, 40, 1, 0), false);
            fExpectBool("touch: ... one at the feet is in it (a point one unit high)",
                UWMissileHitRules.InTouchReach(68, 68, 16, 2, 23, 69, 68, 15, 1, 0), true);
            fExpectBool("touch: ... one below the feet is not",
                UWMissileHitRules.InTouchReach(68, 68, 16, 2, 23, 69, 68, 14, 1, 0), false);
        }

        private static int fCount(FakeMotionWorld pOWorld, string psLine)
        {
            return pOWorld.Log.Count(lsLine => lsLine == psLine);
        }

        private static bool fAnyPush(FakeMotionWorld pOWorld, int piIndex)
        {
            return pOWorld.Log.Any(lsLine => lsLine.StartsWith("push " + piIndex + " "));
        }

        /// <summary>A goblin (index 5) walking north into a hanging arrow two eighths ahead of its
        /// reach: the arrow is used on it and pushed north with no vertical speed; the creature is
        /// not stopped by its mobile mark, so the next step meets it again (the fake world moves
        /// nothing). A magic missile is used on it but not pushed.</summary>
        private static void fCheckCreatureWalksIntoMissile(DataImport pOData)
        {
            foreach (int liKind in new[] { ArrowKind, MagicMissileKind })
            {
                FakeMotionWorld lOWorld;
                UWCreatureMotion lOMotion = fNewCreatureMotion(pOData, out lOWorld);
                string lsName = liKind == ArrowKind ? "an arrow" : "a magic missile";

                lOWorld.AddBody(8, 10, new UWMotionBody
                {
                    Index = HangingMissileIndex, ItemId = liKind, XPos = 4, YPos = 0, ZPos = 20, IsMobile = true
                });

                UWCritterRecord lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 4, 16, 0, 8);
                StepResult lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

                fExpectBool("walk-in: a goblin walking into " + lsName + " meets it", lOStep.Collided, true);
                fExpectInt("walk-in: ... the missile is used on the goblin", fCount(lOWorld, "use " + HangingMissileIndex + " on 5"), 1);

                if (liKind == ArrowKind)
                    fExpectInt("walk-in: ... the arrow is pushed on north, no vertical speed",
                        fCount(lOWorld, "push " + HangingMissileIndex + " heading 0 vz 0"), 1);
                else
                    fExpectBool("walk-in: ... the magic missile (no mass) is not pushed", fAnyPush(lOWorld, HangingMissileIndex), false);

                lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);
                fExpectBool("walk-in: ... a creature is not stopped by the mobile mark: the next step strikes again",
                    fCount(lOWorld, "use " + HangingMissileIndex + " on 5") >= 2, true);
            }
        }

        /// <summary>The player running north from the tile centre into a hanging arrow: used on him
        /// (index 1) and pushed on north with no vertical speed - his record's mass is 0 (PN.mass is
        /// never set). Where the core meets it, he is in the touch reach that ends his touch.</summary>
        private static void fCheckPlayerWalksIntoMissile(DataImport pOData)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWPlayerMotion lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);
            UWCommonObjectProperties.Entry lOMan;
            UWCommonObjectProperties.Entry lOArrow;

            pOData.CommonObjectProperties.TryGet(UWObjectMechanics.AdventurerObjectId, out lOMan);
            pOData.CommonObjectProperties.TryGet(ArrowKind, out lOArrow);

            int liArrowZ = lOPlayer.FeetZPos + 10;

            lOWorld.AddBody(8, 9, new UWMotionBody
            {
                Index = HangingMissileIndex, ItemId = ArrowKind, XPos = 4, YPos = 2, ZPos = liArrowZ, IsMobile = true
            });

            string lsUse = "use " + HangingMissileIndex + " on 1";
            bool lbInReach = false;

            for (int liFrame = 0; liFrame < 12 && fCount(lOWorld, lsUse) == 0; liFrame++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                lOPlayer.Frame(16);

                if (fCount(lOWorld, lsUse) > 0)
                    lbInReach = UWMissileHitRules.InTouchReach(lOPlayer.Params.X >> 5, lOPlayer.Params.Y >> 5, lOPlayer.FeetZPos,
                        lOMan.Radius & 7, lOMan.Height, (8 << 3) + 4, (9 << 3) + 2, liArrowZ, lOArrow.Radius & 7, lOArrow.Height);
            }

            fExpectBool("walk-in: the player running into a hanging arrow meets it, used on him", fCount(lOWorld, lsUse) >= 1, true);
            fExpectBool("walk-in: ... the arrow is pushed on north with no vertical speed (his mass 0)",
                fCount(lOWorld, "push " + HangingMissileIndex + " heading 0 vz 0") >= 1, true);
            fExpectBool("walk-in: ... where the core meets it he is in the touch reach", lbInReach, true);
        }
    }
}
