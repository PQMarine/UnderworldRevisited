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
