namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STEPS OF MAGIC WHERE THE GAMES PART (Docs/UW2-PLAN.md section 4.3, phase 0 step 2,
	/// 2026-10-28): the rule keeps its one flow (UWSpellCasting.CastRunes, player_cast) and asks the
	/// game's variant at these steps, from Underworld Exhumed's list of differences (docs/
	/// UW1-UW2-DIFFERENCES.md, "Casting from runes"). The numbers that differ - the check's bonus and
	/// factor, the pause's base - are rule set entries (UWRulesMagic), not variant methods.
	/// </summary>
	public interface IUWMagicVariant
	{
		/// <summary>The game's spell table (spells[]): the first game's 53, the second's 69.</summary>
		System.Collections.Generic.IReadOnlyList<UWRunicMagic.Spell> Spells { get; }

		/// <summary>The spells of a circle (UW1: 6, RUNES.C:242; UW2: 8, RUNES.C:227).</summary>
		int SpellsPerCircle { get; }

		/// <summary>fail_spell's first string in our block 1: not experienced, no mana, the incantation failed,
		/// not successful, and the backfire's after them.</summary>
		int FailStringBase { get; }

		/// <summary>"You are healed." in our block 1, -1 for none (UW1).</summary>
		int HealedString { get; }

		/// <summary>Whether the spell's class runs in this game yet (UW2 plan phase 4, list item 3).</summary>
		bool IsBuilt(UWRunicMagic.Spell pOSpell);

		/// <summary>The sound of a cast that took (UW1: 0x10; UW2: by the class, RUNES.C:255).</summary>
		void PlayCastSound(IUWSpellHost pIHost, UWRunicMagic.Spell pOSpell);

		/// <summary>Whether the player may begin a rune cast at all (UW2: not while paralysed, RUNES.C:174).</summary>
		bool MayCast(IUWSpellHost pIHost);

		/// <summary>Whether a spell of this circle may be cast where the player is (UW2: above circle
		/// 3 not in Britannia, RUNES.C:232); the place where no magic works at all is the world's
		/// (IUWSpellHost.IsMagicBlocked).</summary>
		bool MayCastHere(IUWSpellHost pIHost, int piCircle);

		/// <summary>Whether a spell's mana is paid when it is released rather than at the cast (UW1:
		/// a projectile, release_missile; UW2: a projectile or a spell aimed at one target, RUNES.C:249).</summary>
		bool PaysOnRelease(UWRunicMagic.Spell pOSpell);

		/// <summary>A cast tried before the pause since the last one is over (UW1: a sound; UW2: a
		/// sound and string 0xB, RUNES.C:183).</summary>
		void TooSoon(IUWSpellHost pIHost);
	}
}
