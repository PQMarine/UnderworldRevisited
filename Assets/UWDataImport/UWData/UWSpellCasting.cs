namespace UWDataImport.UWData
{
	/// <summary>
	/// Casting a spell: the rules of rune casting (lookup, circle, mana, skill check, no-magic
	/// check, backfire) and the dispatch of a successful spell by major class. Engine-free
	/// since 2026-09-18 (P3 of the engine separation), out of UWGameUI; the game is reached
	/// through IUWSpellHost.
	///
	/// The dispatch follows the reference's dispatcher:
	///
	///   0 to 3   lasting effects on the character - light, movement, resistance, bonuses.
	///            They occupy one of the three icon slots and expire after their time.
	///   4        Healing, immediate.
	///   5        Projectiles - Magic Arrow, Electrical Bolt, Fireball, Acid.
	///   6        Effect on an area in front of the character, see UWAreaSpellRules.
	///   7        Spells that go to a clicked target, see UWTargetSpell (Unity side).
	///   8        Summoning and creating, see UWSummonSpellRules.
	///   10       Mana restoration, only from items.
	///   11       The bag of special cases, see UWMiscSpellRules.
	///
	/// Any other class costs mana and reports nothing.
	/// </summary>
	public static class UWSpellCasting
	{
		public enum OutcomeEnum
		{
			NotASpell,

			NotExperienced,

			NotEnoughMana,

			/// <summary>The skill check failed - the attempt cost nothing.</summary>
			Failed,

			/// <summary>No magic here (the void, a no-magic tile).</summary>
			Blocked,

			/// <summary>A critical failure: the mana is gone, nothing happens.</summary>
			Backfired,

			Cast
		}

		/// <summary>What the original shows when the runes are no spell - not in the string
		/// block, UWGameUI printed the same literal.</summary>
		private const string NotASpellText = "Not a spell";

		/// <summary>Messages from string block 1. The reference numbers each one lower - the
		/// usual offset in this project.</summary>
		private const int NotExperiencedMessage = 211;

		private const int NotEnoughManaMessage = 212;

		private const int IncantationFailedMessage = 213;

		private const int CastingNotSuccessfulMessage = 214;

		private const int SpellBackfiresMessage = 215;

		/// <summary>Up to here spells act lastingly on the character: light, movement,
		/// resistance, bonuses.</summary>
		private const int LastLastingMajorClass = 3;

		private const int HealMajorClass = 4;

		public const int ProjectileMajorClass = 5;

		/// <summary>Area spells - Sheet Lightning, Confusion, Reveal, Flame Wind.</summary>
		private const int AreaMajorClass = 6;

		/// <summary>Spells that go to a clicked target (UWTargetSpell).</summary>
		public const int TargetMajorClass = 7;

		/// <summary>Mana restoration - castable only via items, see
		/// UWCharacter.RestoreManaFromSpell.</summary>
		private const int ManaMajorClass = 10;

		/// <summary>This minor class heals fully - the Greater Heal.</summary>
		private const int FullHealMinorClass = 0xF;

		/// <summary>
		/// Casts the selected runes.
		///
		/// The rules come from the disassembled reference, the mana costs are cross-checked against the original:
		/// In Lor is spell 0, so circle 1, and costs 3 mana there - exactly circle
		/// times three (per user, 2026-09-03).
		///
		/// Order: lookup, circle, mana, skill check, no-magic check; the effect of a successful
		/// spell is applied by ApplyEffect.
		/// </summary>
		public static OutcomeEnum CastRunes(int piFirstRune, int piSecondRune, int piThirdRune, IUWSpellHost pIHost)
		{
			if (pIHost == null || !pIHost.HasPlayer)
				return OutcomeEnum.NotASpell;

			int liSequence = UWRunicMagic.GetSequence(piFirstRune, piSecondRune, piThirdRune);

			UWRunicMagic.Spell lOSpell;

			if (!UWRunicMagic.TryGetSpell(liSequence, out lOSpell))
			{
				pIHost.AddMessage(NotASpellText);

				return OutcomeEnum.NotASpell;
			}

			// The circle the character can cast at all grows with its level.
			if (((pIHost.PlayerLevel + 1) / 2) < lOSpell.Level)
			{
				pIHost.AddGeneralMessage(NotExperiencedMessage);

				return OutcomeEnum.NotExperienced;
			}

			if (pIHost.PlayerMana < lOSpell.ManaCost)
			{
				pIHost.AddGeneralMessage(NotEnoughManaMessage);

				return OutcomeEnum.NotEnoughMana;
			}

			// ORDER: first the check, then the mana. On a failure the attempt costs
			// nothing at all (tested per user in the original, 2026-09-03) - the reference, by contrast, deducts the
			// mana beforehand.
			UWSkillCheck.ResultEnum leResult = UWSkillCheck.Check(pIHost.PlayerCastingSkill, lOSpell.Level * 3);

			if (leResult == UWSkillCheck.ResultEnum.Failure)
			{
				pIHost.PlaySpellFailureSound();
				pIHost.AddGeneralMessage(IncantationFailedMessage);

				return OutcomeEnum.Failed;
			}

			// NO MAGIC HERE (void or tile with no-magic bit): UW.EXE
			// PlayerCastsValidSpell_ovr119_488 then sets the mana cost to 0 and reports error 3,
			// "Casting was not successful.", with the failure sound.
			if (pIHost.IsMagicBlocked)
			{
				pIHost.PlaySpellFailureSound();
				pIHost.AddGeneralMessage(CastingNotSuccessfulMessage);

				return OutcomeEnum.Blocked;
			}

			// ASSUMPTION: that a backfire costs the mana. Only the simple failure has been
			// checked; on a backfire the original casts a curse, so the attempt did
			// work - only wrongly.
			if (leResult == UWSkillCheck.ResultEnum.CriticalFailure)
			{
				pIHost.SpendMana(lOSpell.ManaCost);

				pIHost.PlaySpellFailureSound();
				pIHost.AddGeneralMessage(SpellBackfiresMessage);

				return OutcomeEnum.Backfired;
			}

			// A PROJECTILE PAYS WHEN IT IS RELEASED (Class5ProjectileSpells_seg038_4EE keeps the cost
			// in SpellManaCost and subtracts it only after PrepareProjectileObject found room): the
			// cost travels with the waiting spell. Every other spell pays now.
			int liDeferredCost = lOSpell.MajorClass == ProjectileMajorClass ? lOSpell.ManaCost : 0;

			if (liDeferredCost == 0)
				pIHost.SpendMana(lOSpell.ManaCost);

			// Successful: now the spell takes effect - with the casting sound (reference:
			// runicmagic.GetSpellSFX, which in uw1 returns the same number for everything).
			pIHost.PlaySpellSound();
			ApplyEffect(lOSpell, false, pIHost, liDeferredCost);

			return OutcomeEnum.Cast;
		}

		/// <summary>
		/// Casts the spell of an item - a wand, for example.
		///
		/// WITHOUT the rules of rune casting: no circle, no mana, no skill check.
		/// The reference calls its path for this CastRunicSpellWithoutRules; the item has
		/// its charge, and that is the price.
		/// </summary>
		/// <param name="pbFireImmediately">For a projectile spell send it off immediately, instead of
		/// waiting for another click - the click that applied the wand already gives the
		/// direction.</param>
		/// <returns>True if this spell exists and was cast.</returns>
		public static bool CastFromObject(int piSpellIndex, bool pbFireImmediately, IUWSpellHost pIHost)
		{
			if (pIHost == null || piSpellIndex < 0 || piSpellIndex >= UWRunicMagic.AllSpells.Count)
				return false;

			ApplyEffect(UWRunicMagic.AllSpells[piSpellIndex], true, pIHost);

			if (pbFireImmediately)
				pIHost.FirePendingSpell();

			return true;
		}

		/// <summary>
		/// Casts a spell via its classes instead of via its place in the spell list -
		/// for the a_spelltrap, which carries only major and minor class (see
		/// UWTrapRules).
		///
		/// The minor class of a trap is the PURE minor class; in the spell list, its
		/// upper bits also hold duration and target type. For the duration that means the
		/// lowest class, and that is consistent for a trap too - its spell should not
		/// last longer than the weakest one.
		/// </summary>
		public static bool CastByClass(int piMajorClass, int piMinorClass, IUWSpellHost pIHost)
		{
			if (pIHost == null)
				return false;

			ApplyEffect(new UWRunicMagic.Spell(-1, piMajorClass, piMinorClass, 0), true, pIHost);

			return true;
		}

		/// <summary>The effect of a successful spell, by major class - see the class comment.</summary>
		public static void ApplyEffect(UWRunicMagic.Spell pOSpell, bool pbFromObject, IUWSpellHost pIHost,
			int piDeferredManaCost = 0)
		{
			int liMinor = UWRunicMagic.GetEffectMinor(pOSpell.MinorClass);

			// Spells from items do not take effect here either (UW.EXE: the same abort in
			// CastSpells, without message for wands and traps).
			if (pIHost.HasPlayer && pIHost.IsMagicBlocked)
				return;

			if (pOSpell.MajorClass == HealMajorClass)
			{
				fHeal(liMinor, pIHost);

				return;
			}

			if (pOSpell.MajorClass == ProjectileMajorClass)
			{
				fBeginProjectileSpell(liMinor, pIHost, piDeferredManaCost);

				return;
			}

			if (pOSpell.MajorClass == AreaMajorClass)
			{
				UWAreaSpellRules.Cast(liMinor, UWRunicMagic.GetAreaTargetType(pOSpell.MinorClass), pIHost);

				return;
			}

			// Class 7 goes onto the creatures IN FRONT OF the player - from runes as from an item.
			// There is NO TARGET CURSOR in the original (per user, 2026-09-23, casting Ally
			// with runes: "no cursor came, it was cast on the slug in front of me, you could
			// see it by the visual effect"). Class7Spells_seg038_3307_E00 hands the spell for
			// the player to RunCodeOnTargetsAroundObject_seg038_C77 with a distance of 4 and a
			// radius of 2 - the square the area spells use, measured on the poison potion
			// (2026-09-09): tiles two to six ahead, exactly one creature, the most distant
			// first. See UWAreaSpellRules.CastTargetSpellInArea.
			//
			// Until 2026-09-23 a rune-cast class 7 spell waited for a clicked target, as the
			// reference has it (spellcasting, case 7) - one of its derived spots; the click
			// often hit something that was no creature, and nothing happened.
			if (pOSpell.MajorClass == TargetMajorClass)
			{
				if (pIHost.HasPlayer)
					UWAreaSpellRules.CastTargetSpellInArea(liMinor, pIHost);

				return;
			}

			// Class 8 puts something into the world - see UWSummonSpellRules.
			if (pOSpell.MajorClass == UWSummonSpellRules.MajorClass)
			{
				UWSummonSpellRules.Cast(liMinor, pIHost);

				return;
			}

			// Class 10 restores mana. Two spells, both without rune sequence - they exist only in
			// items, therefore the minor class here is the AMOUNT.
			if (pOSpell.MajorClass == ManaMajorClass)
			{
				if (pIHost.HasPlayer)
					pIHost.RestoreManaFromSpell(liMinor);

				return;
			}

			// Class 11 is the bag full of special cases - see UWMiscSpellRules.
			if (pOSpell.MajorClass == UWMiscSpellRules.MajorClass)
			{
				UWMiscSpellRules.Cast(liMinor, RollStability(UWRunicMagic.GetStabilityClass(pOSpell.MinorClass)), pIHost);

				pIHost.RefreshSpellIcons();

				return;
			}

			// Class 13 exists only on items - see UWItemSpellRules.
			if (pOSpell.MajorClass == UWItemSpellRules.MajorClass)
			{
				UWItemSpellRules.Cast(liMinor, pIHost);

				return;
			}

			if (pOSpell.MajorClass > LastLastingMajorClass || !pIHost.HasPlayer)
				return;

			// The upper bits of the minor class say how long the spell lasts - for In Lor
			// 0x80, so three rolls of a d24.
			//
			// No more than three at once. The mana is gone anyway then (tested per user
			// in the original).
			if (!pIHost.TryAddActiveSpell(pOSpell.MajorClass, liMinor,
				RollStability(UWRunicMagic.GetStabilityClass(pOSpell.MinorClass))))
			{
				pIHost.AddGeneralMessage(CastingNotSuccessfulMessage);

				return;
			}

			pIHost.RefreshSpellIcons();
		}

		/// <summary>
		/// A projectile spell does not fly off immediately: the original waits for the click that
		/// gives the direction, and meanwhile shows its own mouse pointer.
		///
		/// The damage is in the projectile table of OBJECTS.DAT and for a
		/// spell projectile is the whole damage - the original includes the Missile skill only
		/// for physical projectiles, recognisable by weapon type 0xC0.
		/// </summary>
		private static void fBeginProjectileSpell(int piMinorClass, IUWSpellHost pIHost, int piManaCost)
		{
			int liProjectile = UWRunicMagic.GetProjectileId(piMinorClass);

			if (liProjectile < 0 || pIHost.Data == null || pIHost.Data.ObjectProperties == null)
				return;

			pIHost.BeginProjectileSpell(liProjectile,
				pIHost.Data.ObjectProperties.GetRangedDamage(liProjectile),
				pIHost.Data.ObjectProperties.GetRangedSpeed(liProjectile), piManaCost);
		}

		/// <summary>Heals by as many rolls of a d8 as the minor class says. The
		/// calculation comes from the reference.</summary>
		private static void fHeal(int piMinorClass, IUWSpellHost pIHost)
		{
			if (!pIHost.HasPlayer)
				return;

			if (piMinorClass == FullHealMinorClass)
			{
				pIHost.HealFully();

				return;
			}

			int liAmount = 0;

			for (int liDie = 0; liDie < piMinorClass; liDie++)
				liAmount += UWRandom.Next(1, 9);

			pIHost.Heal(liAmount);
		}

		/// <summary>How long a spell lasts, by the class in the upper bits of the
		/// minor class. Values from the reference.</summary>
		public static int RollStability(int piStabilityClass)
		{
			switch (piStabilityClass)
			{
				case 0x80:
					return UWRandom.Next(1, 25) + UWRandom.Next(1, 25) + UWRandom.Next(1, 25);

				case 0x40:
					return UWRandom.Next(1, 9) + UWRandom.Next(1, 9);

				case 0x00:
					return UWRandom.Next(1, 4) + UWRandom.Next(1, 4);

				default:
					return 1;
			}
		}
	}
}
