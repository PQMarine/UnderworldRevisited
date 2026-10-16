namespace UWDataImport.UWData
{
	/// <summary>
	/// THE NINETEEN EXTRA BYTES OF A MOBILE OBJECT - record offsets 0x08 to 0x1A, the
	/// creature's mind as the original keeps it (object.h's OBJ_* macros from OBJ_HP on,
	/// Docs/AI/creature-ai.md 2.1) - read and written in one place (cleanup phase 2, step
	/// "Codecs", 2026-10-12). Until then UWLevel.load_object_list parsed them, UWLevelWriter
	/// wrote them back and UWCritterRecord addressed the same bytes with its own literals.
	/// The offsets here count from the first extra byte, so record offset 0x08 + n is byte n
	/// (UWNpc.RawNpcBytes[n]).
	///
	/// WHAT THE PORT PARSES INTO FIELDS is only a part: hit points, goal and target, the
	/// backup goal, the attitude word's bits, the home post, the facing residual, the ally bit
	/// and the conversation slot. Everything else lives in the raw bytes alone, where
	/// UWCritterRecord reads and writes it during play; the writer takes the raw bytes as its
	/// base and overwrites only the parsed fields, so nothing is lost on a save.
	/// </summary>
	public static class UWNpcRecordCodec
	{
		public const int ByteCount = 19;

		// ------------------------------------------------- Offsets within the nineteen bytes

		/// <summary>0x08: hit points, one byte (OBJ_HP).</summary>
		public const int HitPointsByte = 0;

		/// <summary>0x09: the moving heading, 256 steps per turn.</summary>
		public const int FineHeadingByte = 1;

		/// <summary>0x0A: due slot, tile state, attitude locked (OBJ_BIN, OBJ_TERRAIN, OBJ_LONER).</summary>
		public const int StateByte = 2;

		/// <summary>0x0B: goal, gtarg, frame (OBJ_GOAL, OBJ_GTARG, OBJ_FRAME).</summary>
		public const int GoalWord = 3;

		/// <summary>0x0D: the attitude word (OBJ_OLDGOAL, OBJ_TARGETZ, OBJ_TEMP, OBJ_NOHEAL,
		/// OBJ_POWERFUL, OBJ_HAS_INV, OBJ_TALKEDTO, OBJ_ATTITUDE).</summary>
		public const int AttitudeWord = 5;

		/// <summary>0x0F: destination tile and swing charge (OBJ_DESTX, OBJ_DESTY, OBJ_ATKFRAME).</summary>
		public const int DestinationWord = 7;

		/// <summary>0x11: damage taken since the last decision (OBJ_DAMAGE).</summary>
		public const int DamageByte = 9;

		/// <summary>0x12: the last attacker's object number.</summary>
		public const int AttackerByte = 10;

		/// <summary>0x13: speed and the gravity bit (OBJ_SPEED).</summary>
		public const int SpeedByte = 11;

		/// <summary>0x14: update interval and pitch (OBJ_RATE, OBJ_PITCH).</summary>
		public const int IntervalByte = 12;

		/// <summary>0x15: the animation sequence and two flags (OBJ_SEQ, OBJ_B15_6, OBJ_B15_7).</summary>
		public const int AnimationByte = 13;

		/// <summary>0x16: path slot and the current tile (OBJ_PATH, OBJ_HOMEY, OBJ_HOMEX).</summary>
		public const int HomeWord = 14;

		/// <summary>0x18: the fine facing and three path bits (OBJ_FINEHEAD, OBJ_B18_5..7).</summary>
		public const int FacingByte = 16;

		/// <summary>0x19: perception and temper flags, the spell slot, ally, fed (OBJ_B19_*,
		/// OBJ_CAST, OBJ_ALLY, OBJ_FED).</summary>
		public const int FlagsByte = 17;

		/// <summary>0x1A: the conversation slot (npc_whoami).</summary>
		public const int WhoAmIByte = 18;

		// ------------------------------------------------- StateByte

		public const int DueSlotMask = 0xF;

		public const int TileStateShift = 4;

		public const int TileStateMask = 0x7;

		/// <summary>OBJ_LONER, bit 7: exempt from its race's attitude changes (the port's
		/// NPCAttitudeLocked).</summary>
		public const int AttitudeLockedBit = 0x80;

		// ------------------------------------------------- GoalWord

		public const int GoalMask = 0xF;

		public const int GTargShift = 4;

		public const int GTargMask = 0xFF;

		public const int FrameShift = 12;

		public const int FrameMask = 0xF;

		// ------------------------------------------------- AttitudeWord

		/// <summary>OBJ_OLDGOAL, bits 0-3: the goal saved while another runs (the port's NPCLevel).</summary>
		public const int BackupGoalMask = 0xF;

		public const int DestinationHeightShift = 4;

		public const int DestinationHeightMask = 0xF;

		/// <summary>OBJ_TEMP, bit 8: made during play, deleted on leaving the level (NPCSpawned).</summary>
		public const int SpawnedBit = 0x100;

		/// <summary>OBJ_NOHEAL, bit 9: no healing on leaving the level (NPCNoHealing).</summary>
		public const int NoHealingBit = 0x200;

		/// <summary>OBJ_POWERFUL, bit 10: a strong individual.</summary>
		public const int StrongBit = 0x400;

		/// <summary>OBJ_HAS_INV, bit 12: the belongings have been rolled (NPCLootSpawned).</summary>
		public const int LootSpawnedBit = 0x1000;

		/// <summary>OBJ_TALKEDTO, bit 13.</summary>
		public const int TalkedToBit = 0x2000;

		/// <summary>OBJ_ATTITUDE, bits 14-15: 0 hostile, 1 upset, 2 mellow, 3 friendly.</summary>
		public const int AttitudeShift = 14;

		public const int AttitudeMask = 0x3;

		// ------------------------------------------------- DestinationWord

		public const int DestinationXMask = 0x3F;

		public const int DestinationYShift = 6;

		public const int DestinationYMask = 0x3F;

		public const int ChargeShift = 12;

		public const int ChargeMask = 0xF;

		// ------------------------------------------------- SpeedByte

		public const int SpeedMask = 0x7F;

		public const int GravityBit = 0x80;

		// ------------------------------------------------- IntervalByte

		public const int IntervalMask = 0x7;

		public const int PitchShift = 3;

		public const int PitchMask = 0x1F;

		// ------------------------------------------------- AnimationByte

		public const int AnimationMask = 0x3F;

		public const int StandingBit = 0x40;

		public const int HasPathBit = 0x80;

		// ------------------------------------------------- HomeWord

		public const int PathSlotMask = 0xF;

		/// <summary>OBJ_HOMEY bits 4-9 and OBJ_HOMEX bits 10-15: the CURRENT tile (uw-formats
		/// calls it npc_xhome/npc_yhome; the home post is in quality and owner).</summary>
		public const int HomeYShift = 4;

		public const int HomeXShift = 10;

		public const int HomeMask = 0x3F;

		// ------------------------------------------------- FacingByte

		public const int FacingResidualMask = 0x1F;

		public const int NewDestinationBit = 0x20;

		public const int BlockedBit = 0x40;

		public const int StraightLineKnownBit = 0x80;

		// ------------------------------------------------- FlagsByte

		public const int TargetConfirmedBit = 0x1;

		public const int HeardSomethingBit = 0x2;

		public const int SpellSlotShift = 2;

		public const int SpellSlotMask = 0x3;

		public const int MadeStandBit = 0x10;

		public const int RelentlessBit = 0x20;

		/// <summary>OBJ_ALLY, bit 6: on the player's side (NPCIsAlly).</summary>
		public const int AllyBit = 0x40;

		/// <summary>OBJ_FED, bit 7: the hunger bit of the conversation import.</summary>
		public const int HungryBit = 0x80;

		/// <summary>Bits 0-6 as the port's misnamed NPCHunger field loads them.</summary>
		public const int HungerMask = 0x7F;

		// ------------------------------------------------- Reading

		/// <summary>
		/// The nineteen bytes into the creature: a private copy as RawNpcBytes, and the parsed
		/// fields as UWLevel.load_object_list has filled them since the beginning. The hit
		/// points are the byte as it is - until 2026-09-11 they were doubled, a mistake
		/// without any apparent reason. The offsets 16, 17 and 18 are the docs' hexadecimal
		/// 0x10, 0x11 and 0x12 (heading, hunger, whoami); a decimal misreading once gave every
		/// creature of level 1 the same conversation slot.
		/// </summary>
		public static void Read(byte[] pyData, int piAt, UWNpc pONpc)
		{
			byte[] lyRaw = new byte[ByteCount];

			System.Array.Copy(pyData, piAt, lyRaw, 0, ByteCount);

			pONpc.NPCUsed = true;
			pONpc.NPC_HP = lyRaw[HitPointsByte];
			pONpc.HitPoints = lyRaw[HitPointsByte];
			pONpc.RawNpcBytes = lyRaw;

			int liGoal = UWBinary.Read16(lyRaw, GoalWord);

			pONpc.NPCGoal = (byte)(liGoal & GoalMask);
			pONpc.NPCGTarg = (byte)((liGoal >> GTargShift) & GTargMask);

			int liAttitude = UWBinary.Read16(lyRaw, AttitudeWord);

			pONpc.NPCLevel = (byte)(liAttitude & BackupGoalMask);
			pONpc.NPCLootSpawned = (liAttitude & LootSpawnedBit) != 0;
			pONpc.NPCTalkedTo = (liAttitude & TalkedToBit) != 0;
			pONpc.NPCAttitude = (byte)((liAttitude >> AttitudeShift) & AttitudeMask);
			pONpc.NPCSpawned = (liAttitude & SpawnedBit) != 0;
			pONpc.NPCNoHealing = (liAttitude & NoHealingBit) != 0;
			pONpc.NPCAttitudeLocked = (lyRaw[StateByte] & AttitudeLockedBit) != 0;

			int liHome = UWBinary.Read16(lyRaw, HomeWord);

			pONpc.NPCYHome = (byte)((liHome >> HomeYShift) & HomeMask);
			pONpc.NPCXHome = (byte)((liHome >> HomeXShift) & HomeMask);
			pONpc.NPCHeading = (byte)(lyRaw[FacingByte] & FacingResidualMask);
			pONpc.NPCHunger = (byte)(lyRaw[FlagsByte] & HungerMask);
			pONpc.NPCIsAlly = (lyRaw[FlagsByte] & AllyBit) != 0;
			pONpc.NPCwhoami = lyRaw[WhoAmIByte];
		}

		// ------------------------------------------------- Writing

		/// <summary>
		/// The nineteen bytes. The basis is the bytes that were read, or zeros for a new
		/// critter (conjured) - not the leftovers of a critter that previously occupied this
		/// slot. The fields Read fills are overwritten with the same masks, except NPCNoHealing
		/// and NPCAttitudeLocked, which stay as they are in the basis bytes - at runtime the
		/// record writes them there itself (breaking Tybal's orb sets NPCNoHealing on him).
		/// NPCSpawned comes from the field since 2026-10-11: a trap or a summon sets it on a
		/// copy, and only the file can carry it to the next load.
		///
		/// Byte 0x19: only the ally bit comes from a field. Bits 0-5 are the creature AI's
		/// perception and temper flags and bit 7 the hunger bit of the conversation import -
		/// all of them live in the raw bytes, written by UWCritterRecord and
		/// UWConversationSession. Until 2026-09-20 the load-time NPCHunger was copied over
		/// bits 0-6 and so threw the AI flags away on every save.
		/// </summary>
		public static void Write(byte[] pyData, int piAt, UWNpc pONpc)
		{
			byte[] lyNpc = pONpc.RawNpcBytes != null && pONpc.RawNpcBytes.Length >= ByteCount
				? (byte[])pONpc.RawNpcBytes.Clone()
				: new byte[ByteCount];

			lyNpc[HitPointsByte] = pONpc.HitPoints;

			int liGoal = UWBinary.Read16(lyNpc, GoalWord);

			liGoal = (liGoal & ~(GoalMask | (GTargMask << GTargShift)))
				| (pONpc.NPCGoal & GoalMask) | ((pONpc.NPCGTarg & GTargMask) << GTargShift);
			UWBinary.Write16(lyNpc, GoalWord, liGoal);

			int liAttitude = UWBinary.Read16(lyNpc, AttitudeWord);

			liAttitude = (liAttitude & ~(BackupGoalMask | SpawnedBit | LootSpawnedBit | TalkedToBit | (AttitudeMask << AttitudeShift)))
				| (pONpc.NPCLevel & BackupGoalMask)
				| (pONpc.NPCSpawned ? SpawnedBit : 0)
				| (pONpc.NPCLootSpawned ? LootSpawnedBit : 0)
				| (pONpc.NPCTalkedTo ? TalkedToBit : 0)
				| ((pONpc.NPCAttitude & AttitudeMask) << AttitudeShift);
			UWBinary.Write16(lyNpc, AttitudeWord, liAttitude);

			int liHome = UWBinary.Read16(lyNpc, HomeWord);

			liHome = (liHome & ~((HomeMask << HomeYShift) | (HomeMask << HomeXShift)))
				| ((pONpc.NPCYHome & HomeMask) << HomeYShift) | ((pONpc.NPCXHome & HomeMask) << HomeXShift);
			UWBinary.Write16(lyNpc, HomeWord, liHome);

			lyNpc[FacingByte] = (byte)((lyNpc[FacingByte] & ~FacingResidualMask) | (pONpc.NPCHeading & FacingResidualMask));
			lyNpc[FlagsByte] = (byte)((lyNpc[FlagsByte] & ~AllyBit) | (pONpc.NPCIsAlly ? AllyBit : 0));
			lyNpc[WhoAmIByte] = pONpc.NPCwhoami;

			System.Array.Copy(lyNpc, 0, pyData, piAt, ByteCount);
		}
	}
}
