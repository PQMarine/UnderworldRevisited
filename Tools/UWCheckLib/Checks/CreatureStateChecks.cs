using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.8 (2026-10-18): the creature's record as the one store of its state. Pinned:
    /// the body's health reads and writes byte 8 (UWHealth.KeepHitPointsIn), is destroyed only when
    /// the death is committed, not at 0 hit points; a refused death (the golem) leaves the record
    /// at 0 and the body alive, and the capture saves that 0 (B23) - and his script's 125 whole,
    /// past the body's maximum; a health without a store keeps its old rules.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckCreatureState()
        {
            Check.Group("Creature state in the record (cleanup step 4.8, B23)");

            fCheckHealthInRecord();
            fCheckRefusedDeathSaved();
            fCheckFollowerJump();
        }

        /// <summary>Goal 3 beyond eight tiles: the brain asks the host to put the follower four
        /// tiles short of the target (seg007_1798_3E4) and writes nothing itself - the host's one
        /// write (the fake host writes none, so the record stays).</summary>
        private static void fCheckFollowerJump()
        {
            FakeCritterHost lOHost = new FakeCritterHost { ClockValue = 1000 };
            UWCritterRecord lORecord = fMakeRecord(5, 5, 3, 2);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lORecord.ZPos = 24;
            lOHost.PlacePlayer((14 << 3) + 4, (5 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectBool("follower: jumps to 10/5, four tiles short of the target", lOHost.HasEvent("teleport 10/5"), true);
            fExpectBool("follower: the brain leaves the record to the host",
                lORecord.TileX == 5 && lORecord.TileY == 5 && lORecord.ZPos == 24, true);
        }

        private static void fCheckHealthInRecord()
        {
            UWCritterRecord lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 15);
            UWHealth lOHealth = new UWHealth();

            lOHealth.Initialise(20, 0, 0, 0, 0, 15);
            lOHealth.KeepHitPointsIn(lORecord);

            fExpectInt("store: the health reads byte 8", lOHealth.CurrentHealth, 15);

            lORecord.HitPoints = 7;
            fExpectInt("store: a write to the record is the health's", lOHealth.CurrentHealth, 7);

            lOHealth.SetCurrentHealth(9);
            fExpectInt("store: the health writes byte 8 ...", lORecord.HitPoints, 9);
            fExpectInt("store: ... and the UWNpc beside it", lORecord.Npc.HitPoints, 9);

            lORecord.HitPoints = 125;
            fExpectInt("store: a script's 125 stays 125 past the maximum of 20", lOHealth.CurrentHealth, 125);

            lORecord.HitPoints = 0;
            fExpectBool("store: 0 hit points is not destroyed", lOHealth.IsDestroyed, false);

            lOHealth.MarkDestroyed();
            fExpectBool("store: destroyed once the death is committed", lOHealth.IsDestroyed, true);

            UWHealth lOPlain = new UWHealth();

            lOPlain.Initialise(40, 0, 0);
            fExpectBool("no store: a blow to 0 destroys", lOPlain.TakeScaledDamage(45) && lOPlain.IsDestroyed
                && lOPlain.CurrentHealth == 0, true);

            UWHealth lOMarked = new UWHealth();

            lOMarked.Initialise(40, 0, 0);
            lOMarked.MarkDestroyed();
            fExpectBool("no store: marked destroyed is 0 hit points", lOMarked.IsDestroyed && lOMarked.CurrentHealth == 0, true);
        }

        /// <summary>The golem's case: the blow takes the record to 0, his whoami refuses the death,
        /// he lives on at 0 - and is saved so.</summary>
        private static void fCheckRefusedDeathSaved()
        {
            UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0, -1, -1, 10);

            lORecord.Npc.RawNpcBytes[UWNpcRecordCodec.WhoAmIByte] = UWSpecialDeaths.MetalGolemWhoami;

            UWHealth lOHealth = new UWHealth();

            lOHealth.Initialise(20, 0, 0, 0, 0, 10);
            lOHealth.KeepHitPointsIn(lORecord);

            FakeCritterHost lOHost = new FakeCritterHost { Vetoes = true, ClockValue = 1000 };
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lOBrain.OnDamaged(lOHost, UWCritterBrain.PlayerIndex, 12);

            fExpectInt("refused death: the record holds 0", lORecord.HitPoints, 0);
            fExpectBool("refused death: no death started", lOHost.HasEvent("death"), false);
            fExpectBool("refused death: the body lives on at 0", !lOHealth.IsDestroyed && lOHealth.CurrentHealth == 0, true);

            UWWorldCapture.CaptureCritter(new UWTileQueries(null, null), lORecord, lOHealth.IsDestroyed, lOHealth.InitialHealth,
                false);
            fExpectInt("refused death: saved with 0, not 1 (B23)", lORecord.Npc.HitPoints, 0);

            // His script sets 125 into the UWNpc (the conversation's npc_hp); UWCritter.RefreshFromData
            // carries it into the record, which the body reads.
            lORecord.HitPoints = 125;

            UWWorldCapture.CaptureCritter(new UWTileQueries(null, null), lORecord, lOHealth.IsDestroyed, lOHealth.InitialHealth,
                false);
            fExpectInt("script's 125: saved whole, past the body's maximum of 20", lORecord.Npc.HitPoints, 125);
        }
    }
}
