using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 8c (2026-10-25): the debug panel's engine-free report (UWDebugReport from a
    /// UWDebugSnapshot), the folded sections' text (UWDebugFolds), the light of a changed value
    /// (UWDebugFlash) and the columns (UWDebugColumns). Data-free.
    ///
    /// NO LINE OF THE OLD OVERLAY LOST: fLegacyOverlayText below is the F1 overlay's text as
    /// UWDebugOverlay.fBuildText, fDescribeLookTarget, fAppendNearestCritter and UWCritter's
    /// DescribeCombatStats and GetTrace wrote it until cleanup 8c, format for format, on the same
    /// snapshot; every word and number of it must appear in the new report (its titles, labels,
    /// values and footer), for fixture snapshots that switch on every optional line between them.
    /// The few words that changed on purpose are listed in msRenamedWords.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>Words of the old text the report says otherwise: the section "Creature" for the
        /// old "Enemy", "Trap log" for "Traps:", and the "on" of "floor on tile (3,4)", which the
        /// surface's rows say as Surface and Tile.</summary>
        private static readonly HashSet<string> msRenamedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "enemy", "traps", "on"
        };

        private static void fCheckDebugReport()
        {
            Check.Group("Debug report (UWDebugReport, UWDebugFolds, UWDebugFlash, UWDebugColumns - cleanup step 8c)");

            fCheckDebugReportCoverage();
            fCheckDebugReportRows();
            fCheckDebugFolds();
            fCheckDebugFlash();
            fCheckDebugColumns();
        }

        // ------------------------------------------------- Fixtures

        /// <summary>Everything on: the motion core with two records, two traps on the tile (one linked),
        /// poisoned, a typed hit with a shake, every armour extra and two enchantments, an object looked
        /// at, a creature with its stats, alarm, a collided step and a trace, variables and three traps.</summary>
        private static UWDebugSnapshot fDebugFull()
        {
            UWDebugSnapshot lOSnapshot = new UWDebugSnapshot
            {
                Level = 3,
                HideKey = "Shift+F1",
                Now = 100f,
                HasPlace = true,
                X = 2020.4f,
                Y = 212.6f,
                Z = 128f,
                TileX = 31,
                TileZ = 2,
                HasTile = true,
                TileType = "open",
                FloorHeight = 16,
                Yaw = 93.7f,
                OriginalX = 0x1F35,
                OriginalY = 0x0214,
                OriginalZ = 0x0060,
                OriginalHeading = 0x4000,
                Motion = new UWDebugMotion
                {
                    X = 0x1F35, Y = 0x214, Z = 96, Speed = 12, Vz = -3, Gravity = 4, Contact = 0x21, State = "Walking", Precise = true, FineTicks = true,
                    Flags = 0x3, AllFlags = 0x13, CentreFloor = 12, MaxFloor = 14, Count = 2, Overlap = 1, First = -1
                },
                HasBackpack = true,
                BackpackCount = 5,
                BackpackSize = 8,
                Player = new UWDebugPlayer
                {
                    HitPoints = 27.5f, MaxHitPoints = 30f, Mana = 11.6f, MaxMana = 20f, Poison = 4,
                    HasLastHit = true, LastDamage = 3, LastDamageType = UWDamageTypes.PlainFire, LastHitAge = 1.25f, Shook = true, ShakeLevel = 2,
                    DefenceCheck = 17, Defence = 12, DefenceSkill = "Sword", Resistance = 2, Proof = 0x03 | UWDamageTypes.Poison, Stealth = 0x6,
                    DragonSkinBoots = true
                },
                Look = new UWDebugLook
                {
                    Kind = UWDebugLookKind.Object, ObjectId = 0x0123, Category = "Weapon", Texture = "Object 12", TileX = 30, TileY = 3, Height = 24,
                    Heading = 5, Description = "a longsword"
                },
                CreatureCount = 4,
                Creature = new UWDebugCreature
                {
                    Name = "giant rat (7)", LookedAt = true, Distance = 130.2f, State = "Pursuing", AttackKind = "Melee", HasRecord = true,
                    Goal = 5, GTarg = 1, BackupGoal = 2, Attitude = 0, HitPoints = 9, Index = 7, IsAlly = false, IsPassive = true, NoiseRange = 6, Kind = 11,
                    HasAlarm = true, AlarmVictim = 9, AlarmKind = 2, AlarmTileX = 30, AlarmTileY = 4, AlarmAge = 37, AlarmFresh = true, AlarmManhattan = 3,
                    DueSlot = 3, Phase = 1, Interval = 4, Animation = 0x0A, Frame = 2, Speed = 13, FineHeading = 77, FacingEighth = 2, FacingResidual = 13,
                    Confirmed = true, Heard = false, Blocked = true, HasPath = false, Straight = true, Stand = false, Relentless = true, Charge = 1, Pitch = -2,
                    DestinationX = 29, DestinationY = 5, DestinationHeight = 8, TileX = 30, TileY = 3, FineX = 4, FineY = 6, ZPos = 16, Shots = 0,
                    HasStats = true, Vitality = 15, Morale = 20, Initiative = 3, SightRange = 8, DoorSkill = 1, Mover = "walker", IsCaster = true,
                    Collided = true, HitObjectIndex = 412, HitObjectItemId = 0x141, HitClosedDoor = true,
                    BodyY = 64.4f, TileFloor = 16, PictureSlot = 0x21, PictureFrame = 3, LastEvent = "attack rolled 7 of 12"
                }
            };

            lOSnapshot.Motion.Records.Add(new UWDebugMotion.Record { ItemId = 0x141, Index = 412, Dx = 3, Dy = -2, Bottom = 0, Top = 24, Flags = 0x5 });
            lOSnapshot.Motion.Records.Add(new UWDebugMotion.Record { ItemId = 0x0BF, Index = 9, Dx = 0, Dy = 7, Bottom = 8, Top = 16, Flags = 0 });
            lOSnapshot.TileTraps.Add(new UWDebugTileTrap { Name = "move trigger", Quality = 1, Owner = 2, LinkedName = "damage trap" });
            lOSnapshot.TileTraps.Add(new UWDebugTileTrap { Name = "a_look trigger", Quality = 0, Owner = 0 });

            int[] laArmour = { 3, 1, 2, 4 };
            int[] laProtection = { 2, 0, 0, 1 };

            Array.Copy(laArmour, lOSnapshot.Player.Armour, 4);
            Array.Copy(laProtection, lOSnapshot.Player.Protection, 4);
            lOSnapshot.Player.Enchantments.Add(new UWDebugEnchantment { Slot = "Helmet", ObjectId = 0x51, Major = 2, Minor = 0x1F, Protection = 2 });
            lOSnapshot.Player.Enchantments.Add(new UWDebugEnchantment { Slot = "LeftRing", ObjectId = 0x38, Major = 1, Minor = 0x4 });

            lOSnapshot.Creature.Trace.Add("88.1s goal 5 -> 6");
            lOSnapshot.Creature.Trace.Add("99.9s attack rolled 7 of 12");

            lOSnapshot.Variables.Add(new KeyValuePair<int, int>(0, 0));
            lOSnapshot.Variables.Add(new KeyValuePair<int, int>(12, 3));
            lOSnapshot.Variables.Add(new KeyValuePair<int, int>(40, -1));

            lOSnapshot.Traps.Add(new UWTrapLog.Entry { Time = 40f, Text = "damage trap (q 4, own 0)  <- move trigger target 31/2  [repeatable]  damage 4" });
            lOSnapshot.Traps.Add(new UWTrapLog.Entry { Time = 88.6f, Text = "text string trap (q 1, own 0)  [one-shot]" });
            lOSnapshot.Traps.Add(new UWTrapLog.Entry { Time = 99.2f, Text = "camera trap (q 0, own 0)  NOT BUILT" });

            return lOSnapshot;
        }

        /// <summary>The other branches: each look kind, no creature on the level, a creature without a
        /// record, no place, an untyped hit without a shake, the CritterTrace mode.</summary>
        private static List<KeyValuePair<string, UWDebugSnapshot>> fDebugFixtures()
        {
            List<KeyValuePair<string, UWDebugSnapshot>> lOFixtures = new List<KeyValuePair<string, UWDebugSnapshot>>();

            lOFixtures.Add(new KeyValuePair<string, UWDebugSnapshot>("full", fDebugFull()));

            UWDebugSnapshot lOSurface = fDebugFull();

            lOSurface.Motion = null;
            lOSurface.TileTraps.Clear();
            lOSurface.HasTile = false;
            lOSurface.Player.Poison = 0;
            lOSurface.Player.LastDamageType = UWDamageTypes.None;
            lOSurface.Player.Shook = false;
            lOSurface.Player.Resistance = 0;
            lOSurface.Player.Proof = 0;
            lOSurface.Player.Stealth = 0;
            lOSurface.Player.DragonSkinBoots = false;
            lOSurface.Look = new UWDebugLook { Kind = UWDebugLookKind.Surface, Surface = "wall", TileX = 32, TileY = 2, TextureIndex = 41, TileType = "diag_se" };
            lOSurface.Creature.HasRecord = false;
            lOSurface.Creature.LookedAt = false;
            lOSurface.Variables.Clear();
            lOSurface.Traps.Clear();
            lOFixtures.Add(new KeyValuePair<string, UWDebugSnapshot>("surface", lOSurface));

            UWDebugSnapshot lOCollider = fDebugFull();

            lOCollider.Look = new UWDebugLook { Kind = UWDebugLookKind.Collider, ColliderName = "Door 12", HasEntity = true, Description = "a door" };
            lOCollider.Creature.HasStats = false;
            lOCollider.Creature.HasAlarm = false;
            lOCollider.Creature.Collided = false;
            lOCollider.Creature.IsCaster = false;
            lOCollider.Player.HasLastHit = false;
            lOFixtures.Add(new KeyValuePair<string, UWDebugSnapshot>("collider", lOCollider));

            UWDebugSnapshot lOBare = fDebugFull();

            lOBare.Look = new UWDebugLook { Kind = UWDebugLookKind.Collider, ColliderName = "Bridge" };
            lOBare.CreatureCount = 0;
            lOBare.Creature = null;
            lOFixtures.Add(new KeyValuePair<string, UWDebugSnapshot>("bare collider, no creature", lOBare));

            UWDebugSnapshot lONothing = new UWDebugSnapshot { Level = 1, HideKey = "Shift+F1", Now = 3f, CreatureCount = 2 };

            lONothing.Look.Kind = UWDebugLookKind.Nothing;
            lOFixtures.Add(new KeyValuePair<string, UWDebugSnapshot>("no place, nothing looked at", lONothing));

            UWDebugSnapshot lONoCamera = new UWDebugSnapshot { Level = 9, HideKey = "Shift+F1", HasBackpack = true, BackpackCount = 0, BackpackSize = 8 };

            lONoCamera.Look.Kind = UWDebugLookKind.NoCamera;
            lOFixtures.Add(new KeyValuePair<string, UWDebugSnapshot>("no camera", lONoCamera));

            UWDebugSnapshot lOTrace = fDebugFull();

            lOTrace.CritterTrace = true;
            lOFixtures.Add(new KeyValuePair<string, UWDebugSnapshot>("critter trace", lOTrace));

            UWDebugSnapshot lOTraceNone = fDebugFull();

            lOTraceNone.CritterTrace = true;
            lOTraceNone.CreatureCount = 0;
            lOTraceNone.Creature = null;
            lOFixtures.Add(new KeyValuePair<string, UWDebugSnapshot>("critter trace, no creature", lOTraceNone));

            return lOFixtures;
        }

        // ------------------------------------------------- The old overlay's text

        private static string fLegacyF(string psFormat, params object[] pOArguments)
        {
            return string.Format(CultureInfo.InvariantCulture, psFormat, pOArguments);
        }

        /// <summary>The F1 overlay's text as UWDebugOverlay wrote it until cleanup 8c (fBuildText and the
        /// methods it called), format for format, from the snapshot's values.</summary>
        private static string fLegacyOverlayText(UWDebugSnapshot pOSnapshot)
        {
            StringBuilder lOText = new StringBuilder();

            if (pOSnapshot.CritterTrace)
            {
                lOText.Append(fLegacyF("Level {0}", pOSnapshot.Level));
                fLegacyCritter(lOText, pOSnapshot);
                lOText.Append(fLegacyF("\n[{0}] hides", pOSnapshot.HideKey));

                return lOText.ToString();
            }

            lOText.Append(fLegacyF("Level {0}", pOSnapshot.Level));

            if (pOSnapshot.HasBackpack)
                lOText.Append(fLegacyF("   Backpack {0}/{1}", pOSnapshot.BackpackCount, pOSnapshot.BackpackSize));

            if (pOSnapshot.HasPlace)
            {
                lOText.Append(fLegacyF("   Tile ({0},{1})", pOSnapshot.TileX, pOSnapshot.TileZ));
                lOText.Append(fLegacyF("\nPosition {0:F0} / {1:F0} / {2:F0}", pOSnapshot.X, pOSnapshot.Y, pOSnapshot.Z));

                if (pOSnapshot.HasTile)
                    lOText.Append(fLegacyF("   {0}, floor {1}", pOSnapshot.TileType, pOSnapshot.FloorHeight));

                fLegacyCharacter(lOText, pOSnapshot);

                foreach (UWDebugTileTrap lOTrap in pOSnapshot.TileTraps)
                {
                    lOText.Append(fLegacyF("\n  on this tile: {0} (q {1}, own {2})", lOTrap.Name, lOTrap.Quality, lOTrap.Owner));

                    if (lOTrap.LinkedName != null)
                        lOText.Append(fLegacyF(" -> {0}", lOTrap.LinkedName));
                }

                lOText.Append(fLegacyF("\nHeading {0:F0} degrees ({1})", pOSnapshot.Yaw, UWDebugReport.Compass(pOSnapshot.Yaw)));
                lOText.Append(fLegacyF("\nOriginal  x {0} (0x54={0:X4})  y {1} (0x56={1:X4})", pOSnapshot.OriginalX, pOSnapshot.OriginalY));
                lOText.Append(fLegacyF("\n          zpos {0} (0x58={1:X4})  heading {2} (0x5A={2:X4})",
                    pOSnapshot.OriginalZ >> 3, pOSnapshot.OriginalZ, pOSnapshot.OriginalHeading));

                UWDebugMotion lOMotion = pOSnapshot.Motion;

                if (lOMotion != null)
                {
                    lOText.Append(fLegacyF("\nCore      x {0} y {1} z {2} (fine)  speed {3} vz {4} gravity {5}  contact 0x{6:X}  state {7}{8}",
                        lOMotion.X, lOMotion.Y, lOMotion.Z, lOMotion.Speed, lOMotion.Vz, lOMotion.Gravity, lOMotion.Contact, lOMotion.State,
                        lOMotion.Precise ? (lOMotion.FineTicks ? "  precise, fine ticks" : "  precise") : string.Empty));
                    lOText.Append(fLegacyF("\n          flags 0x{0:X} all 0x{1:X}  centre floor {2}  max floor {3}  records {4} (overlap {5} from {6})",
                        lOMotion.Flags, lOMotion.AllFlags, lOMotion.CentreFloor, lOMotion.MaxFloor, lOMotion.Count, lOMotion.Overlap, lOMotion.First));

                    for (int liAt = 0; liAt < lOMotion.Records.Count; liAt++)
                    {
                        UWDebugMotion.Record lORecord = lOMotion.Records[liAt];

                        lOText.Append(fLegacyF("\n          record {0}: item 0x{1:X3} index {2} at {3}/{4} z {5}..{6} flags 0x{7:X}",
                            liAt, lORecord.ItemId, lORecord.Index, lORecord.Dx, lORecord.Dy, lORecord.Bottom, lORecord.Top, lORecord.Flags));
                    }
                }
            }

            lOText.Append("\n");
            lOText.Append(fLegacyLook(pOSnapshot.Look));

            fLegacyCritter(lOText, pOSnapshot);

            bool lbAny = false;

            foreach (KeyValuePair<int, int> lOPair in pOSnapshot.Variables)
            {
                if (lOPair.Value == 0)
                    continue;

                if (!lbAny)
                {
                    lOText.Append("\nVariables:");
                    lbAny = true;
                }

                lOText.Append(fLegacyF(" {0}={1}", lOPair.Key, lOPair.Value));
            }

            if (pOSnapshot.Traps.Count > 0)
            {
                lOText.Append("\nTraps:");

                for (int liAt = pOSnapshot.Traps.Count - 1; liAt >= 0; liAt--)
                    lOText.Append(fLegacyF("\n  {0:F0}s ago  {1}", pOSnapshot.Now - pOSnapshot.Traps[liAt].Time, pOSnapshot.Traps[liAt].Text));
            }

            lOText.Append(fLegacyF("\n[{0}] hides", pOSnapshot.HideKey));

            return lOText.ToString();
        }

        private static void fLegacyCharacter(StringBuilder pOText, UWDebugSnapshot pOSnapshot)
        {
            UWDebugPlayer lOPlayer = pOSnapshot.Player;

            if (lOPlayer == null)
                return;

            pOText.Append(fLegacyF("\nHealth {0}/{1}   Mana {2:F0}/{3:F0}", lOPlayer.HitPoints, lOPlayer.MaxHitPoints, lOPlayer.Mana, lOPlayer.MaxMana));

            if (lOPlayer.Poison > 0)
                pOText.Append(fLegacyF("   POISONED {0}", lOPlayer.Poison));

            if (lOPlayer.HasLastHit)
            {
                string lsType = lOPlayer.LastDamageType == UWDamageTypes.None ? "untyped" : UWDebugReport.DescribeDamageTypes(lOPlayer.LastDamageType);

                pOText.Append(fLegacyF("\nLast hit {0} ({1}) {2:F1}s ago", lOPlayer.LastDamage, lsType, lOPlayer.LastHitAge));
                pOText.Append(lOPlayer.Shook ? fLegacyF(", shake level {0}", lOPlayer.ShakeLevel) : ", no shake");
            }

            pOText.Append(fLegacyF("\nArmour  body {0}/{1}  hands {2}/{3}  legs {4}/{5}  head {6}/{7}   (armour/protection)",
                lOPlayer.Armour[0], lOPlayer.Protection[0], lOPlayer.Armour[1], lOPlayer.Protection[1],
                lOPlayer.Armour[2], lOPlayer.Protection[2], lOPlayer.Armour[3], lOPlayer.Protection[3]));
            pOText.Append(fLegacyF("   Defence {0} (skill {1} + half of {2})", lOPlayer.DefenceCheck, lOPlayer.Defence, lOPlayer.DefenceSkill));

            if (lOPlayer.Resistance > 0)
                pOText.Append(fLegacyF("   Resistance +{0}", lOPlayer.Resistance));

            if (lOPlayer.Proof != 0)
                pOText.Append(fLegacyF("   Immune to {0}", UWDebugReport.DescribeDamageTypes(lOPlayer.Proof)));

            if (lOPlayer.Stealth != 0)
                pOText.Append(fLegacyF("   Stealth 0x{0:X}", lOPlayer.Stealth));

            if (lOPlayer.DragonSkinBoots)
                pOText.Append("   Dragon skin boots (lava-proof)");

            if (!pOSnapshot.HasBackpack)
                return;

            foreach (UWDebugEnchantment lOEnchantment in lOPlayer.Enchantments)
            {
                pOText.Append(fLegacyF("\n  {0}: object {1}, spell {2}-{3:X}", lOEnchantment.Slot, lOEnchantment.ObjectId, lOEnchantment.Major, lOEnchantment.Minor));

                if (lOEnchantment.Protection > 0)
                    pOText.Append(fLegacyF("  Protection +{0}", lOEnchantment.Protection));
            }
        }

        private static string fLegacyLook(UWDebugLook pOLook)
        {
            switch (pOLook.Kind)
            {
                case UWDebugLookKind.NoCamera:
                    return "Look target: no camera";

                case UWDebugLookKind.Nothing:
                    return "Look target: nothing";

                case UWDebugLookKind.Object:
                    return fLegacyF("Look target: object 0x{0:X4} ({1}), texture {2}\n   tile ({3},{4}), height {5}, heading {6}, {7}",
                        pOLook.ObjectId, pOLook.Category, pOLook.Texture, pOLook.TileX, pOLook.TileY, pOLook.Height, pOLook.Heading, pOLook.Description);

                case UWDebugLookKind.Surface:
                    return fLegacyF("Look target: {0} on tile ({1},{2}), texture {3}, {4}",
                        pOLook.Surface, pOLook.TileX, pOLook.TileY, pOLook.TextureIndex, pOLook.TileType);

                default:
                    return pOLook.HasEntity
                        ? fLegacyF("Look target: {0} ({1})", pOLook.ColliderName, pOLook.Description)
                        : "Look target: " + pOLook.ColliderName;
            }
        }

        /// <summary>fAppendNearestCritter with UWCritter.DescribeCombatStats and GetTrace.</summary>
        private static void fLegacyCritter(StringBuilder pOText, UWDebugSnapshot pOSnapshot)
        {
            if (pOSnapshot.CreatureCount == 0)
            {
                pOText.Append("\nEnemy: none on this level");

                return;
            }

            UWDebugCreature lOCreature = pOSnapshot.Creature;

            if (lOCreature == null)
                return;

            pOText.Append(fLegacyF("\nEnemy {0} ({1} of {2})  {3:F0} away ({4:F1} tiles)  {5}  attack {6}",
                lOCreature.Name, lOCreature.LookedAt ? "looked at" : "nearest", pOSnapshot.CreatureCount,
                lOCreature.Distance, lOCreature.Distance / UWWorldScale.TileSize, lOCreature.State, lOCreature.AttackKind));

            if (!lOCreature.HasRecord)
                pOText.Append("  (no record)");
            else
            {
                pOText.Append(fLegacyF("\n  Goal {0}  gtarg {1}  backup {2}  attitude {3}  hp {4}  index {5}",
                    lOCreature.Goal, lOCreature.GTarg, lOCreature.BackupGoal, lOCreature.Attitude, lOCreature.HitPoints, lOCreature.Index));
                pOText.Append(fLegacyF("\n  Ally {0}  passive {1}  hearing {2}  kind {3}",
                    lOCreature.IsAlly ? 1 : 0, lOCreature.IsPassive ? 1 : 0, lOCreature.NoiseRange, lOCreature.Kind));

                if (lOCreature.HasAlarm)
                    pOText.Append(fLegacyF("\n  Alarm: victim {0} kind {1} at {2}/{3}, age {4} of {5}, fresh {6}, manhattan {7}",
                        lOCreature.AlarmVictim, lOCreature.AlarmKind, lOCreature.AlarmTileX, lOCreature.AlarmTileY, lOCreature.AlarmAge,
                        UWCritterRules.KinAlarmPitTicks, lOCreature.AlarmFresh ? 1 : 0, lOCreature.AlarmManhattan));
                else
                    pOText.Append("\n  Alarm: none");

                pOText.Append(fLegacyF("\n  Slot {0} of phase {1}  interval {2}  anim 0x{3:X2} frame {4}  speed {5}  heading {6}  facing {7}+{8}",
                    lOCreature.DueSlot, lOCreature.Phase, lOCreature.Interval, lOCreature.Animation, lOCreature.Frame,
                    lOCreature.Speed, lOCreature.FineHeading, lOCreature.FacingEighth, lOCreature.FacingResidual));
                pOText.Append(fLegacyF("\n  Confirmed {0}  heard {1}  blocked {2}  path {3}  straight {4}  stand {5}  relentless {6}  charge {7}  pitch {8}",
                    lOCreature.Confirmed ? 1 : 0, lOCreature.Heard ? 1 : 0, lOCreature.Blocked ? 1 : 0, lOCreature.HasPath ? 1 : 0,
                    lOCreature.Straight ? 1 : 0, lOCreature.Stand ? 1 : 0, lOCreature.Relentless ? 1 : 0, lOCreature.Charge, lOCreature.Pitch));
                pOText.Append(fLegacyF("\n  Destination {0}/{1} h{2}  tile {3}/{4} fine {5}/{6} z {7}  shots {8}",
                    lOCreature.DestinationX, lOCreature.DestinationY, lOCreature.DestinationHeight, lOCreature.TileX, lOCreature.TileY,
                    lOCreature.FineX, lOCreature.FineY, lOCreature.ZPos, lOCreature.Shots));

                if (lOCreature.HasStats)
                    pOText.Append(fLegacyF("\n  Vitality {0}  morale {1}  initiative {2}  hear {3}  see {4}  door skill {5}  {6}{7}",
                        lOCreature.Vitality, lOCreature.Morale, lOCreature.Initiative, lOCreature.NoiseRange, lOCreature.SightRange,
                        lOCreature.DoorSkill, lOCreature.Mover + " ", lOCreature.IsCaster ? "caster" : ""));

                if (lOCreature.Collided)
                    pOText.Append(fLegacyF("\n  Last step collided: object {0} item 0x{1:X3}{2}",
                        lOCreature.HitObjectIndex, lOCreature.HitObjectItemId, lOCreature.HitClosedDoor ? " (closed door)" : ""));

                pOText.Append(fLegacyF("\n  y {0:F0}  tile floor {1}  picture slot 0x{2:X2} frame {3}  last event: {4}",
                    lOCreature.BodyY, lOCreature.TileFloor, lOCreature.PictureSlot, lOCreature.PictureFrame, lOCreature.LastEvent));
            }

            foreach (string lsLine in lOCreature.Trace)
                pOText.Append("\n    ").Append(lsLine);
        }

        // ------------------------------------------------- Coverage and rows

        /// <summary>The words and numbers of a text: its runs of letters and digits, lower case.</summary>
        private static HashSet<string> fDebugTokens(string psText)
        {
            HashSet<string> lOTokens = new HashSet<string>(StringComparer.Ordinal);
            StringBuilder lOToken = new StringBuilder();

            foreach (char lcChar in psText + " ")
            {
                if (char.IsLetterOrDigit(lcChar))
                {
                    lOToken.Append(char.ToLowerInvariant(lcChar));
                    continue;
                }

                if (lOToken.Length > 0)
                {
                    lOTokens.Add(lOToken.ToString());
                    lOToken.Clear();
                }
            }

            return lOTokens;
        }

        private static void fCheckDebugReportCoverage()
        {
            foreach (KeyValuePair<string, UWDebugSnapshot> lOFixture in fDebugFixtures())
            {
                string lsLegacy = fLegacyOverlayText(lOFixture.Value);
                UWDebugReport lOReport = UWDebugReport.Build(lOFixture.Value);
                HashSet<string> lONew = fDebugTokens(lOReport.ToText());
                List<string> lOMissing = new List<string>();

                foreach (string lsToken in fDebugTokens(lsLegacy))
                {
                    if (!lONew.Contains(lsToken) && !msRenamedWords.Contains(lsToken))
                        lOMissing.Add(lsToken);
                }

                lOMissing.Sort(StringComparer.Ordinal);
                Check.Str("report: every word and number of the old overlay (" + lOFixture.Key + ")", string.Join(" ", lOMissing), string.Empty);
            }

            // The old overlay's lines, one by one, each into the rows that carry it now: the coverage
            // check above holds for the words; these say where they went.
            UWDebugReport lOFull = UWDebugReport.Build(fDebugFull());

            Check.Str("report: the six sections in order", fDebugSectionIds(lOFull), "place,player,look,creature,variables,traps");
            Check.Str("report: the footer", lOFull.Footer, "Shift+F1 hides");
        }

        private static string fDebugSectionIds(UWDebugReport pOReport)
        {
            List<string> lOIds = new List<string>();

            foreach (UWDebugSection lOSection in pOReport.Sections)
                lOIds.Add(lOSection.Id);

            return string.Join(",", lOIds);
        }

        private static string fDebugRow(UWDebugReport pOReport, string psSection, string psKey)
        {
            UWDebugSection lOSection = pOReport.Find(psSection);
            UWDebugRow lORow = lOSection != null ? lOSection.Find(psKey) : null;

            return lORow == null ? "<missing>" : lORow.Label + ": " + lORow.Value;
        }

        private static void fCheckDebugReportRows()
        {
            UWDebugReport lOFull = UWDebugReport.Build(fDebugFull());

            // "Level 3   Backpack 5/8   Tile (31,2)", "Position 2020 / 213 / 128   open, floor 16"
            Check.Str("place: level", fDebugRow(lOFull, "place", "Level"), "Level: 3");
            Check.Str("place: tile", fDebugRow(lOFull, "place", "Tile"), "Tile: 31, 2");
            Check.Str("place: position", fDebugRow(lOFull, "place", "Position"), "Position: 2020 / 213 / 128");
            Check.Str("place: tile type", fDebugRow(lOFull, "place", "Tile type"), "Tile type: open");
            Check.Str("place: floor", fDebugRow(lOFull, "place", "Floor"), "Floor: 16");
            Check.Str("player: backpack", fDebugRow(lOFull, "player", "Backpack"), "Backpack: 5/8");

            // "Heading 94 degrees (East)" and the original's units.
            Check.Str("place: heading", fDebugRow(lOFull, "place", "Heading"), "Heading: 94 degrees");
            Check.Str("place: compass", fDebugRow(lOFull, "place", "Compass"), "Compass: East");
            Check.Str("place: original x", fDebugRow(lOFull, "place", "orig-x"), "x: 7989 (0x54=1F35)");
            Check.Str("place: original zpos", fDebugRow(lOFull, "place", "orig-z"), "zpos: 12 (0x58=0060)");
            Check.Str("place: original heading", fDebugRow(lOFull, "place", "orig-heading"), "heading: 16384 (0x5A=4000)");
            Check.Str("place: core state", fDebugRow(lOFull, "place", "core-state"), "State: Walking, precise, fine ticks");
            Check.Str("place: core records", fDebugRow(lOFull, "place", "core-records"), "Records: 2 (overlap 1 from -1)");
            Check.Str("place: a core record", fDebugRow(lOFull, "place", "record 0"), "record 0: item 0x141 index 412 at 3/-2 z 0..24 flags 0x5");
            Check.Str("place: a trap on the tile", fDebugRow(lOFull, "place", "tiletrap0"), "On this tile: move trigger (q 1, own 2) -> damage trap");

            // "Health 27.5/30   Mana 12/20   POISONED 4", "Last hit 3 (Fire) 1.2s ago, shake level 2"
            Check.Str("player: health", fDebugRow(lOFull, "player", "Health"), "Health: 27.5/30");
            Check.Str("player: mana", fDebugRow(lOFull, "player", "Mana"), "Mana: 12/20");
            Check.Str("player: poisoned", fDebugRow(lOFull, "player", "Poisoned"), "Poisoned: 4");
            Check.Str("player: last hit", fDebugRow(lOFull, "player", "Last hit"), "Last hit: 3 (Fire), shake level 2");
            Check.Str("player: armour", fDebugRow(lOFull, "player", "Head"), "Head: 4 / 1");
            Check.Str("player: defence", fDebugRow(lOFull, "player", "Defence"), "Defence: 17 (skill 12 + half of Sword)");
            Check.Str("player: immunity", fDebugRow(lOFull, "player", "Immune to"), "Immune to: Magic 3/3, Poison");
            Check.Str("player: enchantment", fDebugRow(lOFull, "player", "enchant:Helmet"), "Helmet: object 81, spell 2-1F, Protection +2");

            Check.Str("look: object", fDebugRow(lOFull, "look", "Object"), "Object: 0x0123 (Weapon)");
            Check.Str("creature: chosen", fDebugRow(lOFull, "creature", "Chosen"), "Chosen: looked at of 4 on the level");
            Check.Str("creature: distance", fDebugRow(lOFull, "creature", "Distance"), "Distance: 130 away (2.0 tiles)");
            Check.Str("creature: anim", fDebugRow(lOFull, "creature", "Anim"), "Anim: 0x0A");
            Check.Str("creature: mover", fDebugRow(lOFull, "creature", "Mover"), "Mover: walker caster");
            Check.Str("creature: last step", fDebugRow(lOFull, "creature", "Last step"), "Last step: collided: object 412 item 0x141 (closed door)");
            Check.Bool("creature: the record's numbers are grid cells", lOFull.Find("creature").Find("Goal").Is(UWDebugRowFlags.Cell), true);

            Check.Str("variables: a grid of the set ones", fDebugRow(lOFull, "variables", "12") + "; " + fDebugRow(lOFull, "variables", "40")
                + "; " + fDebugRow(lOFull, "variables", "0"), "12: 3; 40: -1; <missing>");

            UWDebugSection lOTraps = lOFull.Find("traps");

            Check.Str("traps: latest first, with their age", lOTraps.Rows[0].Label + " | " + lOTraps.Rows[2].Label, "1s ago | 60s ago");
            Check.Bool("traps: a trap not built is a warning", lOTraps.Rows[0].Emphasis == UWDebugEmphasis.Warning, true);
            Check.Bool("traps: the lines are log lines", lOTraps.Rows[1].Is(UWDebugRowFlags.Log), true);

            UWDebugReport lOTrace = UWDebugReport.Build(fDebugFixtures()[6].Value);

            Check.Str("critter trace: its own view, one section", fDebugSectionIds(lOTrace), "critter");
            Check.Str("critter trace: the level first", lOTrace.Sections[0].Rows[0].Label, "Level");

            UWDebugReport lOEmpty = UWDebugReport.Build(new UWDebugSnapshot { Level = 1, HideKey = "Shift+F1" });

            Check.Str("empty: the variables say none", fDebugRow(lOEmpty, "variables", "Variables"), "Variables: none set");
            Check.Str("empty: the trap log says none", fDebugRow(lOEmpty, "traps", "Traps"), "Traps: none fired yet");
        }

        // ------------------------------------------------- Folds, light, columns

        private static void fCheckDebugFolds()
        {
            Check.Int("folds: an older file folds nothing", UWDebugFolds.Parse("").Count, 0);
            Check.Int("folds: null folds nothing", UWDebugFolds.Parse(null).Count, 0);
            Check.Str("folds: sorted, blanks and empties dropped", UWDebugFolds.Format(UWDebugFolds.Parse(" traps, ,creature,traps")), "creature,traps");
            Check.Str("folds: folding one adds it", UWDebugFolds.Toggle("creature", "place"), "creature,place");
            Check.Str("folds: folding it again takes it away", UWDebugFolds.Toggle("creature,place", "place"), "creature");
            Check.Str("folds: an unknown section (UW2's) is kept", UWDebugFolds.Toggle("uw2-section", "traps"), "traps,uw2-section");
            Check.Str("folds: nothing folded writes empty", UWDebugFolds.Format(new string[0]), string.Empty);
        }

        private static UWDebugReport fFlashReport(string psValue, string psLive, params string[] paLog)
        {
            UWDebugReport lOReport = new UWDebugReport();
            UWDebugSection lOSection = new UWDebugSection("s", "S");

            lOSection.Add("Value", psValue);
            lOSection.Live("Live", psLive);

            foreach (string lsLog in paLog)
                lOSection.Add(lsLog, "text", UWDebugEmphasis.Normal, UWDebugRowFlags.Log, "log:" + lsLog);

            lOReport.Sections.Add(lOSection);

            return lOReport;
        }

        private static float fFlashOf(UWDebugFlash pOFlash, UWDebugReport pOReport, string psKey, float pfNow)
        {
            UWDebugSection lOSection = pOReport.Sections[0];

            return pOFlash.Strength(lOSection, lOSection.Find(psKey), pfNow);
        }

        private static void fCheckDebugFlash()
        {
            UWDebugFlash lOFlash = new UWDebugFlash();
            UWDebugReport lOFirst = fFlashReport("1", "a", "t1");

            lOFlash.Note(lOFirst, 10f);
            Check.Near("light: the first report lights nothing", fFlashOf(lOFlash, lOFirst, "Value", 10f), 0, 0.0001);
            Check.Near("light: not even its log lines", fFlashOf(lOFlash, lOFirst, "log:t1", 10f), 0, 0.0001);

            UWDebugReport lOSame = fFlashReport("1", "b", "t1");

            lOFlash.Note(lOSame, 10.1f);
            Check.Near("light: a live row that changes stays dark", fFlashOf(lOFlash, lOSame, "Live", 10.1f), 0, 0.0001);
            Check.Near("light: an unchanged value stays dark", fFlashOf(lOFlash, lOSame, "Value", 10.1f), 0, 0.0001);

            UWDebugReport lOChanged = fFlashReport("2", "c", "t1", "t2");

            lOFlash.Note(lOChanged, 11f);
            Check.Near("light: a changed value lights fully", fFlashOf(lOFlash, lOChanged, "Value", 11f), 1, 0.0001);
            Check.Near("light: a new log line lights", fFlashOf(lOFlash, lOChanged, "log:t2", 11f), 1, 0.0001);
            Check.Near("light: an old log line does not", fFlashOf(lOFlash, lOChanged, "log:t1", 11f), 0, 0.0001);

            // Fading by the clock, however many frames come between.
            for (int liFrame = 1; liFrame <= 30; liFrame++)
                lOFlash.Note(fFlashReport("2", "d", "t1", "t2"), 11f + (liFrame * 0.01f));

            Check.Near("light: half faded after half the time, whatever the frames", fFlashOf(lOFlash, lOChanged, "Value", 11.5f), 0.5, 0.0001);
            Check.Near("light: gone after FadeSeconds", fFlashOf(lOFlash, lOChanged, "Value", 11f + UWDebugFlash.FadeSeconds), 0, 0.0001);

            lOFlash.Reset();
            lOFlash.Note(fFlashReport("3", "e", "t3"), 20f);
            Check.Near("light: after a reset the next report lights nothing", fFlashOf(lOFlash, fFlashReport("3", "e", "t3"), "Value", 20f), 0, 0.0001);
        }

        private static void fCheckDebugColumns()
        {
            Check.Int("columns: two when both fit", UWDebugColumns.Count(800f, 380f, 12f), 2);
            Check.Int("columns: one when narrow", UWDebugColumns.Count(700f, 380f, 12f), 1);

            int[] laColumnOf = new int[5];
            float lfTaller = UWDebugColumns.Split(new[] { 100f, 200f, 50f, 150f, 80f }, 2, laColumnOf);

            Check.Seq("columns: in order, the taller column lowest", laColumnOf, new[] { 0, 0, 1, 1, 1 });
            Check.Near("columns: its height", lfTaller, 300, 0.0001);

            lfTaller = UWDebugColumns.Split(new[] { 100f, 200f, 50f, 150f, 80f }, 1, laColumnOf);

            Check.Seq("columns: one column takes all", laColumnOf, new[] { 0, 0, 0, 0, 0 });
            Check.Near("columns: one column's height is the sum", lfTaller, 580, 0.0001);
        }
    }
}
