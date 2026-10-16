namespace UWDataImport.UWData
{
	/// <summary>
	/// The spells only items carry: major class 13 and up, which the spell form of an
	/// enchantment reaches when (link &amp; 0x1FF) >> 6 is not zero (see
	/// UWObjectMechanics.TryGetItemClassSpell). No rune combination gives them.
	///
	/// READ 2026-09-25 in the original's spell dispatcher (its jump table over the major
	/// class ends at 0xE; the case of class 13 is MajorSpellClassD_seg038_258):
	///
	///   13/3   the bullfrog puzzle's action 4, the reset (the same routine the a_do traps call,
	///          with mode 4) - "Reset Activated." on level 4, "There is a pained whining
	///          sound." anywhere else. Carried by the wand of the Frog on level 4 at 46/47.
	///   13/5   "Your vision distorts and you feel light headed." and the hallucination bits of
	///          PLAYER.DAT 0x61 set to 3. Carried by five red potions, a green potion and a
	///          scroll (level 3 49/52, level 5 58/8, level 6 5/15 and the scroll at 12/43,
	///          level 8 48/44 (green) and 11/57).
	///   13/x   any other minor class: nothing.
	///   14/n   plays cutscene n - NOT BUILT: in the nine levels only wall writings (class 5)
	///          carry it, and the enchantment lookup rejects a class 5 carrier, so no item
	///          reaches it.
	///   12, 15 and up: nothing.
	///
	/// "Nothing" still counts as cast: the wand loses its charge, the potion is gone.
	///
	/// Before the dispatch the original refuses every spell of a caster standing on a
	/// no-magic tile or on level 9 - UWSpellCasting.ApplyEffect does that for all classes.
	/// </summary>
	public static class UWItemSpellRules
	{
		public const int MajorClass = 13;

		/// <summary>The minor class that resets the bullfrog puzzle.</summary>
		public const int BullfrogResetMinorClass = 3;

		/// <summary>The bullfrog routine's mode for the reset (UWTrapRules.FireBullfrog).</summary>
		private const int BullfrogResetMode = 4;

		public const int HallucinationMinorClass = 5;

		/// <summary>"Your vision distorts and you feel light headed." (block 1, our numbering).
		/// </summary>
		private const int HallucinationMessage = 229;

		/// <summary>The wands (0x98-0x9B): the player touching one in the world casts nothing.</summary>
		private const int FirstWandId = 0x98;

		private const int LastWandId = 0x9B;

		/// <summary>decode_obj_spell and useNSpellCharges: a spell object of quality 0 is missed,
		/// or taken off when spent, when a roll of 0 to 9 falls below 4.</summary>
		private const int EmptyRollRange = 10;

		private const int EmptyFailBelow = 4;

		/// <summary>
		/// A THING IN THE WORLD THAT CARRIES A CHARGED SPELL, used (checkSpell with how 0, the last
		/// step of UseObj; UnderworldExhumed obj/USEITEMS.C, read 2026-10-15): the spell is cast
		/// with the thing itself as the caster - no pause, no target cursor - and one charge is
		/// spent. Only the charged form (ID_FLAG11) counts; a wand the player touches casts
		/// nothing; a spell object of quality 0 is missed four times in ten (decode_obj_spell
		/// without always_decode), and when it is spent at 0 it is taken off four times in ten
		/// (useNSpellCharges). The shipped cases (B14): the sceptre 0xAA on level 5 at 59/54 (Ally)
		/// and the ruby 0xA2 on level 6 at 14/3 (Restore Mana), both with five charges - both
		/// spells do nothing with a thing as the caster (UWTrapRules.SpellTrapCastsFor), so the
		/// player sees a charge go and nothing else. Until 2026-10-15 ours had no such step
		/// (reference audit round 2, A75).
		///
		/// Returns true when a spell was cast; piMajorClass and piMinorClass then say which
		/// (a rune spell given by number is turned into its classes, as cast() does through
		/// spells[]; a number beyond the list casts nothing but still spends the charge). The
		/// caller sends the spell off as a spell trap's when the class acts for a thing as caster.
		/// pOMaster is the list the thing's chain lives in (the level's).
		/// </summary>
		public static bool TryCastFromWorldObject(UWObject pOThing, System.Collections.Generic.List<UWObject> pOMaster,
			bool pbUsedByPlayer, out int piMajorClass, out int piMinorClass)
		{
			piMajorClass = -1;
			piMinorClass = 0;

			if (pOThing == null || UWObjectIds.Major(pOThing.ID) == UWObjectMechanics.TrapMajorClass)
				return false;

			if (pbUsedByPlayer && pOThing.ID >= FirstWandId && pOThing.ID <= LastWandId)
				return false;

			UWObject lOSpell = UWObjectMechanics.FindSpellObject(pOThing, pOMaster);

			if (lOSpell == null || (lOSpell.Flags & UWObjectMechanics.SpellFormatFlag) == 0)
				return false;

			bool lbChained = lOSpell != pOThing;

			if (lbChained && lOSpell.Quality == 0 && UWRandom.Next(EmptyRollRange) < EmptyFailBelow)
				return false;

			int liRaw = lOSpell.Quantity & 0x1FF;
			int liUpper = liRaw >> 6;

			if (liUpper != 0)
			{
				piMajorClass = liUpper + UWObjectMechanics.FirstItemSpellMajorClass - 1;
				piMinorClass = liRaw & 0x3F;
			}
			else if ((liRaw & 0x3F) < UWRunicMagic.AllSpells.Count)
			{
				UWRunicMagic.Spell lORune = UWRunicMagic.AllSpells[liRaw & 0x3F];

				piMajorClass = lORune.MajorClass;
				piMinorClass = lORune.MinorClass;
			}

			// useNSpellCharges: only a chained spell object, never the thing itself.
			if (lbChained && !pOThing.HasQuantity)
			{
				if (lOSpell.Quality > 0)
					lOSpell.Quality = (ushort)(lOSpell.Quality - 1);
				else if (UWRandom.Next(EmptyRollRange) < EmptyFailBelow)
				{
					// The chain is resolved only now, to take the spell object out of it (saving
					// writes Contents back as the chain).
					if (pOMaster != null)
						pOThing.EnsureContentsLoaded(pOMaster);

					pOThing.Contents?.Remove(lOSpell);
				}
			}

			return true;
		}

		public static void Cast(int piMinorClass, IUWSpellHost pIHost)
		{
			if (pIHost == null)
				return;

			if (piMinorClass == BullfrogResetMinorClass)
			{
				pIHost.FireBullfrog(BullfrogResetMode);

				return;
			}

			if (piMinorClass == HallucinationMinorClass && pIHost.HasPlayer)
			{
				pIHost.AddGeneralMessage(HallucinationMessage);
				pIHost.StartHallucination();
			}
		}
	}
}
