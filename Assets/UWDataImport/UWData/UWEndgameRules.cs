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

		/// <summary>Bones 194 to 198 - "some_bones" and the named skulls (UWObjectIds).</summary>
		public const int FirstBonesObjectId = UWObjectIds.FirstBones;

		public const int LastBonesObjectId = UWObjectIds.LastBones;

		/// <summary>One of the five bones pictures for the character's remains, on the rules'
		/// random source (UWDeath drew from Unity's until 2026-10-11, so no run could repeat it).</summary>
		public static int RollBonesObjectId()
		{
			return FirstBonesObjectId + UWRandom.Next(LastBonesObjectId - FirstBonesObjectId + 1);
		}

		/// <summary>Garamon's bones carry this owner value.</summary>
		public const int GaramonBonesOwner = 0x3E;

		/// <summary>The grave number of "The empty grave of Garamon" ...</summary>
		public const int GaramonGrave = 0x21;

		/// <summary>... and of "The grave of Garamon", once the bones lie in it.</summary>
		public const int GaramonBuriedGrave = 0x22;

		/// <summary>The trigger that fires after the burial conversation, tile 54/52.</summary>
		public const int BurialTriggerTileX = UWQuestPlaces.BurialTriggerTileX;

		public const int BurialTriggerTileY = UWQuestPlaces.BurialTriggerTileY;

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
		/// six tiles of it (Manhattan distance) - UWLevelIds and UWQuestPlaces since 2026-10-11.</summary>
		public const int VolcanoLevelIndex = UWLevelIds.Volcano;

		public const int VolcanoCentre = UWQuestPlaces.VolcanoCentre;

		public const int VolcanoRadius = UWQuestPlaces.VolcanoRadius;

		/// <summary>Level 9 (0-based 8) is the Ethereal Void: no magic there.</summary>
		public const int VoidLevelIndex = UWLevelIds.Void;

		/// <summary>Destination tile in the void, 27/23.</summary>
		public const int VoidTileX = UWQuestPlaces.VoidTileX;

		public const int VoidTileY = UWQuestPlaces.VoidTileY;

		private static byte[] myGraves;

		// ------------------------------------------------- Magic

		/// <summary>
		/// No magic on a tile with the no-magic bit or in the void (DungeonLevel 9) - for runes,
		/// wands and spell traps alike (the reference's spellcasting.CastSpell does the same).
		/// In the original neither Time Freeze nor Gate Travel works on level 9 (per user, 2026-09-14).
		///
		/// THE ONE REFUSAL OF A CAST, the player's and a creature's (do_spell, UnderworldExhumed
		/// SPELLS.C: a critter or the player is refused on an anti-magic square of its own or with
		/// PlayerLevel 9, before the class switch). Since cleanup step 4.10 (2026-10-18) the
		/// creatures' spells go through it as well (UWCreatureSpellHost.IsMagicBlocked, the caster's
		/// tile); until then a creature's missile spell flew in the Void (B2 of Docs/CLEANUP-PLAN.md,
		/// the Exhumed audit's row 30) and its area spell and summoning had a copy of this test.
		/// </summary>
		public static bool IsMagicBlocked(UWLevel pOLevel, int piLevelIndex, UWTilePos pOTile)
		{
			if (pOLevel == null)
				return false;

			if (piLevelIndex == UWRules.Current.Levels.Void)
				return true;

			return IsAntiMagicTile(pOLevel, pOTile);
		}

		/// <summary>
		/// The tile's no-magic bit alone (anti_magic_p, UnderworldExhumed SPELLS.C: bit 0 of the
		/// square's door byte), without the Void: what a creature's mind asks before it starts a
		/// cast (crit_magik_attack and maybe_cast_defensive_spell, AI.C) and before it chooses to
		/// answer a far hit with magic (critter_mv) - so a caster in the Void still raises its arms,
		/// and only the cast itself is refused there (IsMagicBlocked). Off the map: false. The
		/// creature host's own copy of the test until cleanup step 4.10 (2026-10-18).
		/// </summary>
		public static bool IsAntiMagicTile(UWLevel pOLevel, UWTilePos pOTile)
		{
			UWTile lOData = pOLevel != null ? pOLevel.GetTile(pOTile.X, pOTile.Y) : null;

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
			return UWObjectIds.IsBones(piObjectId);
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

			/// <summary>The last one: the counter reaches 0 and the rending sound plays; the
			/// talisman is gone without a flame. The journey is CheckVictory's, which watches the
			/// counter (since 2026-10-18).</summary>
			Last
		}

		/// <summary>The counter after the journey: check_victory sets 0xFF so that its test does
		/// not fire again (UnderworldExhumed game/SKILLS.C).</summary>
		public const int TalismansAfterVictory = 0xFF;

		/// <summary>
		/// check_victory's test (game/SKILLS.C, run every frame from editor_dispatch): the talisman
		/// counter (quest 36) at 0 starts the journey through the moongate into the void, however
		/// it got there. Until 2026-10-18 ours launched from the impact of the last talisman alone
		/// and set 0xFF at once, so a save with the counter at 0 left the player on his level
		/// and immortal (per user on the original the same day: such a save swirls into level 9
		/// right after loading). The impact now only counts down to 0, as mob_to_static does.
		/// </summary>
		public static bool AllTalismansGone()
		{
			return UWQuestFlags.Get(TalismanQuestFlag) == 0;
		}

		/// <summary>
		/// Following the reference's motion.ObjectHitsFloorTile. Applies the quest flag changes;
		/// piRemaining is how many talismans are still to go after this one (for the flame).
		/// </summary>
		public static TalismanOutcome DropTalismanInLava(int piObjectId, int piLevelIndex, int piTileX, int piTileY,
			bool pbPlayerAlive, bool pbTalismansDestroyable, out int piRemaining)
		{
			piRemaining = 0;

			// The volcano is the rule set's (levels.volcano, story.volcano.*, cleanup phase 10); the
			// consts above are its defaults.
			UWRulesStory.VolcanoRules lOVolcano = UWRules.Current.Story.Volcano;

			if (!UWTalismans.IsTalisman(piObjectId) || piLevelIndex != UWRules.Current.Levels.Volcano
				|| System.Math.Abs(piTileX - lOVolcano.Centre) + System.Math.Abs(piTileY - lOVolcano.Centre) >= lOVolcano.Radius)
				return TalismanOutcome.NotApplicable;

			if (!pbPlayerAlive)
				return TalismanOutcome.NotApplicable;

			if (!pbTalismansDestroyable)
			{
				UWQuestFlags.Set(DreamQuestFlag, UWQuestFlags.Get(DreamQuestFlag) | 8);

				return TalismanOutcome.DreamMarked;
			}

			// The counter is a byte that mob_to_static decrements (motion/OBJPHYS.C): from 0 it
			// wraps to 0xFF, a flame like any other.
			piRemaining = (UWQuestFlags.Get(TalismanQuestFlag) - 1) & 0xFF;

			if (piRemaining == 0)
			{
				UWQuestFlags.Set(TalismanQuestFlag, 0);

				return TalismanOutcome.Last;
			}

			UWQuestFlags.Set(TalismanQuestFlag, piRemaining);

			return TalismanOutcome.Flame;
		}
	}
}
