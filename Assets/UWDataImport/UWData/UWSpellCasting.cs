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

			/// <summary>No magic here (the void, a no-magic tile) - the mana is paid all the same.</summary>
			Blocked,

			/// <summary>A critical failure: the mana is paid and the spell turns on the caster
			/// (IUWSpellHost.Backfire).</summary>
			Backfired,

			Cast,

			/// <summary>Too soon after the last cast (the pause between two casts).</summary>
			TooSoon
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

		/// <summary>The sound of a cast refused for being too soon (try_cast: effect 0x15).</summary>
		private const int TooSoonSound = 0x15;

		/// <summary>The skill check's bonus on the casting skill and its target per circle
		/// (player_cast: skill_check(Casting + 5, 2 * circle)).</summary>
		private const int CastingCheckBonus = 5;

		private const int CastingCheckPerCircle = 2;

		/// <summary>
		/// THE PAUSE BETWEEN TWO CASTS (try_cast and player_cast, UnderworldExhumed RUNES.C, read
		/// 2026-10-11; the Exhumed audit's row 4): every cast that got past the skill check sets
		/// spell_delay = (2 * circle - level) * 4 + 0x40 clock units (a byte, 256 a second), and a
		/// rune cast before the clock passed the last cast's time plus it is refused with sound 0x15
		/// - a quarter of a second, a little more for a circle above one's level, down to a thirtieth
		/// for a high level's first circle. Kept in real seconds from the host (IUWSpellHost.
		/// RealSeconds): our game clock advances in whole player ticks.
		/// </summary>
		private const int CastDelayBase = 0x40;

		private const int CastDelayPerStep = 4;

		private static double mdLastCastSeconds = double.NegativeInfinity;

		private static double mdCastDelaySeconds;

		/// <summary>
		/// Casts the selected runes.
		///
		/// The rules come from the disassembled reference, the mana costs are cross-checked against the original:
		/// In Lor is spell 0, so circle 1, and costs 3 mana there - exactly circle
		/// times three (per user, 2026-09-03).
		///
		/// ORDER AS UW.EXE'S player_cast (UnderworldExhumed RUNES.C, 2026-10-11): the pause since
		/// the last cast, lookup, circle, mana, skill check (a failure costs nothing), then the
		/// pause set and the mana paid (a missile's when it is released), and only then the
		/// spell - so a cast where no magic works costs the mana, and a backfire turns on the
		/// caster.
		/// </summary>
		public static OutcomeEnum CastRunes(int piFirstRune, int piSecondRune, int piThirdRune, IUWSpellHost pIHost)
		{
			if (pIHost == null || !pIHost.HasPlayer)
				return OutcomeEnum.NotASpell;

			double ldNow = pIHost.RealSeconds;

			if (ldNow < mdLastCastSeconds + mdCastDelaySeconds)
			{
				pIHost.PlaySoundAtPlayer(TooSoonSound);

				return OutcomeEnum.TooSoon;
			}

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

			// THE CHECK: Casting + 5 against twice the circle (player_cast; until 2026-10-11 ours had
			// Casting against three times the circle, the reference's - 6 to 13 points harder, the
			// Exhumed audit's row 1). A failure costs nothing at all (tested per user in the
			// original, 2026-09-03; the mana is paid only after the check).
			UWSkillCheck.ResultEnum leResult = UWSkillCheck.Check(pIHost.PlayerCastingSkill + CastingCheckBonus,
				lOSpell.Level * CastingCheckPerCircle);

			if (leResult == UWSkillCheck.ResultEnum.Failure)
			{
				pIHost.PlaySpellFailureSound();
				pIHost.AddGeneralMessage(IncantationFailedMessage);

				return OutcomeEnum.Failed;
			}

			bool lbBackfire = leResult == UWSkillCheck.ResultEnum.CriticalFailure;

			if (lbBackfire)
				pIHost.AddGeneralMessage(SpellBackfiresMessage);

			// The pause before the next cast, from now - a backfire sets it too.
			mdLastCastSeconds = ldNow;
			mdCastDelaySeconds = (byte)((((lOSpell.Level * 2) - pIHost.PlayerLevel) * CastDelayPerStep) + CastDelayBase)
				/ (double)UWPlayerTick.PitTicksPerSecond;

			// THE MANA IS PAID BEFORE THE SPELL (player_cast; the Exhumed audit's row 2) - so a
			// cast where no magic works costs it; until 2026-10-11 ours let it off. A PROJECTILE
			// PAYS WHEN IT IS RELEASED (release_missile; Class5ProjectileSpells_seg038_4EE keeps the
			// cost and subtracts it only once the missile found room): the cost travels with the
			// waiting spell, and a refused one is not paid.
			int liDeferredCost = !lbBackfire && lOSpell.MajorClass == ProjectileMajorClass ? lOSpell.ManaCost : 0;

			if (liDeferredCost == 0)
				pIHost.SpendMana(lOSpell.ManaCost);

			// NO MAGIC HERE (void or tile with no-magic bit): do_spell refuses, error 3, "Casting
			// was not successful.", with the failure sound - a backfire as well (do_spell tests it
			// before the class).
			if (pIHost.IsMagicBlocked)
			{
				pIHost.PlaySpellFailureSound();
				pIHost.AddGeneralMessage(CastingNotSuccessfulMessage);

				return OutcomeEnum.Blocked;
			}

			// THE BACKFIRE (class 9, strength circle / 2; the Exhumed audit's row 3): (circle / 2)d8
			// on the caster, never below three hit points, the view flashing - and the cast sound,
			// as do_spell took it. Until 2026-10-11 ours only paid the mana.
			if (lbBackfire)
			{
				pIHost.Backfire(lOSpell.Level / 2);
				pIHost.PlaySpellSound();

				return OutcomeEnum.Backfired;
			}

			// The spell, and the cast sound only once it took (player_cast plays effect 0x10 after
			// do_spell; a lasting spell with no free slot fails with the failure sound).
			if (!ApplyEffect(lOSpell, false, pIHost, liDeferredCost))
			{
				pIHost.PlaySpellFailureSound();

				return OutcomeEnum.Blocked;
			}

			pIHost.PlaySpellSound();

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

		/// <summary>The effect of a successful spell, by major class - see the class comment.
		/// False when it did not take: no magic here, or a lasting spell with no free slot.</summary>
		public static bool ApplyEffect(UWRunicMagic.Spell pOSpell, bool pbFromObject, IUWSpellHost pIHost,
			int piDeferredManaCost = 0)
		{
			int liMinor = UWRunicMagic.GetEffectMinor(pOSpell.MinorClass);

			// Spells from items do not take effect here either (UW.EXE: the same abort in
			// CastSpells, without message for wands and traps).
			if (pIHost.HasPlayer && pIHost.IsMagicBlocked)
				return false;

			if (pOSpell.MajorClass == HealMajorClass)
			{
				fHeal(liMinor, pIHost);

				return true;
			}

			if (pOSpell.MajorClass == ProjectileMajorClass)
			{
				fBeginProjectileSpell(liMinor, pIHost, piDeferredManaCost);

				return true;
			}

			if (pOSpell.MajorClass == AreaMajorClass)
			{
				UWAreaSpellRules.Cast(liMinor, UWRunicMagic.GetAreaTargetType(pOSpell.MinorClass), pIHost);

				return true;
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

				return true;
			}

			// Class 8 puts something into the world - see UWSummonSpellRules.
			if (pOSpell.MajorClass == UWSummonSpellRules.MajorClass)
			{
				UWSummonSpellRules.Cast(liMinor, pIHost);

				return true;
			}

			// Class 10 restores mana. Two spells, both without rune sequence - they exist only in
			// items, therefore the minor class here is the AMOUNT.
			if (pOSpell.MajorClass == ManaMajorClass)
			{
				if (pIHost.HasPlayer)
					pIHost.RestoreManaFromSpell(liMinor);

				return true;
			}

			// Class 11 is the bag full of special cases - see UWMiscSpellRules.
			if (pOSpell.MajorClass == UWMiscSpellRules.MajorClass)
			{
				UWMiscSpellRules.Cast(liMinor, RollStability(UWRunicMagic.GetStabilityClass(pOSpell.MinorClass)), pIHost);

				pIHost.RefreshSpellIcons();

				return true;
			}

			// Class 13 exists only on items - see UWItemSpellRules.
			if (pOSpell.MajorClass == UWItemSpellRules.MajorClass)
			{
				UWItemSpellRules.Cast(liMinor, pIHost);

				return true;
			}

			if (pOSpell.MajorClass > LastLastingMajorClass || !pIHost.HasPlayer)
				return true;

			// The upper bits of the minor class say how long the spell lasts - for In Lor
			// 0x80, so three rolls of a d24.
			//
			// No more than three at once. The mana is gone anyway then (tested per user
			// in the original).
			if (!pIHost.TryAddActiveSpell(pOSpell.MajorClass, liMinor,
				RollStability(UWRunicMagic.GetStabilityClass(pOSpell.MinorClass))))
			{
				pIHost.AddGeneralMessage(CastingNotSuccessfulMessage);

				return false;
			}

			pIHost.RefreshSpellIcons();

			return true;
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
