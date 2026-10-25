using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The creatures the checks make: a brain on FakeCritterHost with its table row and record,
    /// a walking body for the motion checks, a creature reference for FakeWorld. Two record shapes
    /// on purpose: the brain's record starts from 19 zero bytes (its fields set one by one), the
    /// motion's from an object without bytes, which UWCritterRecord seeds from the properties.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>A brain on the fake's services, own height and velocities, its goal changes into
        /// the fake's events (cleanup step 4.13, 2026-10-19).</summary>
        private static UWCritterBrain fMakeBrain(UWCritterRecord pORecord, UWObjectClassProperties.Critter pORow, int piIndex,
            FakeCritterHost pOHost)
        {
            UWCritterBrain lOBrain = new UWCritterBrain(pORecord, pORow, piIndex, pOHost, pOHost.OwnObjectHeight,
                pOHost.MissileVelocities);

            lOBrain.GoalChanged += pOHost.OnGoalChanged;

            return lOBrain;
        }

        /// <summary>A creature of a made-up kind with the table values the checks need.</summary>
        private static UWObjectClassProperties.Critter fMakeRow(int piKind = 3, int piVitality = 20, int piMorale = 5,
            int piNoise = 8, int piSight = 8, int piInitiative = 15, int piRestlessness = 5, int piTravelRange = 3)
        {
            return new UWObjectClassProperties.Critter
            {
                Row = new byte[48],
                Vitality = (byte)piVitality,
                Morale = (byte)piMorale,
                NoiseRange = (byte)piNoise,
                SightRange = (byte)piSight,
                Initiative = (byte)piInitiative,
                Restlessness = (byte)piRestlessness,
                TravelRange = (byte)piTravelRange,
                WanderSpeed = 6,
                MovementSpeed = 8,
                Dexterity = 0,
                GeneralType = (byte)piKind,
                Attacks = new[]
                {
                    new UWObjectClassProperties.CritterAttack { ToHit = 10, Damage = 4, Probability = 100 },
                    new UWObjectClassProperties.CritterAttack(),
                    new UWObjectClassProperties.CritterAttack()
                },
                Spell3 = -1
            };
        }

        /// <summary>A creature record at a tile (fine position 4/4, floor 0), with a home post.</summary>
        private static UWCritterRecord fMakeRecord(int piTileX, int piTileY, int piGoal, int piAttitude,
            int piHomeX = -1, int piHomeY = -1, int piHitPoints = 20)
        {
            UWNpc lONpc = new UWNpc(0x43)
            {
                RawNpcBytes = new byte[19],
                XPos = 4,
                YPos = 4,
                ZPos = 0,
                Heading = 0,
                Quality = (ushort)(piHomeX < 0 ? piTileX : piHomeX),
                Owner = (ushort)(piHomeY < 0 ? piTileY : piHomeY)
            };

            UWCritterRecord lORecord = new UWCritterRecord(lONpc);

            lORecord.TileX = piTileX;
            lORecord.TileY = piTileY;
            lORecord.Goal = piGoal;
            lORecord.GTarg = 1;
            lORecord.Attitude = piAttitude;
            lORecord.HitPoints = piHitPoints;
            lORecord.Interval = 4;

            return lORecord;
        }

        /// <summary>A creature record of a kind at a tile, eighth and height, walking: heading
        /// 0 (+y), interval 4, pitch level, animation 0x2C.</summary>
        private static UWCritterRecord fMakeCreature(int piKind, int piTileX, int piTileY, int piFineX, int piFineY,
            int piZPos, int piHeading, int piSpeed)
        {
            UWNpc lONpc = new UWNpc((ushort)piKind);
            UWCritterRecord lORecord = new UWCritterRecord(lONpc);

            lONpc.TileX = piTileX;
            lONpc.TileY = piTileY;
            lORecord.TileX = piTileX;
            lORecord.TileY = piTileY;
            lORecord.FineX = piFineX;
            lORecord.FineY = piFineY;
            lORecord.ZPos = piZPos;
            lORecord.FineHeading = piHeading;
            lORecord.Speed = piSpeed;
            lORecord.Interval = UWCritterRules.IntervalWalking;
            lORecord.Pitch = UWCritterRules.FlierPitchLevel;
            lORecord.Animation = UWCritterBrain.AnimWalking;
            lORecord.IsStanding = false;

            return lORecord;
        }

        /// <summary>A creature for the fake world: its object (id 0x40 + piKind), its record at the
        /// record tile, and a body tile of its own.</summary>
        private static UWCreatureRef fFakeCreature(int piKind, int piRecordX, int piRecordY, int piBodyX, int piBodyY,
            bool pbDead = false, bool pbSpawned = false, bool pbWithRecord = true, bool pbWithObject = true,
            int piWhoami = 0, int piIndex = 0)
        {
            UWNpc lONpc = new UWNpc((ushort)(0x40 + piKind)) { NPCwhoami = (byte)piWhoami };
            UWCritterRecord lORecord = new UWCritterRecord(lONpc);

            lORecord.TileX = piRecordX;
            lORecord.TileY = piRecordY;

            if (pbSpawned)
                lONpc.MarkSpawned();

            return new UWCreatureRef(pbWithObject ? lONpc : null, pbWithRecord ? lORecord : null,
                new UWTilePos(piBodyX, piBodyY), pbDead, piIndex);
        }
    }
}
