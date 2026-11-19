namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S CONVERSATION EFFECTS ON THE PLAYER (UW2 plan G5 part 1, 2026-11-16), from Underworld Exhumed's
	/// uw2 conv/BABLHACK.C: what x_skills' values mean, babl_hack 10's signet ring and babl_hack 7's wand charges.
	/// x_exp goes through the shared experience (UWExperience.Change with the second game's halving) and the advance
	/// by a skill point through UWPlayerVitals.GrantSkillAdvance; the session (UWConversationSessionUw2) calls them.
	/// </summary>
	public static class UWConversationRulesUw2
	{
		/// <summary>x_skills: a value above this spends a skill point (the trainers' 10001).</summary>
		public const int SkillPointAbove = 10000;

		/// <summary>x_skills: exactly this raises the skill through get_skill without a point.</summary>
		public const int SkillRaiseWithoutPoint = 10000;

		/// <summary>x_skills sets a skill to a value up to this; anything else only reads.</summary>
		public const int MaxSetSkill = 30;

		/// <summary>ITEM_GUARDIAN_SIGNET_RING (uw2 items.h:189).</summary>
		public const int GuardianSignetRing = 0x35;

		/// <summary>babl_hack's modes built here.</summary>
		public const int HackWandCharges = 7;

		public const int HackSignetRing = 10;

		public enum SkillCallEnum
		{
			/// <summary>Above 10000: grant_skill_advance with a skill point; returns 1 or 0.</summary>
			AdvanceByPoint,

			/// <summary>Exactly 10000: get_skill; returns the skill.</summary>
			RaiseWithoutPoint,

			/// <summary>0 to 30: the skill set; returns it.</summary>
			Set,

			/// <summary>Anything else (the data's 9999): returns the skill.</summary>
			Read
		}

		/// <summary>What x_skills does with this value (BABLHACK.C:414-431).</summary>
		public static SkillCallEnum SkillCall(int piValue)
		{
			if (piValue > SkillPointAbove)
				return SkillCallEnum.AdvanceByPoint;

			if (piValue == SkillRaiseWithoutPoint)
				return SkillCallEnum.RaiseWithoutPoint;

			return piValue >= 0 && piValue <= MaxSetSkill ? SkillCallEnum.Set : SkillCallEnum.Read;
		}

		/// <summary>babl_hack 10: the Guardian's signet ring on either hand (Inventory[9] and [10]).</summary>
		public static bool WearsSignetRing(UWObject pORightRing, UWObject pOLeftRing)
		{
			return (pORightRing != null && pORightRing.ID == GuardianSignetRing) || (pOLeftRing != null && pOLeftRing.ID == GuardianSignetRing);
		}

		// ------------------------------------------------- G5 part 2: the talker

		/// <summary>babl_hack 5: the loner bit for a whoami.</summary>
		public const int HackLoner = 5;

		/// <summary>FIRST_CREATURE (uw2 items.h): transform_creature's item counts from here.</summary>
		public const int FirstCreature = 0x40;

		/// <summary>can_place's range for teleport_critter and transform_creature.</summary>
		public const int PlaceRange = 8;

		/// <summary>A flier is teleported half way up to this zpos.</summary>
		public const int FlierCeiling = 0x80;

		/// <summary>set_mob_seq_n_frame: the frame is the low three bits.</summary>
		public const int FrameMask = 7;

		/// <summary>babl_hack 5 (gronk_whoami(who, 0, ...), set_me_loner): the FIRST active creature with that whoami becomes
		/// a loner (byte 0x0A bit 7) - set_race_attitude and the kin alarm pass it by. False without one.</summary>
		public static bool MakeLoner(UWLevel pOLevel, int piWhoAmI)
		{
			UWNpc lONpc = UWTrapRules.FindActiveByWhoAmI(pOLevel, piWhoAmI);

			if (lONpc == null)
				return false;

			new UWCritterRecord(lONpc).AttitudeLocked = true;

			return true;
		}

		/// <summary>set_sequence(whoami, sequence, frame) (BABLHACK.C:263): the first active creature with that whoami
		/// shows that sequence at that frame (seq * 8 + frame, the frame's three bits). Sequence 7 is the dying one: the
		/// creature's update plays it to its last frame and leaves the corpse (Nelson, conversation 139). False without one.</summary>
		public static bool SetSequence(UWLevel pOLevel, int piWhoAmI, int piSequence, int piFrame)
		{
			UWNpc lONpc = UWTrapRules.FindActiveByWhoAmI(pOLevel, piWhoAmI);

			if (lONpc == null)
				return false;

			int liPacked = (piSequence << 3) | (piFrame & FrameMask);
			UWCritterRecord lORecord = new UWCritterRecord(lONpc);

			lORecord.RawSequence = liPacked >> 3;
			lORecord.Frame = liPacked & FrameMask;

			return true;
		}

		/// <summary>
		/// teleport_critter(talker, x, y) as do_babl_teleport runs it when the conversation is over (CRITTIME.C:374):
		/// nothing to do on its own tile; else the spot of set_gridx_and_y_based_on_tile_type (UWSleepRules.TryGetAmbushSpot,
		/// none on a solid tile), the floor or for a flier half way up to 0x80, can_place with range 8, then the move.
		/// False when it stays.
		/// </summary>
		public static bool TeleportCritter(IUWWorld pIWorld, UWNpc pONpc, int piTileX, int piTileY, bool pbFlier)
		{
			if (pIWorld == null || pONpc == null || pIWorld.Level == null)
				return false;

			UWCritterRecord lORecord = new UWCritterRecord(pONpc);

			if (lORecord.TileX == piTileX && lORecord.TileY == piTileY)
				return true;

			UWTile lOTile = pIWorld.Level.GetTile(piTileX, piTileY);

			if (lOTile == null || !UWSleepRules.TryGetAmbushSpot(lOTile.TileType, out int liSubX, out int liSubY))
				return false;

			int liFloor = (lOTile.FloorHeight >> 4) << 3;
			int liZ = pbFlier ? (liFloor + FlierCeiling) >> 1 : liFloor;

			if (!pIWorld.CanPlace(pONpc.ID, (piTileX << 3) + liSubX, (piTileY << 3) + liSubY, liZ, pbFlier, PlaceRange))
				return false;

			return pIWorld.TeleportCreature(pONpc, new UWTilePos(piTileX, piTileY), liSubX, liSubY, liZ);
		}

		/// <summary>
		/// transform_creature(talker, item, whoami, powerful, terrain) (WORLDEV.C:1775): the creature id from item's six bits
		/// (-1 keeps it), the whoami, the powerful bit and the terrain bits of byte 0x0A (-1 keeps each; the code's last
		/// parameter is called attitude but is written to the terrain field, kept as the original does it). Unless the new
		/// kind flies, a can_place asked with the TILE numbers as fine coordinates and the tile's height unshifted may set
		/// that height as zpos - both as the original passes them. The caller redraws the creature.
		/// </summary>
		public static void TransformCreature(UWNpc pONpc, int piItem, int piWhoAmI, int piPowerful, int piTerrain,
			UWObjectClassProperties pOProperties, IUWWorld pIWorld)
		{
			if (pONpc == null)
				return;

			UWCritterRecord lORecord = new UWCritterRecord(pONpc);
			int liX = lORecord.TileX;
			int liY = lORecord.TileY;
			UWTile lOTile = pIWorld != null && pIWorld.Level != null ? pIWorld.Level.GetTile(liX, liY) : null;
			int liHeight = lOTile != null ? lOTile.FloorHeight >> 4 : 0;

			if (piItem != -1)
				pONpc.ID = (ushort)(FirstCreature + (piItem & 0x3F));

			if (piWhoAmI != -1)
			{
				pONpc.NPCwhoami = (byte)piWhoAmI;

				if (pONpc.RawNpcBytes != null && pONpc.RawNpcBytes.Length > UWNpcRecordCodec.WhoAmIByte)
					pONpc.RawNpcBytes[UWNpcRecordCodec.WhoAmIByte] = (byte)piWhoAmI;
			}

			if (piPowerful != -1)
				lORecord.IsStrong = (piPowerful & 1) != 0;

			if (piTerrain != -1)
				lORecord.TileState = piTerrain & 7;

			bool lbFlier = pOProperties != null && pOProperties.TryGetCritter(pONpc.ID, out UWObjectClassProperties.Critter lOCritter) && lOCritter.IsFlier;

			if (!lbFlier && pIWorld != null && pIWorld.CanPlace(pONpc.ID, liX, liY, liHeight, false, PlaceRange))
				lORecord.ZPos = liHeight;
		}

		/// <summary>babl_hack 7: useNSpellCharges(thing, n) on a carried thing - Merzan's recharge passes -5, five charges
		/// more (up to the most a thing holds); returns the charges left as useNSpellCharges does, 0 without a spell.</summary>
		public static int ChangeCharges(UWObject pOThing, int piCount)
		{
			return UWCarriedSpellRulesUw2.UseCharges(pOThing, null, piCount);
		}
	}
}
