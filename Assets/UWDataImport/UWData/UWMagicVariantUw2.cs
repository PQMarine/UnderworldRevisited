using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S MAGIC STEPS (UW2 plan phase 4, list item 3): its spell table of 64 rune spells and five
	/// that live in things, eight to a circle; Casting against three times the circle, the rule set's; no spell above
	/// circle 3 in Britannia; a target spell paid when it is released; its own sounds and strings (uw2 combat/RUNES.C,
	/// SPELLS.C). IsBuilt says which classes run so far, step by step.
	/// </summary>
	public sealed class UWMagicVariantUw2 : IUWMagicVariant
	{
		private const int LevelsPerWorld = 8;

		/// <summary>Britannia allows no more (RUNES.C:232).</summary>
		private const int LastCircleInBritannia = 3;

		private const int FailStrings = 0xE1 + 1;

		private const int NotReadyString = 0xB + 1;

		private const int HealedMessage = 0x124 + 1;

		private const int NoSound = 0xFF;

		/// <summary>Stage 1: light, motion, armour, protection, healing.</summary>
		private const int LastLastingOrHealClass = 4;

		/// <summary>Iron Flesh's glaze (do_spell, SPELLS.C:97): class 2 minor 5 while the djinn quest stands at 4,
		/// "The baked mud hardens into a clear glaze."</summary>
		private const int IronFleshMinor = 5;

		private const int DjinnMudStage = 4;

		private const int DjinnGlazedStage = 5;

		private const int GlazeString = 0x14F + 1;

		/// <summary>spells[] (uw2 combat/SPELLS.C:1033): index, class, minor and runes as the game's data has them.</summary>
		private static readonly UWRunicMagic.Spell[] mOSpells =
		{
			new UWRunicMagic.Spell(0, 8, 1, 8599),
			new UWRunicMagic.Spell(1, 3, 1, 1480),
			new UWRunicMagic.Spell(2, 5, 1, 14648),
			new UWRunicMagic.Spell(3, 2, 2, 1298),
			new UWRunicMagic.Spell(4, 7, 13, 22840),
			new UWRunicMagic.Spell(5, 0, 131, 8568),
			new UWRunicMagic.Spell(6, 1, 6, 20591),
			new UWRunicMagic.Spell(7, 11, 3, 1732),
			new UWRunicMagic.Spell(8, 7, 1, 16472),
			new UWRunicMagic.Spell(9, 3, 10, 16386),
			new UWRunicMagic.Spell(10, 5, 5, 15049),
			new UWRunicMagic.Spell(11, 4, 2, 8236),
			new UWRunicMagic.Spell(12, 8, 2, 8361),
			new UWRunicMagic.Spell(13, 1, 2, 17519),
			new UWRunicMagic.Spell(14, 1, 1, 20984),
			new UWRunicMagic.Spell(15, 11, 13, 16791),
			new UWRunicMagic.Spell(16, 7, 0, 9624),
			new UWRunicMagic.Spell(17, 11, 6, 440),
			new UWRunicMagic.Spell(18, 7, 8, 302),
			new UWRunicMagic.Spell(19, 5, 2, 14552),
			new UWRunicMagic.Spell(20, 0, 133, 16760),
			new UWRunicMagic.Spell(21, 6, 133, 322),
			new UWRunicMagic.Spell(22, 11, 0, 18031),
			new UWRunicMagic.Spell(23, 1, 68, 24056),
			new UWRunicMagic.Spell(24, 6, 135, 8197),
			new UWRunicMagic.Spell(25, 4, 4, 8600),
			new UWRunicMagic.Spell(26, 3, 11, 13623),
			new UWRunicMagic.Spell(27, 7, 10, 312),
			new UWRunicMagic.Spell(28, 3, 70, 18616),
			new UWRunicMagic.Spell(29, 2, 67, 8792),
			new UWRunicMagic.Spell(30, 7, 7, 22936),
			new UWRunicMagic.Spell(31, 3, 5, 6735),
			new UWRunicMagic.Spell(32, 1, 3, 20719),
			new UWRunicMagic.Spell(33, 5, 3, 15544),
			new UWRunicMagic.Spell(34, 7, 11, 15063),
			new UWRunicMagic.Spell(35, 7, 12, 4856),
			new UWRunicMagic.Spell(36, 7, 2, 76),
			new UWRunicMagic.Spell(37, 8, 3, 8809),
			new UWRunicMagic.Spell(38, 7, 9, 18007),
			new UWRunicMagic.Spell(39, 11, 8, 14839),
			new UWRunicMagic.Spell(40, 7, 3, 16950),
			new UWRunicMagic.Spell(41, 6, 66, 21958),
			new UWRunicMagic.Spell(42, 0, 134, 21771),
			new UWRunicMagic.Spell(43, 7, 15, 22063),
			new UWRunicMagic.Spell(44, 4, 15, 21772),
			new UWRunicMagic.Spell(45, 3, 68, 22091),
			new UWRunicMagic.Spell(46, 13, 7, 22680),
			new UWRunicMagic.Spell(47, 7, 5, 143),
			new UWRunicMagic.Spell(48, 6, 131, 21526),
			new UWRunicMagic.Spell(49, 7, 14, 21975),
			new UWRunicMagic.Spell(50, 6, 129, 14352),
			new UWRunicMagic.Spell(51, 6, 134, 9464),
			new UWRunicMagic.Spell(52, 8, 5, 10252),
			new UWRunicMagic.Spell(53, 11, 9, 22007),
			new UWRunicMagic.Spell(54, 11, 1, 21655),
			new UWRunicMagic.Spell(55, 8, 6, 14822),
			new UWRunicMagic.Spell(56, 6, 132, 5368),
			new UWRunicMagic.Spell(57, 1, 5, 21743),
			new UWRunicMagic.Spell(58, 11, 11, 632),
			new UWRunicMagic.Spell(59, 2, 69, 8882),
			new UWRunicMagic.Spell(60, 11, 2, 21912),
			new UWRunicMagic.Spell(61, 11, 7, 14838),
			new UWRunicMagic.Spell(62, 7, 6, 21804),
			new UWRunicMagic.Spell(63, 11, 12, 21826),
			new UWRunicMagic.Spell(64, 6, 5, 25368),
			new UWRunicMagic.Spell(65, 5, 4, 25368),
			new UWRunicMagic.Spell(66, 11, 13, 25368),
			new UWRunicMagic.Spell(67, 10, 3, 25368),
			new UWRunicMagic.Spell(68, 10, 9, 25368),
		};

		public IReadOnlyList<UWRunicMagic.Spell> Spells => mOSpells;

		public int SpellsPerCircle => 8;

		public int FailStringBase => FailStrings;

		public int HealedString => HealedMessage;

		/// <summary>Stage 1 the classes up to 4; stage 2 part A the backfire, mana, class 11 (but Roaming Sight)
		/// and class 13 (but Mind Blast and the two missiles); part B the area spells, class 6.</summary>
		public bool IsBuilt(UWRunicMagic.Spell pOSpell)
		{
			int liMinor = UWRunicMagic.GetEffectMinor(pOSpell.MinorClass);

			switch (pOSpell.MajorClass)
			{
				case UWSpellCasting.BackfireMajorClass:
				case UWSpellCasting.ManaMajorClass:
					return true;

				case UWExtraSpellRulesUw2.MajorClass:
					return UWExtraSpellRulesUw2.IsBuilt(liMinor);

				case UWSpecialSpellRulesUw2.MajorClass:
					return UWSpecialSpellRulesUw2.IsBuilt(liMinor);

				case UWAreaSpellRulesUw2.MajorClass:
					return UWAreaSpellRulesUw2.IsBuilt(liMinor);

				default:
					return pOSpell.MajorClass <= LastLastingOrHealClass;
			}
		}

		/// <summary>Classes 11 and 13 by the second game's own tables (do_spell's cases return 1: they always take);
		/// Iron Flesh's glaze before the shared lasting spell.</summary>
		public bool TryApplyOwn(UWRunicMagic.Spell pOSpell, int piMinor, IUWSpellHost pIHost, out bool pbTook)
		{
			pbTook = true;

			switch (pOSpell.MajorClass)
			{
				case UWExtraSpellRulesUw2.MajorClass:
					UWExtraSpellRulesUw2.Cast(piMinor, UWSpellCasting.RollStability(UWRunicMagic.GetStabilityClass(pOSpell.MinorClass)), pIHost);

					if (pIHost.HasPlayer)
						pIHost.RefreshSpellIcons();

					return true;

				case UWSpecialSpellRulesUw2.MajorClass:
					UWSpecialSpellRulesUw2.Cast(piMinor, pIHost);

					return true;

				// Stage 2 part B, the area spells by the second game's table (do_spell's case 6 always takes).
				case UWAreaSpellRulesUw2.MajorClass:
					UWAreaSpellRulesUw2.Cast(pOSpell.MinorClass, pIHost);

					return true;

				case UWSpellCasting.ArmourMajorClass:
					if (piMinor == IronFleshMinor && pIHost is IUWSpellHostUw2 lIPlayer && pIHost.HasPlayer
						&& UWPlayerRecordUw2.XClock(lIPlayer.Record, UWPlayerRecordUw2.DjinnClock) == DjinnMudStage)
					{
						pIHost.World.Presentation.AddGeneralMessage(GlazeString);
						UWPlayerRecordUw2.SetXClock(lIPlayer.Record, UWPlayerRecordUw2.DjinnClock, DjinnGlazedStage);
					}

					break;
			}

			pbTook = false;

			return false;
		}

		public bool MayCast(IUWSpellHost pIHost)
		{
			return pIHost.CanAct;
		}

		public bool MayCastHere(IUWSpellHost pIHost, int piCircle)
		{
			UWLevel lOLevel = UWSpellCasting.LevelOf(pIHost);

			return lOLevel == null || (lOLevel.LevelNumber - 1) / LevelsPerWorld != 0 || piCircle <= LastCircleInBritannia;
		}

		/// <summary>SPELLC_MISSILE and SPELLC_1AREA keep the cost until they are released (RUNES.C:249).</summary>
		public bool PaysOnRelease(UWRunicMagic.Spell pOSpell)
		{
			return pOSpell.MajorClass == UWSpellCasting.ProjectileMajorClass || pOSpell.MajorClass == UWSpellCasting.TargetMajorClass;
		}

		public void TooSoon(IUWSpellHost pIHost)
		{
			pIHost.World.Presentation.PlaySoundAtPlayer(UWSpellCasting.TooSoonSound);
			pIHost.World.Presentation.AddGeneralMessage(NotReadyString);
		}

		public void PlayCastSound(IUWSpellHost pIHost, UWRunicMagic.Spell pOSpell)
		{
			int liSound = fCastSound(pOSpell.MajorClass, pOSpell.MinorClass);

			if (liSound != NoSound)
				pIHost.World.Presentation.PlaySoundAtPlayer(liSound);
		}

		/// <summary>player_cast's switch (RUNES.C:255).</summary>
		private static int fCastSound(int piClass, int piMinor)
		{
			switch (piClass)
			{
				case 0:
				case 3:
					return 0x2A;

				case 2:
					return 0x2C;

				case 5:
					return NoSound;

				case 6:
					return piMinor != 0x81 ? 0x2B : 0x2A;

				case 7:
					return piMinor == 0 || piMinor == 2 || piMinor == 6 || piMinor == 8 ? 0x2B : 0x2A;

				case 8:
					if (piMinor == 5)
						return 0x29;

					break;

				case 1:
				case 4:
				case 9:
				case 10:
				case 11:
					break;

				default:
					return 0x10;
			}

			return piMinor == 0xC ? 0x29 : piMinor == 9 ? 0x28 : 0x10;
		}
	}
}
