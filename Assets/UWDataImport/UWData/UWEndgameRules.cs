using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The decisions on the road to the end of Ultima Underworld 1: the gravestones, the
	/// burial of Garamon, the talismans in the volcano, and where magic is blocked.
	/// Engine-free since 2026-09-18 (P3 of the engine separation), out of UWEndgame, which
	/// keeps the conversation, the inventory, the flames, the flight into the void and the
	/// victory screens.
	///
	/// SOURCES: the burial exists only in UW.EXE (UW1_asm.asm, UseBones_seg040_719) - the
	/// reference reads the two bits but never sets them. Talismans and void follow the
	/// reference (motion.ObjectHitsFloorTile, etherealvoid.LaunchPlayerIntoTheVoid),
	/// gravestones follow gravestone.cs.
	/// </summary>
	public static class UWEndgameRules
	{
		/// <summary>The gravestone (357).</summary>
		public const int GravestoneObjectId = 0x165;

		/// <summary>Bones 194 to 198 - "some_bones" and the named skulls.</summary>
		public const int FirstBonesObjectId = 0xC2;

		public const int LastBonesObjectId = 0xC6;

		/// <summary>Garamon's bones carry this owner value.</summary>
		public const int GaramonBonesOwner = 0x3E;

		/// <summary>The grave number of "The empty grave of Garamon" ...</summary>
		public const int GaramonGrave = 0x21;

		/// <summary>... and of "The grave of Garamon", once the bones lie in it.</summary>
		public const int GaramonBuriedGrave = 0x22;

		/// <summary>The trigger that fires after the burial conversation, tile 54/52.</summary>
		public const int BurialTriggerTileX = 0x36;

		public const int BurialTriggerTileY = 0x34;

		/// <summary>The conversation with Garamon's ghost.</summary>
		public const int GaramonConversation = 0x1B;

		/// <summary>The placeholder partner from UseBones (var_22 on the stack): object 0x7E,
		/// goal 7, attitude 3, conversation slot 0x1B - in no tile.</summary>
		private const ushort PlaceholderTalkerObjectId = 0x7E;

		private const byte PlaceholderTalkerGoal = 7;

		private const byte PlaceholderTalkerAttitude = 3;

		/// <summary>Quest flag 36: how many talismans are left to destroy.</summary>
		public const int TalismanQuestFlag = UWPlayerData.TalismanQuestFlag;

		/// <summary>Quest flag 37: the dreams (see UWSleepRules) - bit 3 marks the volcano dream.</summary>
		public const int DreamQuestFlag = UWPlayerData.DreamQuestFlag;

		/// <summary>Level 8 (0-based 7) holds the volcano, its centre at 32/32, the lava within
		/// six tiles of it (Manhattan distance).</summary>
		public const int VolcanoLevelIndex = 7;

		public const int VolcanoCentre = 32;

		public const int VolcanoRadius = 6;

		/// <summary>Level 9 (0-based 8) is the Ethereal Void: no magic there.</summary>
		public const int VoidLevelIndex = 8;

		/// <summary>Destination tile in the void, 27/23.</summary>
		public const int VoidTileX = 0x1B;

		public const int VoidTileY = 0x17;

		private static byte[] myGraves;

		// ------------------------------------------------- Magic

		/// <summary>
		/// No magic on a tile with the no-magic bit or in the void (DungeonLevel 9) - for runes,
		/// wands and spell traps alike (the reference's spellcasting.CastSpell does the same).
		/// In the original neither Time Freeze nor Gate Travel works on level 9 (per user, 2026-09-14).
		/// </summary>
		public static bool IsMagicBlocked(UWLevel pOLevel, int piLevelIndex, UWTilePos pOTile)
		{
			if (pOLevel == null)
				return false;

			if (piLevelIndex == VoidLevelIndex)
				return true;

			UWTile lOData = pOLevel.GetTile(pOTile.X, pOTile.Y);

			return lOData != null && lOData.NoMagicAllowed;
		}

		// ------------------------------------------------- Gravestones

		/// <summary>The grave number of a gravestone - it is stored as a special value (from 512) in
		/// the link field -, or minus one.</summary>
		public static int GetGraveNumber(UWObject pOObject)
		{
			if (pOObject == null || pOObject.ID != GravestoneObjectId || !pOObject.HasQuantity
				|| (pOObject.Quantity & 0x200) == 0)
				return -1;

			return pOObject.Quantity & 0x1FF;
		}

		/// <summary>The picture in CS401.N01 for a grave number: GRAVE.DAT holds one byte per number,
		/// picture plus one; zero means no picture.</summary>
		public static int GetGraveFrame(DataImport pOData, int piGrave)
		{
			if (pOData == null || piGrave < 0)
				return -1;

			if (myGraves == null)
			{
				string lsPath = Path.Combine(pOData.DataPath, "GRAVE.DAT");

				myGraves = File.Exists(lsPath) ? File.ReadAllBytes(lsPath) : new byte[0];
			}

			return piGrave < myGraves.Length ? myGraves[piGrave] - 1 : -1;
		}

		/// <summary>The inscription: string block 8 at grave number plus one (grave 33 "The empty grave
		/// of Garamon", 34 "The grave of Garamon", 66 Korianous - checked with the removed tool UWGaramonDump).
		/// </summary>
		public static string GetGraveText(DataImport pOData, int piGrave)
		{
			if (pOData == null || piGrave < 0)
				return string.Empty;

			try
			{
				return pOData.GetReadableText(piGrave + 1);
			}
			catch (System.Exception)
			{
				return string.Empty;
			}
		}

		// ------------------------------------------------- Burial

		public static bool IsBones(int piObjectId)
		{
			return piObjectId >= FirstBonesObjectId && piObjectId <= LastBonesObjectId;
		}

		/// <summary>What bones on a target come to - UW.EXE UseBones_seg040_719.</summary>
		public enum BonesOutcome
		{
			/// <summary>No gravestone: "It seems to have no effect."</summary>
			NoEffect,

			/// <summary>Ordinary bones at a grave: "You thoughtfully give the bones a final
			/// resting place." - the bones are gone.</summary>
			RestingPlace,

			/// <summary>Garamon's bones at another grave: "The bones do not seem at rest in the
			/// grave, and you take them back." - you keep them.</summary>
			NotAtRest,

			/// <summary>Garamon's bones at his grave: both bits set, the grave reads "The grave
			/// of Garamon", the bones are gone - the conversation with his ghost and the
			/// trigger at 54/52 follow on the host.</summary>
			GaramonBuried
		}

		/// <summary>Decides the burial and applies what belongs to the data: the grave's link and
		/// the two flags.</summary>
		public static BonesOutcome UseBones(UWObject pOBones, UWObject pOTarget,
			ref bool pbTalismansDestroyable, ref bool pbGaramonBuried)
		{
			if (pOBones == null || pOTarget == null || pOTarget.ID != GravestoneObjectId)
				return BonesOutcome.NoEffect;

			if (pOBones.Owner != GaramonBonesOwner)
				return BonesOutcome.RestingPlace;

			if (GetGraveNumber(pOTarget) != GaramonGrave)
				return BonesOutcome.NotAtRest;

			pbTalismansDestroyable = true;
			pbGaramonBuried = true;

			// Link to grave 0x22, the special value stays; the owner is stored in a separate field.
			pOTarget.Quantity = (ushort)(0x200 | GaramonBuriedGrave);

			return BonesOutcome.GaramonBuried;
		}

		/// <summary>The placeholder partner of the burial conversation. It reads its npc_ values
		/// from it and writes back into it at the end; afterwards it is forgotten.</summary>
		public static UWNpc CreateBurialPlaceholder()
		{
			return new UWNpc(PlaceholderTalkerObjectId)
			{
				ID = PlaceholderTalkerObjectId,
				NPCGoal = PlaceholderTalkerGoal,
				NPCAttitude = PlaceholderTalkerAttitude,
				NPCwhoami = GaramonConversation
			};
		}

		// ------------------------------------------------- Talismans in the volcano

		/// <summary>What a talisman falling into the volcano's lava comes to.</summary>
		public enum TalismanOutcome
		{
			/// <summary>Not a talisman, not the volcano, or the player is dead: nothing.</summary>
			NotApplicable,

			/// <summary>Before the burial: the dream bit is set, the talisman stays where it lies.</summary>
			DreamMarked,

			/// <summary>Counted down, the talisman perishes in flames.</summary>
			Flame,

			/// <summary>The last one: counter to 0xFF and off into the void.</summary>
			Launch
		}

		/// <summary>
		/// Following the reference's motion.ObjectHitsFloorTile. Applies the quest flag changes;
		/// piRemaining is how many talismans are still to go after this one (for the flame).
		/// </summary>
		public static TalismanOutcome DropTalismanInLava(int piObjectId, int piLevelIndex, int piTileX, int piTileY,
			bool pbPlayerAlive, bool pbTalismansDestroyable, out int piRemaining)
		{
			piRemaining = 0;

			if (!UWTalismans.IsTalisman(piObjectId) || piLevelIndex != VolcanoLevelIndex
				|| System.Math.Abs(piTileX - VolcanoCentre) + System.Math.Abs(piTileY - VolcanoCentre) >= VolcanoRadius)
				return TalismanOutcome.NotApplicable;

			if (!pbPlayerAlive)
				return TalismanOutcome.NotApplicable;

			if (!pbTalismansDestroyable)
			{
				UWQuestFlags.Set(DreamQuestFlag, UWQuestFlags.Get(DreamQuestFlag) | 8);

				return TalismanOutcome.DreamMarked;
			}

			piRemaining = UWQuestFlags.Get(TalismanQuestFlag) - 1;

			if (piRemaining <= 0)
			{
				UWQuestFlags.Set(TalismanQuestFlag, 0xFF);

				return TalismanOutcome.Launch;
			}

			UWQuestFlags.Set(TalismanQuestFlag, piRemaining);

			return TalismanOutcome.Flame;
		}
	}
}
