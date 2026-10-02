using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Spell class 11 - the bag full of special cases (the reference: spellcasting_class_11,
	/// CastClassB_Spells). Fourteen spells that have nothing to do with each other.
	///
	/// ALL FOURTEEN ARE BUILT:
	///
	///   0    Speed              speeds up walking, expires like any lasting spell
	///   1    Detect Monsters    reports where critters are
	///   2    Strengthen Door    does NOTHING - that is how it is in the original
	///   3    Remove Trap        disarms the trap on a clicked object
	///   4    Name Enchantment   permanently identifies a clicked object
	///   5    Unlock             unlocks a clicked door
	///   6    Cure Poison        poison gone
	///   7    Roaming Sight      the view detaches from the body and travels across the level
	///   8    Telekinesis        the hand reaches further, expires like a lasting spell
	///   9    Tremor             makes boulders fall from the ceiling
	///   0xA  Gate Travel        jumps to the moonstone
	///   0xB  Freeze Time        the critters no longer move, lasting spell
	///   0xC  Armageddon         clears inventory, rune bag and all objects of the world
	///   0xD  Dispel Hunger      hunger to 0xC0, i.e. sated
	///
	/// Engine-free since 2026-09-18 (P3): the three spells that go onto a CLICKED target
	/// (Remove Trap, Name Enchantment, Unlock) only set the target cursor here; what happens
	/// on the click is component work and stays in UWMiscSpell on the Unity side.
	/// </summary>
	public static class UWMiscSpellRules
	{
		/// <summary>
		/// THE LASTING SPELLS OF CLASS 11 REGISTER UNDER DIFFERENT MINOR CLASSES than the ones
		/// they are cast with. The reference explicitly notes the change at each of the four
		/// places ("Note the change in mapping here"):
		///
		///   cast 0xB Freeze Time   -> effect 0
		///   cast 7   Roaming Sight -> effect 1
		///   cast 0   Speed         -> effect 2
		///   cast 8   Telekinesis   -> effect 3
		///
		/// This is evaluated in UWCharacter.fApplyActiveSpells.
		/// </summary>
		public const int FreezeTimeEffectMinor = 0;

		/// <summary>See FreezeTimeEffectMinor. UWRoamingSight depends on this.</summary>
		public const int RoamingSightEffectMinor = 1;

		/// <summary>See FreezeTimeEffectMinor.</summary>
		public const int SpeedEffectMinor = 2;

		/// <summary>See FreezeTimeEffectMinor.</summary>
		public const int TelekinesisEffectMinor = 3;

		/// <summary>Spell class 11.</summary>
		public const int MajorClass = 11;

		public const int SpeedSpell = 0x0;
		public const int DetectMonstersSpell = 0x1;
		public const int StrengthenDoorSpell = 0x2;
		public const int RemoveTrapSpell = 0x3;
		public const int NameEnchantmentSpell = 0x4;
		public const int UnlockSpell = 0x5;
		public const int CurePoisonSpell = 0x6;
		public const int RoamingSightSpell = 0x7;
		public const int TelekinesisSpell = 0x8;
		public const int TremorSpell = 0x9;
		public const int GateTravelSpell = 0xA;
		public const int FreezeTimeSpell = 0xB;
		public const int ArmageddonSpell = 0xC;
		public const int DispelHungerSpell = 0xD;

		/// <summary>What Dispel Hunger sets hunger to - sated, but not overfed.</summary>
		private const int DispelHungerValue = 0xC0;

		/// <summary>
		/// Casts a class 11 spell. Returns false for an unknown spell number or when a
		/// precondition is missing (no player, no level, no free effect slot) -
		/// the caller should then behave as before.
		/// </summary>
		public static bool Cast(int piMinorClass, int piStability, IUWSpellHost pIHost)
		{
			if (pIHost == null || !pIHost.HasPlayer)
				return false;

			switch (piMinorClass)
			{
				case SpeedSpell:
					return pIHost.TryAddActiveSpell(MajorClass, SpeedEffectMinor, piStability);

				case DetectMonstersSpell:
					return fDetectCreatures(pIHost, DetectRange, DetectSkill);

				case StrengthenDoorSpell:
					// DELIBERATELY NOTHING. The reference notes at this point that the spell
					// does nothing in the original ("appears to be bugged and does nothing"). It still
					// costs its mana, and exactly that happens here too.
					return true;

				case RemoveTrapSpell:
				case NameEnchantmentSpell:
				case UnlockSpell:
					pIHost.BeginTargetSpell(MajorClass, piMinorClass);

					return true;

				case CurePoisonSpell:
					pIHost.CurePoison();

					return true;

				case RoamingSightSpell:
					// Only register it. The camera depends on the EFFECT, not on the casting - see
					// UWCharacter.fApplyActiveSpells and UWRoamingSight. If no effect slot
					// is free, nothing happens to the camera either; the reference detaches it at
					// this point before it has even checked that.
					return pIHost.TryAddActiveSpell(MajorClass, RoamingSightEffectMinor, piStability);

				case TelekinesisSpell:
					return pIHost.TryAddActiveSpell(MajorClass, TelekinesisEffectMinor, piStability);

				case TremorSpell:
					return fTremor(pIHost);

				case GateTravelSpell:
					return fGateTravel(pIHost);

				case FreezeTimeSpell:
					return pIHost.TryAddActiveSpell(MajorClass, FreezeTimeEffectMinor, piStability);

				case ArmageddonSpell:
					return fArmageddon(pIHost);

				case DispelHungerSpell:
					pIHost.ChangeHunger(DispelHungerValue - pIHost.PlayerHunger);

					return true;

				default:
					return false;
			}
		}

		/// <summary>How far the spell looks: a creature counts while BOTH tile distances are
		/// BELOW this (DetectMonster_seg038_134E, called with 0x0A). The reference said eight,
		/// and ours counted up to eight inclusive until 2026-09-24.</summary>
		public const int DetectRange = 0x0A;

		/// <summary>The skill it searches with - fixed in the spell, not one of the player's.
		/// </summary>
		public const int DetectSkill = 0x2D;

		/// <summary>How far the track skill looks - see Track.</summary>
		public const int TrackRange = 8;

		/// <summary>
		/// THE TRACK SKILL, F9 (read 2026-09-27): the key calls ovr143_70 with 2, which reads the
		/// skill byte at PLAYER.DAT 0x2B + 2 (0x2D, Track) and hands it with number 0x0C to
		/// ovr143_0; for 0x0C that routine calls the same search as Detect Monsters, only with range
		/// 8 instead of 0x0A and the player's Track value instead of the spell's fixed 0x2D. No
		/// mana, no training, no other message. (Numbers 0x0A and 0x0B do nothing there, any other
		/// writes the skill's name - no key calls those.) Returns false without a player or level.
		/// </summary>
		public static bool Track(IUWSpellHost pIHost, int piTrackSkill)
		{
			if (pIHost == null || !pIHost.HasPlayer)
				return false;

			return fDetectCreatures(pIHost, TrackRange, piTrackSkill);
		}

		/// <summary>The difficulty is this minus the kind's stealth (GetDetectDifficulty).</summary>
		private const int DetectDifficultyBase = 0xF;

		/// <summary>
		/// How hard a creature is to notice: 0xF minus the LOW NIBBLE of critter table byte 0x1D
		/// (read 2026-09-24; the same nibble the creatures use as loudness of a target,
		/// UWObjectClassProperties.Critter.LoudnessAsTarget). Until then ours used the hardest
		/// case for every kind.
		/// </summary>
		public static int GetDetectDifficulty(int piStealthNibble)
		{
			return DetectDifficultyBase - (piStealthNibble & 0xF);
		}

		/// <summary>
		/// THE SECOND MESSAGE (read 2026-09-24): after the one for the first direction with the
		/// most finds, the original starts at a random direction and names the first OTHER one
		/// whose count is above the smaller of the most and three. So it only speaks twice when
		/// two directions hold four or more each - many from two sides. The reference took this
		/// for a bug (a compare against an overwritten value); the overwrite IS the threshold.
		/// Returns the direction, or -1.
		/// </summary>
		public static int GetSecondDirection(int[] piPerDirection, int piFirst, int piStart)
		{
			int liMost = 0;

			for (int liAt = 0; liAt < piPerDirection.Length; liAt++)
				liMost = System.Math.Max(liMost, piPerDirection[liAt]);

			int liThreshold = System.Math.Min(liMost, SecondMessageThreshold);

			for (int liStep = 0; liStep < 8; liStep++)
			{
				int liDirection = (piStart + liStep) & 7;

				if (liDirection != piFirst && piPerDirection[liDirection] > liThreshold)
					return liDirection;
			}

			return -1;
		}

		/// <summary>See GetSecondDirection.</summary>
		private const int SecondMessageThreshold = 3;

		// Numbers in string block 1, in OUR counting - each one above the reference's.
		// Its DirectionOffset 0x24 and DetectionsOffset 0x3B give 37 and 60; the 60 is
		// confirmed against our dump of the block ("You detect a creature ").
		private const int FirstDirectionMessage = UWTrapRules.FirstDirectionMessage;

		private const int OneCreatureMessage = 60;

		private const int NoActivityMessage = 63;

		/// <summary>
		/// Detect Monsters (DetectMonster_seg038_134E, read 2026-09-24): searches for creatures
		/// nearby and reports from which direction the most come.
		///
		/// Every living creature whose tile lies less than DetectRange away on both axes is
		/// found when a check with DetectSkill against its kind's difficulty
		/// (GetDetectDifficulty) succeeds; the finds are counted by the eight compass
		/// directions. The message names the first direction with the most finds and chooses
		/// its wording by their count: one creature, a few (two to four), many (five or more).
		/// A SECOND message may follow for another direction - see GetSecondDirection.
		///
		/// The same search serves the track skill (Track) with a smaller range and the player's
		/// own skill value.
		/// </summary>
		private static bool fDetectCreatures(IUWSpellHost pIHost, int piRange, int piSkill)
		{
			if (pIHost.CurrentLevel == null)
				return false;

			UWTilePos lOPlayerTile = pIHost.PlayerTile;

			int[] liPerDirection = new int[8];

			IReadOnlyList<CreatureSighting> lOCreatures = pIHost.Creatures;

			for (int liAt = 0; lOCreatures != null && liAt < lOCreatures.Count; liAt++)
			{
				int liDeltaX = lOCreatures[liAt].Tile.X - lOPlayerTile.X;
				int liDeltaY = lOCreatures[liAt].Tile.Y - lOPlayerTile.Y;

				if (System.Math.Abs(liDeltaX) >= piRange || System.Math.Abs(liDeltaY) >= piRange)
					continue;

				UWObjectClassProperties.Critter lOKind;
				int liStealth = pIHost.Data != null && pIHost.Data.ObjectClassProperties != null
					&& pIHost.Data.ObjectClassProperties.TryGetCritter(lOCreatures[liAt].ObjectId, out lOKind)
					? lOKind.LoudnessAsTarget : 0;

				if (!UWSkillCheck.IsSuccess(UWSkillCheck.Check(piSkill, GetDetectDifficulty(liStealth))))
					continue;

				// The original's eight directions, not a rounded angle: a straight direction only when
				// one distance is more than twice the other (UWShrineRules.GetCardinalDirection). With
				// the rounded angle ours counted 4 north and 4 east at the troll camp on level 7 and gave
				// the second line nearly every time; the original counts 3, 2 and 3 there and gave it
				// once in 30 (per user, 2026-09-24).
				liPerDirection[UWShrineRules.GetCardinalDirection(lOPlayerTile.X, lOPlayerTile.Y,
					lOCreatures[liAt].Tile.X, lOCreatures[liAt].Tile.Y)]++;
			}

			int liBest = 0;
			int liMost = 0;

			for (int liAt = 0; liAt < liPerDirection.Length; liAt++)
			{
				if (liPerDirection[liAt] <= liMost)
					continue;

				liMost = liPerDirection[liAt];
				liBest = liAt;
			}

			if (liMost == 0)
			{
				pIHost.AddGeneralMessage(NoActivityMessage);

				return true;
			}

			fAddDetection(pIHost, liBest, liMost);

			int liSecond = GetSecondDirection(liPerDirection, liBest, UWRandom.Next(8));

			if (liSecond >= 0)
				fAddDetection(pIHost, liSecond, liPerDirection[liSecond]);

			return true;
		}

		/// <summary>One line: one, a few or many creatures, and the direction
		/// (DetectMonsterString_seg038_3307_12FD).</summary>
		private static void fAddDetection(IUWSpellHost pIHost, int piDirection, int piCount)
		{
			int liCountMessage = OneCreatureMessage + (piCount > 1 ? 1 : 0) + (piCount > 4 ? 1 : 0);

			pIHost.AddMessage(pIHost.GetGeneralMessage(liCountMessage)
				+ pIHost.GetGeneralMessage(FirstDirectionMessage + piDirection));
		}

		// ------------------------------------------------- Tremor

		/// <summary>The three boulders the spell drops: 0x154 "a_large boulder",
		/// 0x155 "a_boulder", 0x156 "a_small boulder" (names from string block 4).</summary>
		private const int FirstBoulderId = 0x154;

		private const int BoulderKinds = 3;

		/// <summary>How far in front of the caster the centre of the affected area lies.
		/// </summary>
		private const int TremorDistance = 5;

		/// <summary>Half the edge length of the affected area, in tiles.</summary>
		private const int TremorRadius = 3;

		/// <summary>Duration of the shaking, the value the reference uses (on
		/// UWScreenShake.LargeChannel).</summary>
		private const int TremorShakeDuration = 0x28;

		/// <summary>
		/// The rumble that goes with the shaking. MajorSpellClassB_seg038_1645 plays effect
		/// 0x12 at the caster right after it sets the screen shaking; our constant for it sat
		/// unused until 2026-09-21, which is how the coverage sweep found this.
		/// </summary>
		public const int TremorSound = 0x12;

		/// <summary>The maximum number of boulders that fall: eight three-sided dice, so eight to
		/// twenty-four.</summary>
		private const int TremorDiceCount = 8;

		private const int TremorDieSides = 3;

		/// <summary>
		/// Tremor: boulders fall from the ceiling.
		///
		/// READ IN UW.EXE 2026-09-27 (MajorSpellClassB case 9 and QuakeSpell_seg038_3307_1211; per
		/// user "carry on with the player's Tremor"): the spell hands the tile-mode area walk the
		/// hit count DiceRoll(8, 3) (8 to 24), distance 5 and radius 3, so the area lies IN FRONT
		/// of the caster, the square from centre - 3 to centre + 4 both ways, with up to five
		/// passes (UWAreaSpellRules.StrikeTiles, the same walk as Sheet Lightning and Flame Wind).
		/// On every struck tile QuakeSpell makes one boulder, 0x154 + RNG % 3, at sub-position 3/3
		/// and zpos 0x6E, and returns 1 even when the boulder could not be placed - every stroke
		/// uses a hit. Then the screen shakes and effect 0x12 sounds at the caster.
		///
		/// Until 2026-09-27 ours walked the reference's version: the inverted per-tile test (it
		/// skipped a tile when the roll was at or below the boulders left), one pass, the square
		/// only to centre + radius, and a boulder that could not be placed did not count.
		///
		/// NO IMPACT DAMAGE, AND THAT IS CONFIRMED. The user verified it in the original
		/// (2026-09-10, with the Tremor scroll from level 6 on the goblins of level 1): they took
		/// no damage and did not even turn hostile. A rockfall is pure scenery; the boulders stay
		/// on the ground and can be picked up.
		/// </summary>
		private static bool fTremor(IUWSpellHost pIHost)
		{
			UWLevel lOLevel = pIHost.CurrentLevel;

			if (lOLevel == null || lOLevel.TileData == null)
				return false;

			UWAreaSpellRules.StrikeTiles(UWAreaSpellRules.GetCentreTile(pIHost, TremorDistance), TremorRadius,
				UWRandom.RollDice(TremorDiceCount, TremorDieSides), pIHost,
				pOTile =>
				{
					pIHost.DropBoulder(FirstBoulderId + UWRandom.Next(BoulderKinds), pOTile.X, pOTile.Z);

					return 1;
				});

			pIHost.ShakeScreen(TremorShakeDuration);
			pIHost.PlaySoundAtPlayer(TremorSound);

			return true;
		}

		// ------------------------------------------------- Armageddon

		/// <summary>
		/// Armageddon: everything is gone.
		///
		/// The spell clears in one go the inventory, the rune bag including the rune shelf, and
		/// ALL objects of the level - items, critters, doors, triggers. What remains
		/// is an empty stone cave. The marker for it stays set: every further level
		/// is cleared the same way on entering (see UWLevelLoader.ArmageddonActive).
		///
		/// In Underworld 1 the spell additionally sets the silver tree level to zero, which
		/// disables resurrection at the silver tree (see UWLevelLoader.ApplyArmageddon).
		///
		/// NO MESSAGE - the original does not output one either. All of it is engine work
		/// (IUWSpellHost.Armageddon); the rune bag in the player data is cleared here.
		/// </summary>
		private static bool fArmageddon(IUWSpellHost pIHost)
		{
			if (pIHost.CurrentLevel == null)
				return false;

			if (pIHost.Data != null && pIHost.Data.InitialPlayer != null)
				pIHost.Data.InitialPlayer.ClearRunes();

			pIHost.Armageddon();

			return true;
		}

		// ------------------------------------------------- Gate Travel

		/// <summary>String block 1, our counting: "The moonstone is not available." The
		/// reference gives 0x111, i.e. 273.</summary>
		private const int NoMoonstoneMessage = 274;

		/// <summary>
		/// Gate Travel: puts the player where the moonstone lies.
		///
		/// Which level that is on is kept up to date while playing (UWGameFlags.MoonstoneLevel,
		/// see UWMoonstoneRules). If it says zero, there is no reachable stone and the spell
		/// only reports that.
		///
		/// DEVIATION: the original first teleports to the target level and then lets a
		/// follow-up step search for the stone (CallBackGateTravel_ovr143_15F4). We search for
		/// it beforehand - the level data are all in memory anyway - and jump to the right place
		/// directly. If none is found, the player stays put instead of landing in the middle of
		/// a foreign level.
		/// </summary>
		private static bool fGateTravel(IUWSpellHost pIHost)
		{
			if (pIHost.Data == null || pIHost.Data.Levels == null)
				return false;

			int liLevel = UWGameFlags.MoonstoneLevel;

			if (liLevel <= 0 || liLevel > pIHost.Data.Levels.Count)
			{
				pIHost.AddGeneralMessage(NoMoonstoneMessage);

				return true;
			}

			UWObject lOStone = UWMoonstoneRules.FindHolderInLevel(pIHost.Data.Levels[liLevel - 1]);

			if (lOStone == null)
			{
				pIHost.AddGeneralMessage(NoMoonstoneMessage);

				return true;
			}

			pIHost.TravelTo(liLevel - 1, lOStone.TileX, lOStone.TileY);

			return true;
		}
	}
}
