using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The djinn capture's first step (xclock[XC_DJINN], UWPlayerRecordUw2.DjinnClock): do_filanium (uw2 motion/OBJPHYS.C).
	/// An object that carries the spell major 0xD effect 3, cast on use (the basilisk oil, a colorless potion with link
	/// 0x243), and lands in water on the floor texture 0xC1 (the filanium mud of the ice caverns, level 26 58-60/15-16)
	/// prints "The thick oil permeates the mud." and moves the clock on to at least 2. The original calls it from both
	/// water landings: mob_to_static (a thrown object at rest in water) and obj_deal (an object put down in water), right
	/// after the splash and before the water takes the object. The later steps: the pressure plate in the mud (level
	/// data, 2 to 3), the lava (UWPlayerTerrain, 3 to 4), Iron Flesh (UWMagicVariantUw2, 4 to 5) and the
	/// bottle broken at the Sigil of Binding (BottleBroken below, 5 to 6).
	/// </summary>
	public static class UWDjinnRulesUw2
	{
		/// <summary>TxmID 0xC1: the mud's floor texture.</summary>
		public const int MudTexture = 0xC1;

		/// <summary>The oil's spell as decode_obj_spell reads it (cast on use).</summary>
		public const int OilMajor = 0xD;

		public const int OilEffect = 3;

		/// <summary>"The thick oil permeates the mud." (block 1, 0x14C + 1).</summary>
		public const int OilPermeatesString = 0x14C + 1;

		/// <summary>The clock's step once the oil is in the mud.</summary>
		public const int OilInMudStage = 2;

		/// <summary>
		/// do_filanium for an object landing in water on a tile whose floor shows piFloorTexture (UWTile.TextureFloor, the
		/// texture map's id). pORecords resolves a spell object in the contents where they are not loaded (may be null).
		/// True when the oil went into the mud: the caller prints OilPermeatesString. The clock is moved on here.
		/// </summary>
		public static bool OilInMud(UWObject pOObject, int piFloorTexture, IList<UWObject> pORecords, UWPlayerFile pOFile)
		{
			if (pOObject == null || pOFile == null || piFloorTexture != MudTexture)
				return false;

			if (!UWEnchantSpellRulesUw2.TryDecode(pOObject, pORecords, out UWObject _, out int liMajor, out int liEffect, out bool lbCastOnUse)
				|| !lbCastOnUse || liMajor != OilMajor || liEffect != OilEffect)
				return false;

			if (UWPlayerRecordUw2.XClock(pOFile, UWPlayerRecordUw2.DjinnClock) < OilInMudStage)
				UWPlayerRecordUw2.SetXClock(pOFile, UWPlayerRecordUw2.DjinnClock, OilInMudStage);

			return true;
		}

		// ------------------------------------------------- The bottle (remove_object, combat/DAMAGE.C:128-150)

		/// <summary>ITEM_BOTTLE_116, the djinn bottle Zoranthus trades for the sceptre.</summary>
		public const int BottleId = 0x116;

		/// <summary>The clock's step from which the daemon is bound (the player glazed, Iron Flesh).</summary>
		public const int BoundStage = 5;

		/// <summary>The step once the daemon is absorbed.</summary>
		public const int AbsorbedStage = 6;

		/// <summary>The Sigil of Binding: level 0x45 (one-based), squares 0x15-0x16 by 0x34-0x35.</summary>
		public const int SigilLevel = 0x45;

		public const int SigilFromX = 0x15;

		public const int SigilToX = 0x16;

		public const int SigilFromY = 0x34;

		public const int SigilToY = 0x35;

		/// <summary>"The air daemon is released, and then rends you." (block 1, 0x171 + 1).</summary>
		public const int DaemonRendsString = 0x171 + 1;

		/// <summary>"The air-daemon is absorbed into your body, ..." (block 1, 0x150 + 1).</summary>
		public const int DaemonAbsorbedString = 0x150 + 1;

		/// <summary>fill_FB(2), the flash of a bound daemon's release.</summary>
		public const int ReleaseFlashColour = 2;

		/// <summary>damage_item(ThePlayer, 0, ..., 0xFF, 0): untyped, enough to kill.</summary>
		public const int RendDamage = 0xFF;

		/// <summary>XC_CASTLE's step the absorption moves on (0xD to 0xE).</summary>
		public const int CastleBeforeAbsorption = 0xD;

		public const int CastleAfterAbsorption = 0xE;

		/// <summary>quests[26] bit 1: the player holds the daemon.</summary>
		public const int DaemonHeldQuest = (26 * 4) + 1;

		public enum BottleOutcome
		{
			/// <summary>Not the bottle: remove_object's other branches.</summary>
			NotTheBottle,

			/// <summary>Before step 5: the message and 0xFF untyped damage to the player; the bottle becomes debris.</summary>
			Rends,

			/// <summary>From step 5 at the sigil: the flash, the message, the bottle is gone (the record is changed here).</summary>
			Absorbed,

			/// <summary>From step 5 anywhere else: the flash alone, the bottle becomes debris - lost for good.</summary>
			Wasted
		}

		/// <summary>
		/// remove_object's bottle branch for bottle piItemId destroyed on square piX/piY of one-based level piLevel. For
		/// Absorbed the record is moved on here: XC_DJINN 6, XC_CASTLE 0xD to 0xE, quests[26] bit 1.
		/// </summary>
		public static BottleOutcome BottleBroken(int piItemId, UWPlayerFile pOFile, int piLevel, int piX, int piY)
		{
			if (piItemId != BottleId || pOFile == null)
				return BottleOutcome.NotTheBottle;

			if (UWPlayerRecordUw2.XClock(pOFile, UWPlayerRecordUw2.DjinnClock) < BoundStage)
				return BottleOutcome.Rends;

			if (piLevel != SigilLevel || piX < SigilFromX || piX > SigilToX || piY < SigilFromY || piY > SigilToY)
				return BottleOutcome.Wasted;

			UWPlayerRecordUw2.SetXClock(pOFile, UWPlayerRecordUw2.DjinnClock, AbsorbedStage);

			if (UWPlayerRecordUw2.XClock(pOFile, UWTrapRulesUw2.CastleXClock) == CastleBeforeAbsorption)
				UWPlayerRecordUw2.SetXClock(pOFile, UWTrapRulesUw2.CastleXClock, CastleAfterAbsorption);

			UWPlayerRecordUw2.SetQuest(pOFile, DaemonHeldQuest, 1);

			return BottleOutcome.Absorbed;
		}
	}
}
