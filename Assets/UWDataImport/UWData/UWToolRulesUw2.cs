using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S CLASS 0x12 THINGS (G4 rest part 2, 2026-11-16): UseMagic, UseOilOn and UseRockHammerOn (uw2
	/// obj/USEITEMS.C:934, :382, :1002) and the fountain, with the second game's own lines. What needs the engine - the
	/// pointer, the pieces in the world, the notes - is UWToolUseUw2's; the rules are here.
	///
	///   - LEECHES from the pack: "The leeches remove the poison as well as some of your skin and blood." only when poisoned
	///     (0xEF), the poison gone, backfire(2): 2d8, never below 3 hit points; then used up.
	///   - THE OIL FLASK asks for a target, and only a carried one counts (UseOilOn returns at once on a thing in the
	///     world). A piece of wood (0xCC, 0xCD) becomes a torch of quality 0x28 with 0xC4; an unlit lantern or torch takes
	///     0x20 more quality, to 0x3F, with 0xC2 (the torch's lines four higher), or says it is full (0xC3) and keeps the
	///     flask; a lit one says 0xC1 and keeps it; anything else "You cannot use oil on that." (0xC0). From a stack one is
	///     taken off onto the pointer first and changed. The flask is used up when it changed something.
	///   - THE ROCK HAMMER asks for a target, and only a boulder in the world counts: "The rock breaks into smaller pieces."
	///     (0x95), the boulder goes first, then rand(0..1) + (0x155 - id) + 1 new pieces, each one or two sizes smaller, one
	///     below the small boulder a pile of 3 to 8 sling stones, put down around its spot (put_at, range 6). Another thing
	///     in the world "It seems to have no effect." (0x92); a carried one nothing.
	///   - THE FOUNTAIN in the world: quality 0 "The fountain is dry." (0x93); else its spell, if it carries one, with
	///     "The waters of the fountain renew your strength." (0x108) for class 4, else "The water refreshes you." (0xFC).
	///     None of the shipped fountains carries a spell (all twelve: quantity 1, no enchantment).
	/// </summary>
	public static class UWToolRulesUw2
	{
		public const int Mandolin = 0x123;

		public const int Flute = 0x124;

		public const int Leeches = 0x125;

		public const int RockHammer = 0x128;

		public const int FishingPole = 0x12B;

		public const int OilFlask = 0x12D;

		public const int Fountain = 0x12E;

		private const int FirstWood = 0xCC;

		private const int LastWood = 0xCD;

		private const int Lantern = 0x90;

		private const int Torch = 0x91;

		private const int LitLantern = 0x94;

		private const int LitTorch = 0x95;

		private const int FirstBoulder = 0x153;

		private const int MediumBoulder = 0x155;

		private const int SmallBoulder = 0x156;

		private const int SlingStone = 0x10;

		/// <summary>The torch made of wood: quality 0x28.</summary>
		public const int WoodTorchQuality = 0x28;

		private const int OilFill = 0x20;

		private const int FullQuality = 0x3F;

		/// <summary>The fountain's spell that renews strength: class 4.</summary>
		private const int StrengthClass = 4;

		/// <summary>backfire(ThePlayer, 2): two eight-sided dice.</summary>
		public const int LeechDice = 2;

		/// <summary>put_at's range for the pieces, in eighths.</summary>
		public const int PieceRange = 6;

		// The game's lines; our block counts one higher.
		public const int LeechesString = 0xEF + 1;

		public const int CannotOilString = 0xC0 + 1;

		private const int LitString = 0xC1 + 1;

		private const int RefuelledString = 0xC2 + 1;

		private const int FullString = 0xC3 + 1;

		private const int WoodTorchString = 0xC4 + 1;

		/// <summary>The torch's lines are four after the lantern's.</summary>
		private const int TorchLines = 4;

		public const int RockBreaksString = 0x95 + 1;

		public const int NoEffectString = 0x92 + 1;

		public const int FountainDryString = 0x93 + 1;

		public const int FountainStrengthString = 0x108 + 1;

		public const int FountainRefreshString = 0xFC + 1;

		public const int InstrumentStartString = 0x109 + 1;

		public const int InstrumentEndString = 0x10A + 1;

		/// <summary>go_fish's lines: caught, no luck, not there, got away.</summary>
		public const int FishCaughtString = 0x70 + 1;

		public const int FishNoLuckString = 0x71 + 1;

		public const int FishNotThereString = 0x72 + 1;

		public const int FishGetsAwayString = 0x73 + 1;

		// ------------------------------------------------- UseUnique (uw2 obj/USEITEMS.C:673, part 3)

		/// <summary>The stoppered bottle: "You are unable to remove the stopper." (0x8D).</summary>
		public const int StopperedBottle = 0x116;

		public const int StopperString = 0x8D + 1;

		/// <summary>The horn: "your lungs fail you" (0x161) before the djinn's X clock reaches 6, else the instrument with
		/// patch 2 (0x5C) and "the horn's power does not seem to be at its fullest" (0x162).</summary>
		public const int Horn = 0x111;

		public const int HornFailsString = 0x161 + 1;

		public const int HornPlayedString = 0x162 + 1;

		/// <summary>play_instrument(2): the third patch.</summary>
		public const int HornInstrument = 2;

		private const int HornDjinnStage = 6;

		/// <summary>The key gems 0x118-0x11F ask for a target (UseThing, UseKeyGem).</summary>
		public const int FirstKeyGem = 0x118;

		public const int LastKeyGem = 0x11F;

		private const int LargeBlackrockGem = 0x168;

		private const int GemsXClock = 2;

		private const int CastleXClock = 1;

		/// <summary>The fourth gem moves the castle plot on.</summary>
		private const int CastleGem = 4;

		public const int KeyGemFusesString = 0x152 + 1;

		public const int KeyGemInertString = 0x15B + 1;

		/// <summary>do_sfx and play_effect_here's sounds: 0x12 the fusing, 0x2A after it, 0x2C once all eight are used.</summary>
		public const int KeyGemSound = 0x12;

		public const int KeyGemAfterSound = 0x2A;

		public const int KeyGemAllSound = 0x2C;

		/// <summary>What the fusing's sounds add to their own volume: 0x28 for 0x12, 0x14 for 0x2A and 0x2C
		/// (USEITEMS.C:134-138; sound audit 2026-11-17).</summary>
		public const int KeyGemSoundVolume = 0x28;

		public const int KeyGemAfterSoundVolume = 0x14;

		public static bool IsKeyGem(int piId) => piId >= FirstKeyGem && piId <= LastKeyGem;

		// ------------------------------------------------- UseUtil (uw2 obj/USEITEMS.C:1055; 2026-11-18, uses part 2)

		/// <summary>The skulls and bones UseUtil asks a target for, from the pack only (UseBonesOn).</summary>
		public const int FirstBones = 0xC2;

		public const int LastBones = 0xC6;

		/// <summary>The anvil and the pole ask a target from the pack and in the world (UseAnvilOn, UsePoleOn).</summary>
		public const int Anvil = 0xD7;

		public const int Pole = 0xD8;

		/// <summary>UseBonesOn's resting place.</summary>
		public const int Gravestone = 0x165;

		/// <summary>"You thoughtfully give the bones a final resting place." (0x94); else NoEffectString.</summary>
		public const int BonesRestString = 0x94 + 1;

		/// <summary>"Using the pole you trigger the switch." (0xAB) and "The pole cannot be used on that." (0xAC).</summary>
		public const int PoleTriggersString = 0xAB + 1;

		public const int PoleCannotString = 0xAC + 1;

		/// <summary>CLASS_SWITCH, 0x170-0x17F: what UsePoleOn works.</summary>
		private const int SwitchClassMask = 0x1F0;

		private const int SwitchClassBase = 0x170;

		public static bool IsBones(int piId) => piId >= FirstBones && piId <= LastBones;

		public static bool IsSwitch(int piId) => (piId & SwitchClassMask) == SwitchClassBase;

		/// <summary>UseBonesOn: on a gravestone the line and the bones are used up (using_punt), else "It seems to have no
		/// effect.". Returns the line; pbUsedUp says whether the bones go.</summary>
		public static int BonesOn(int piTargetId, out bool pbUsedUp)
		{
			pbUsedUp = piTargetId == Gravestone;

			return pbUsedUp ? BonesRestString : NoEffectString;
		}

		/// <summary>Whether the horn plays: the djinn's X clock at 6 or more.</summary>
		public static bool HornSounds(UWPlayerFile pOFile) => pOFile != null && UWPlayerRecordUw2.XClock(pOFile, UWPlayerRecordUw2.DjinnClock) >= HornDjinnStage;

		/// <summary>What UseKeyGem did: its lines (our numbering), whether the key gem is used up, whether all eight are used now.</summary>
		public struct KeyGemResult
		{
			public List<int> Lines;

			public bool Fused;

			public bool AllUsed;

			/// <summary>do_sfx(4, gems before &lt;&lt; 2): the tremor's rounds, set_effect(0x40, arg &lt;&lt; 1) - 0 for the first gem,
			/// which still shakes one round at full strength (tremEfflen counts down from 0 to 255).</summary>
			public int ShakeRounds;
		}

		/// <summary>
		/// UseKeyGem (uw2 obj/USEITEMS.C:113): a key gem whose owner field is set (treated by Nystul) on the large blackrock
		/// gem in the world fuses: gem 4 moves the castle X clock on, 0x152, the gems' X clock up and its line (0x152 + the new
		/// count), the gem's bit in QB_GEMS_USED (gems 6 and 7 swap bits) and vars[6] the facet it shows; the key gem is used up.
		/// Anything else: "The key gem remains inert in your hand." (0x15B).
		/// </summary>
		public static KeyGemResult UseKeyGem(UWObject pOKeyGem, int piTargetId, bool pbCarried, UWPlayerFile pOFile)
		{
			KeyGemResult lOResult = new KeyGemResult { Lines = new List<int>() };

			if (pOFile == null || pOKeyGem == null || pbCarried || piTargetId != LargeBlackrockGem || pOKeyGem.Owner == 0)
			{
				lOResult.Lines.Add(KeyGemInertString);

				return lOResult;
			}

			int liGem = (pOKeyGem.ID & 0xF) - 7;

			if (liGem == CastleGem)
				UWPlayerRecordUw2.SetXClock(pOFile, CastleXClock, UWPlayerRecordUw2.XClock(pOFile, CastleXClock) + 1);

			int liBefore = UWPlayerRecordUw2.XClock(pOFile, GemsXClock);

			lOResult.ShakeRounds = (((liBefore << 2) & 0xFF) << 1) & 0xFF;
			lOResult.Lines.Add(KeyGemFusesString);

			int liCount = liBefore + 1;

			UWPlayerRecordUw2.SetXClock(pOFile, GemsXClock, liCount);
			lOResult.Lines.Add(KeyGemFusesString + liCount);

			if ((liGem & 6) == 6)
				liGem = 13 - liGem;

			int liUsed = UWPlayerRecordUw2.GetNumbered(pOFile, UWTrapRulesUw2.FirstQuestByte + UWTrapRulesUw2.GemsUsedQuestByte) | (1 << (liGem - 1));

			UWPlayerRecordUw2.SetNumbered(pOFile, UWTrapRulesUw2.FirstQuestByte + UWTrapRulesUw2.GemsUsedQuestByte, liUsed & 0xFF);
			UWPlayerRecordUw2.SetNumbered(pOFile, UWTrapRulesUw2.GemFacetVariable, liGem - 1);

			lOResult.Fused = true;
			lOResult.AllUsed = (liUsed & 0xFF) == 0xFF;

			return lOResult;
		}

		/// <summary>An animation object of class 9 stands over a fountain and uses it (UseObj's MAJOR_ANIMOBJ case).</summary>
		public static bool IsFountainAnimation(int piId) => (piId & 0x1C0) == 0x1C0 && (piId & 0xF) == 9;

		public static bool IsBoulder(int piId) => piId >= FirstBoulder && piId <= SmallBoulder;

		/// <summary>What UseOilOn does to a carried thing.</summary>
		public struct OilResult
		{
			/// <summary>The line (our numbering).</summary>
			public int Message;

			/// <summary>The thing changes: one is taken off a stack first.</summary>
			public bool Changes;

			/// <summary>The id it gets (its own when only the quality changes).</summary>
			public int NewId;

			public int NewQuality;

			/// <summary>The flask is used up.</summary>
			public bool FlaskUsed;
		}

		/// <summary>UseOilOn on a carried thing of this id and quality.</summary>
		public static OilResult Oil(int piId, int piQuality)
		{
			int liOff = piId == Lantern || piId == LitLantern ? 0 : TorchLines;

			if (piId >= FirstWood && piId <= LastWood)
				return new OilResult { Message = WoodTorchString, Changes = true, NewId = Torch, NewQuality = WoodTorchQuality, FlaskUsed = true };

			if (piId == Lantern || piId == Torch)
			{
				if (piQuality == FullQuality)
					return new OilResult { Message = liOff + FullString };

				return new OilResult
				{
					Message = liOff + RefuelledString,
					Changes = true,
					NewId = piId,
					NewQuality = piQuality < OilFill ? piQuality + OilFill : FullQuality,
					FlaskUsed = true
				};
			}

			if (piId == LitLantern || piId == LitTorch)
				return new OilResult { Message = liOff + LitString };

			return new OilResult { Message = CannotOilString };
		}

		/// <summary>One piece of a broken boulder: its id, and the count of a sling stone pile (0 for none).</summary>
		public struct RockPiece
		{
			public int ObjectId;

			public int Quantity;
		}

		/// <summary>UseRockHammerOn on a thing of this id in the world: the pieces, none for no boulder (then pbBroken false).</summary>
		public static List<RockPiece> BreakRock(int piId, out bool pbBroken)
		{
			List<RockPiece> lOPieces = new List<RockPiece>();

			pbBroken = IsBoulder(piId);

			if (!pbBroken)
				return lOPieces;

			for (int liCount = UWRandom.Next(2) + (MediumBoulder - piId) + 1; liCount > 0; liCount--)
			{
				int liPiece = piId + UWRandom.Next(2) + 1;

				lOPieces.Add(liPiece > SmallBoulder
					? new RockPiece { ObjectId = SlingStone, Quantity = (UWRandom.Next(0x8000) % 6) + 3 }
					: new RockPiece { ObjectId = liPiece });
			}

			return lOPieces;
		}

		/// <summary>The fountain's line, and its spell to cast (piMajor -1 and false for none).</summary>
		public static int FountainLine(UWObject pOFountain, IList<UWObject> pORecords, out bool pbSpell, out int piMajor, out int piEffect)
		{
			pbSpell = false;
			piMajor = -1;
			piEffect = 0;

			if (pOFountain == null || pOFountain.Quality == 0)
				return FountainDryString;

			if (!UWEnchantSpellRulesUw2.TryDecode(pOFountain, pORecords, out UWObject _, out piMajor, out piEffect, out bool _))
				return FountainRefreshString;

			pbSpell = true;

			return piMajor == StrengthClass ? FountainStrengthString : FountainRefreshString;
		}
	}
}
