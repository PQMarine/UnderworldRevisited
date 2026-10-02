namespace UWDataImport.UWData
{
	/// <summary>
	/// The per-creature state of the creature AI: the fields of the 27-byte mobile object
	/// record that UW.EXE's routines read and write (Docs/AI/creature-ai.md section 2.1),
	/// as properties over the record bytes of a UWNpc.
	///
	/// WHY OVER THE BYTES AND NOT AS FIELDS OF OUR OWN: the original keeps its whole creature
	/// mind in these bytes, so a save written mid-fight carries the goal, the target, the
	/// swing charge, the blocked bit, the destination - everything. Writing through to the
	/// bytes means UWLevelWriter saves exactly what the original would (it takes RawNpcBytes as
	/// its base), and a save of the original loads into the same state here. Where UWNpc
	/// duplicates a field (NPCGoal, NPCGTarg, NPCLevel, NPCAttitude, HitPoints, NPCXHome,
	/// NPCYHome, NPCHeading, NPCIsAlly) the setters keep both in step, because the writer and
	/// the rest of the port read those.
	///
	/// OFFSETS: RawNpcBytes holds the 19 extra bytes after the 8 common ones, so record offset
	/// 0x08 + n is RawNpcBytes[n] (UWLevel.load_object_list, UWLevelWriter.fWriteNpcBytes).
	/// Word 2 of the common bytes (zpos, facing eighth, fine position) is parsed into
	/// UWObject.ZPos, Heading, YPos, XPos and written back from them.
	///
	/// The array reference is read anew on every access, never cached: UWConversationSession
	/// replaces the array when it writes the hunger bit.
	/// </summary>
	public sealed class UWCritterRecord
	{
		/// <summary>The extra bytes of a mobile object record.</summary>
		public const int NpcByteCount = 19;

		private readonly UWNpc mONpc;

		public UWNpc Npc => mONpc;

		public UWCritterRecord(UWNpc pONpc)
		{
			mONpc = pONpc;

			if (mONpc.RawNpcBytes == null || mONpc.RawNpcBytes.Length < NpcByteCount)
			{
				bool lbHadNone = mONpc.RawNpcBytes == null;
				byte[] lyRaw = new byte[NpcByteCount];

				if (mONpc.RawNpcBytes != null)
					System.Array.Copy(mONpc.RawNpcBytes, lyRaw, mONpc.RawNpcBytes.Length);

				mONpc.RawNpcBytes = lyRaw;

				// A creature made in code has only its properties: carry them into the bytes the
				// mind reads, instead of zeros (attitude 0 = hostile, no ally bit - see
				// InitialiseAsNew).
				if (lbHadNone)
					fSeedFromProperties();
			}
		}

		private void fSeedFromProperties()
		{
			UWNpc lONpc = mONpc;
			int liHitPoints = lONpc.HitPoints;
			int liGoal = lONpc.NPCGoal;
			int liGTarg = lONpc.NPCGTarg;
			int liBackupGoal = lONpc.NPCLevel;
			int liAttitude = lONpc.NPCAttitude;
			int liTileX = lONpc.NPCXHome;
			int liTileY = lONpc.NPCYHome;
			int liFacing = lONpc.NPCHeading;
			bool lbAlly = lONpc.NPCIsAlly;

			HitPoints = liHitPoints;
			Goal = liGoal;
			GTarg = liGTarg;
			BackupGoal = liBackupGoal;
			Attitude = liAttitude;
			TileX = liTileX;
			TileY = liTileY;
			FacingResidual = liFacing;
			IsAlly = lbAlly;
		}

		/// <summary>Where a new creature's home post and tile start (ovr101_5E writes 0x20 into
		/// both); a caller that places it overwrites the tile, not the home post.</summary>
		public const int NewCreaturePost = 0x20;

		/// <summary>Attitude of a new creature: 2, mellow.</summary>
		public const int NewCreatureAttitude = 2;

		/// <summary>Goal of a new creature: 8.</summary>
		public const int NewCreatureGoal = 8;

		/// <summary>
		/// A CREATURE THAT COMES INTO BEING (read 2026-09-27, ovr101_5E, reached through
		/// stub101_2A by the summoning): tile and home post 0x20/0x20, hit points = the table's
		/// vitality * (0x10 + RNG % 0x18) / 0x20, the fine heading from the object's heading, goal
		/// 8 with no target, attitude 2, update interval 4, pitch 16, animation 0x20, everything
		/// else - backup goal, destination, damage, attacker, speed, path, ally and the other
		/// state bits, whoami - zero. Found (per user, 2026-09-27): a summoned monster turned on the
		/// player after a while. It had NO raw bytes, the constructor gave it nineteen zeros, and
		/// the mind read attitude 0 (hostile) and no ally bit from them - the summoning's own
		/// attitude and ally flag only lived in the UWNpc properties.
		/// </summary>
		public void InitialiseAsNew(int piVitality, int piRoll)
		{
			System.Array.Clear(fRaw(), 0, fRaw().Length);

			HitPoints = (piVitality * (0x10 + piRoll)) / 0x20;
			FineHeading = (mONpc.Heading & 7) << 5;
			Goal = NewCreatureGoal;
			GTarg = 0;
			BackupGoal = 0;
			Attitude = NewCreatureAttitude;
			Interval = 4;
			Pitch = 16;
			Animation = 0x20;
			TileX = NewCreaturePost;
			TileY = NewCreaturePost;
			IsAlly = false;
			FacingResidual = 0;

			mONpc.Quality = (ushort)((mONpc.Quality & ~0x3F) | NewCreaturePost);
			mONpc.Owner = (ushort)((mONpc.Owner & ~0x3F) | NewCreaturePost);
		}

		/// <summary>The roll InitialiseAsNew takes: RNG % 0x18.</summary>
		public const int NewCreatureHitPointRollRange = 0x18;

		private byte[] fRaw()
		{
			return mONpc.RawNpcBytes;
		}

		private int fWord(int piAt)
		{
			byte[] lyRaw = fRaw();

			return lyRaw[piAt] | (lyRaw[piAt + 1] << 8);
		}

		private void fSetWord(int piAt, int piValue)
		{
			byte[] lyRaw = fRaw();

			lyRaw[piAt] = (byte)(piValue & 0xFF);
			lyRaw[piAt + 1] = (byte)((piValue >> 8) & 0xFF);
		}

		private void fSetBits(int piAt, int piMask, int piValue)
		{
			byte[] lyRaw = fRaw();

			lyRaw[piAt] = (byte)((lyRaw[piAt] & ~piMask) | (piValue & piMask));
		}

		private void fSetWordBits(int piAt, int piMask, int piValue)
		{
			fSetWord(piAt, (fWord(piAt) & ~piMask) | (piValue & piMask));
		}

		private bool fBit(int piAt, int piBit)
		{
			return (fRaw()[piAt] & (1 << piBit)) != 0;
		}

		private void fSetBit(int piAt, int piBit, bool pbValue)
		{
			fSetBits(piAt, 1 << piBit, pbValue ? 1 << piBit : 0);
		}

		// ------------------------------------------------- Word 0 and word 2 (common bytes)

		/// <summary>Word 0 bits 0-8: the item id; its low six bits select the creature table row.</summary>
		public int ItemId => mONpc.ID & 0x1FF;

		public int TableRow => mONpc.ID & 0x3F;

		/// <summary>Word 0 bit 13 (the port's DoorDirection on a creature): excluded from the
		/// path range budget (seg007_1798_340A, 54283).</summary>
		public bool ExcludedFromPathBudget => mONpc.DoorDirection;

		/// <summary>Word 2 bits 0-6: the height. Written by the physics (the host).</summary>
		public int ZPos
		{
			get { return mONpc.ZPos & 0x7F; }
			set { mONpc.ZPos = value & 0x7F; }
		}

		/// <summary>Zpos in floor height levels, zpos &gt;&gt; 3.</summary>
		public int FloorLevel => ZPos >> 3;

		/// <summary>Word 2 bits 7-9: the facing eighth 0..7, 0 = +Y, clockwise. The picture's
		/// direction; the full facing adds the residual of byte 0x18.</summary>
		public int FacingEighth
		{
			get { return mONpc.Heading & 7; }
			set { mONpc.Heading = (ushort)(value & 7); }
		}

		/// <summary>Word 2 bits 13-15 and 10-12: the fine position inside the tile in eighths.</summary>
		public int FineX
		{
			get { return mONpc.XPos & 7; }
			set { mONpc.XPos = (ushort)(value & 7); }
		}

		public int FineY
		{
			get { return mONpc.YPos & 7; }
			set { mONpc.YPos = (ushort)(value & 7); }
		}

		/// <summary>The position in eighths of a tile: tile * 8 + fine (the coordinate globals
		/// of NPCInitialProcessing_seg007_2488, 52900-52960).</summary>
		public int X => (TileX << 3) + FineX;

		public int Y => (TileY << 3) + FineY;

		// ------------------------------------------------- Bytes 4 and 6: the home post

		/// <summary>Byte 4 bits 0-5 ("quality"): the home post x.</summary>
		public int HomeX => mONpc.Quality & 0x3F;

		/// <summary>Byte 6 bits 0-5 ("owner"): the home post y.</summary>
		public int HomeY => mONpc.Owner & 0x3F;

		// ------------------------------------------------- Byte 8: hit points

		public int HitPoints
		{
			get { return fRaw()[0]; }
			set
			{
				fRaw()[0] = (byte)(value & 0xFF);
				mONpc.HitPoints = (byte)(value & 0xFF);
			}
		}

		// ------------------------------------------------- Byte 9: the fine heading

		/// <summary>Byte 9: the moving heading, 256 steps per turn, 0 = +Y, 64 = +X.</summary>
		public int FineHeading
		{
			get { return fRaw()[1]; }
			set { fRaw()[1] = (byte)(value & 0xFF); }
		}

		// ------------------------------------------------- Byte 0x0A

		/// <summary>Byte 0x0A bits 0-3: the due slot of the 16-slot clock (53373, 54807).</summary>
		public int DueSlot
		{
			get { return fRaw()[2] & 0xF; }
			set { fSetBits(2, 0xF, value); }
		}

		/// <summary>Byte 0x0A bits 4-6: the tile state of the last physics step (the host's).</summary>
		public int TileState
		{
			get { return (fRaw()[2] >> 4) & 7; }
			set { fSetBits(2, 0x70, value << 4); }
		}

		/// <summary>Byte 0x0A bit 7: attitude locked - skipped by the kin alarm, theft and the
		/// kind balancing (scripts only).</summary>
		public bool AttitudeLocked => fBit(2, 7);

		// ------------------------------------------------- Word 0x0B: goal, gtarg, frame

		public int Goal
		{
			get { return fWord(3) & 0xF; }
			set
			{
				fSetWordBits(3, 0xF, value);
				mONpc.NPCGoal = (byte)(value & 0xF);
			}
		}

		/// <summary>Word 0x0B bits 4-11: the goal target's object index, 1 = the player.</summary>
		public int GTarg
		{
			get { return (fWord(3) >> 4) & 0xFF; }
			set
			{
				fSetWordBits(3, 0xFF0, value << 4);
				mONpc.NPCGTarg = (byte)(value & 0xFF);
			}
		}

		/// <summary>Word 0x0B bits 12-15: the animation frame; the goal routines cycle it 0..3
		/// as a phase counter, the attack machine counts it to 4.</summary>
		public int Frame
		{
			get { return (fWord(3) >> 12) & 0xF; }
			set { fSetWordBits(3, 0xF000, value << 12); }
		}

		/// <summary>frame := (frame + 1) &amp; 3, the goal routines' "frame++".</summary>
		public void AdvancePhase()
		{
			Frame = (Frame + 1) & 3;
		}

		// ------------------------------------------------- Word 0x0D

		/// <summary>Word 0x0D bits 0-3: the backup goal, only ever 4 or 0 (SetNewGoalAndGtarg
		/// 54350). The port's NPCLevel.</summary>
		public int BackupGoal
		{
			get { return fWord(5) & 0xF; }
			set
			{
				fSetWordBits(5, 0xF, value);
				mONpc.NPCLevel = (byte)(value & 0xF);
			}
		}

		/// <summary>Word 0x0D bits 4-7: the destination height in floor levels
		/// (SetNPCTargetDestination_seg006_1477_28EA, 46574).</summary>
		public int DestinationHeight
		{
			get { return (fWord(5) >> 4) & 0xF; }
			set { fSetWordBits(5, 0xF0, value << 4); }
		}

		/// <summary>Word 0x0D bit 9: no healing when the player leaves the level (CalmNPCS_ovr104_115
		/// skips it) - breaking Tybal's orb sets it on him. The port's NPCNoHealing.</summary>
		public bool NoLevelExitHealing
		{
			get { return (fWord(5) & 0x200) != 0; }
			set
			{
				fSetWordBits(5, 0x200, value ? 0x200 : 0);
				mONpc.NPCNoHealing = value;
			}
		}

		/// <summary>Word 0x0D bit 10: a strong individual, combat bonuses (spec 8.3).</summary>
		public bool IsStrong => (fWord(5) & 0x400) != 0;

		/// <summary>Word 0x0D bit 13: talked to (conversation).</summary>
		public bool TalkedTo => (fWord(5) & 0x2000) != 0;

		/// <summary>Word 0x0D bits 14-15: 0 hostile, 1 upset, 2 mellow, 3 friendly.</summary>
		public int Attitude
		{
			get { return (fWord(5) >> 14) & 3; }
			set
			{
				fSetWordBits(5, 0xC000, value << 14);
				mONpc.NPCAttitude = (byte)(value & 3);
			}
		}

		// ------------------------------------------------- Word 0x0F: destination and charge

		/// <summary>Word 0x0F bits 0-5 and 6-11: the destination tile.</summary>
		public int DestinationX
		{
			get { return fWord(7) & 0x3F; }
			set { fSetWordBits(7, 0x3F, value); }
		}

		public int DestinationY
		{
			get { return (fWord(7) >> 6) & 0x3F; }
			set { fSetWordBits(7, 0xFC0, value << 6); }
		}

		/// <summary>Word 0x0F bits 12-15: the swing charge index 0..15 (ChooseMeleeAttackToMake
		/// 49540-49555, cleared at the blow 53070).</summary>
		public int ChargeIndex
		{
			get { return (fWord(7) >> 12) & 0xF; }
			set { fSetWordBits(7, 0xF000, value << 12); }
		}

		// ------------------------------------------------- Bytes 0x11, 0x12: damage and attacker

		/// <summary>Byte 0x11: damage taken since the last decision (DamageNPC adds, NPCBehaviours clears).</summary>
		public int DamageTaken
		{
			get { return fRaw()[9]; }
			set { fRaw()[9] = (byte)(value & 0xFF); }
		}

		/// <summary>Byte 0x12: the index of the last attacker, 1 the player, 0 none.</summary>
		public int Attacker
		{
			get { return fRaw()[10]; }
			set { fRaw()[10] = (byte)(value & 0xFF); }
		}

		// ------------------------------------------------- Byte 0x13: speed

		/// <summary>Byte 0x13 bits 0-6: the speed; momentum = speed * 0x2F.</summary>
		public int Speed
		{
			get { return fRaw()[11] & 0x7F; }
			set { fSetBits(11, 0x7F, value); }
		}

		/// <summary>Byte 0x13 bit 7: gravity on (the physics').</summary>
		public bool Gravity
		{
			get { return fBit(11, 7); }
			set { fSetBit(11, 7, value); }
		}

		// ------------------------------------------------- Byte 0x14: interval and pitch

		/// <summary>Byte 0x14 bits 0-2: the update interval in slots.</summary>
		public int Interval
		{
			get { return fRaw()[12] & 7; }
			set { fSetBits(12, 7, value); }
		}

		/// <summary>Byte 0x14 bits 3-7: the pitch, 16 level (fliers).</summary>
		public int Pitch
		{
			get { return (fRaw()[12] >> 3) & 0x1F; }
			set { fSetBits(12, 0xF8, value << 3); }
		}

		// ------------------------------------------------- Byte 0x15: animation

		/// <summary>Byte 0x15 bits 0-5: the animation slot (0 combat idle, 1-3 swings, 5
		/// missile, 7 backing off, 0x0C dying, 0x0D spell, 0x20 standing, 0x2C walking).</summary>
		public int Animation
		{
			get { return fRaw()[13] & 0x3F; }
			set { fSetBits(13, 0x3F, value); }
		}

		/// <summary>Byte 0x15 bit 6: standing, no motion needed.</summary>
		public bool IsStanding
		{
			get { return fBit(13, 6); }
			set { fSetBit(13, 6, value); }
		}

		/// <summary>Byte 0x15 bit 7: has a path (its slot in word 0x16 bits 0-3). The host's
		/// path search keeps no slots; the bit still marks "following a path".</summary>
		public bool HasPath
		{
			get { return fBit(13, 7); }
			set { fSetBit(13, 7, value); }
		}

		// ------------------------------------------------- Word 0x16: path slot and current tile

		public int PathSlot
		{
			get { return fWord(14) & 0xF; }
			set { fSetWordBits(14, 0xF, value); }
		}

		/// <summary>Word 0x16 bits 10-15: the current tile x (the asm's "XHome"; written by the
		/// physics on every relink, 100871). The port's NPCXHome.</summary>
		public int TileX
		{
			get { return (fWord(14) >> 10) & 0x3F; }
			set
			{
				fSetWordBits(14, 0xFC00, value << 10);
				mONpc.NPCXHome = (byte)(value & 0x3F);
			}
		}

		/// <summary>Word 0x16 bits 4-9: the current tile y.</summary>
		public int TileY
		{
			get { return (fWord(14) >> 4) & 0x3F; }
			set
			{
				fSetWordBits(14, 0x3F0, value << 4);
				mONpc.NPCYHome = (byte)(value & 0x3F);
			}
		}

		// ------------------------------------------------- Byte 0x18: facing residual and path bits

		/// <summary>Byte 0x18 bits 0-4: the fine part of the facing. The port's NPCHeading.</summary>
		public int FacingResidual
		{
			get { return fRaw()[16] & 0x1F; }
			set
			{
				fSetBits(16, 0x1F, value);
				mONpc.NPCHeading = (byte)(value & 0x1F);
			}
		}

		/// <summary>The full facing eighth * 32 + residual, 0..255 (seg007_1798_1FDC, 52016-52040).</summary>
		public int FullFacing => (FacingEighth << 5) + FacingResidual;

		/// <summary>Writes a full facing into word 2 bits 7-9 and byte 0x18 bits 0-4.</summary>
		public void SetFullFacing(int piFull)
		{
			FacingEighth = (piFull >> 5) & 7;
			FacingResidual = piFull & 0x1F;
		}

		/// <summary>A goal routine setting the facing: eighth written, residual cleared.</summary>
		public void SetFacing(int piEighth)
		{
			FacingEighth = piEighth & 7;
			FacingResidual = 0;
		}

		/// <summary>Byte 0x18 bit 5: a new destination this update (SetNPCTargetDestination sets,
		/// NPCBehaviours clears).</summary>
		public bool NewDestination
		{
			get { return fBit(16, 5); }
			set { fSetBit(16, 5, value); }
		}

		/// <summary>Byte 0x18 bit 6: blocked (NPC_Goto 46937, 47000-47020).</summary>
		public bool Blocked
		{
			get { return fBit(16, 6); }
			set { fSetBit(16, 6, value); }
		}

		/// <summary>Byte 0x18 bit 7: the straight tile line to the destination is known clear
		/// (NPC_Goto 47040).</summary>
		public bool StraightLineKnown
		{
			get { return fBit(16, 7); }
			set { fSetBit(16, 7, value); }
		}

		// ------------------------------------------------- Byte 0x19: perception and temper

		/// <summary>Bit 0: target confirmed, in view or known.</summary>
		public bool TargetConfirmed
		{
			get { return fBit(17, 0); }
			set { fSetBit(17, 0, value); }
		}

		/// <summary>Bit 1: heard something, searching.</summary>
		public bool HeardSomething
		{
			get { return fBit(17, 1); }
			set { fSetBit(17, 1, value); }
		}

		/// <summary>Bits 2-3: the spell slot to cast at animation 0x0D frame 4 (1 = table byte
		/// 0x2A, 2 = 0x2B, 3 = 0x2C), 0 none.</summary>
		public int SpellSlot
		{
			get { return (fRaw()[17] >> 2) & 3; }
			set { fSetBits(17, 0x0C, value << 2); }
		}

		/// <summary>Bit 4: has made a stand - the next hit gives goal 9 without a morale check.</summary>
		public bool MadeStand
		{
			get { return fBit(17, 4); }
			set { fSetBit(17, 4, value); }
		}

		/// <summary>Bit 5: pursues without morale check and without leash (the far-hit reaction).</summary>
		public bool Relentless
		{
			get { return fBit(17, 5); }
			set { fSetBit(17, 5, value); }
		}

		/// <summary>Bit 6: ally of the player. The port's NPCIsAlly.</summary>
		public bool IsAlly
		{
			get { return fBit(17, 6); }
			set
			{
				fSetBit(17, 6, value);
				mONpc.NPCIsAlly = value;
			}
		}

		/// <summary>Bit 7: hungry (conversation import); no reader in the AI.</summary>
		public bool Hungry => fBit(17, 7);

		// ------------------------------------------------- Byte 0x1A

		/// <summary>The conversation slot; nonzero lets SpecialDeathCases veto a death.</summary>
		public int WhoAmI => fRaw()[18];

		// ------------------------------------------------- Goal writers of the original

		/// <summary>SetNewGoalAndGtarg_seg007_348E (54350-54393): if the CURRENT goal is 4, copy
		/// 4 into the backup nibble; then write goal and gtarg. Nothing else is ever saved.</summary>
		public void SetNewGoal(int piGoal, int piGTarg)
		{
			if (Goal == 4)
				BackupGoal = 4;

			Goal = piGoal;
			GTarg = piGTarg;
		}

		/// <summary>ResetGoalAndGtarg_seg007_1798_34FD (54399-54446): a nonzero backup nibble
		/// becomes the goal with gtarg 1 and is cleared; else goal 2, gtarg 0.</summary>
		public void ResetGoal()
		{
			int liBackup = BackupGoal;

			if (liBackup != 0)
			{
				Goal = liBackup;
				GTarg = 1;
				BackupGoal = 0;
			}
			else
			{
				Goal = 2;
				GTarg = 0;
			}
		}

		/// <summary>SetNPCTargetDestination_seg006_1477_28EA (46574-46646): the destination tile
		/// and height; when any of the three changed, the new-destination bit is set and the
		/// blocked bit cleared.</summary>
		public void SetDestination(int piTileX, int piTileY, int piHeight)
		{
			if (DestinationX == (piTileX & 0x3F) && DestinationY == (piTileY & 0x3F)
				&& DestinationHeight == (piHeight & 0xF))
				return;

			DestinationX = piTileX;
			DestinationY = piTileY;
			DestinationHeight = piHeight;
			NewDestination = true;
			Blocked = false;
		}
	}
}
