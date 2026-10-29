namespace UWDataImport.UWData
{
	/// <summary>
	/// THE GAME (Docs/UW2-PLAN.md section 4.1, phase 0 step 1, 2026-10-28): which of the two
	/// Underworld games a game data, a session or a profile belongs to. Everything that differs
	/// between them hangs off the game's profile (IUWGameProfile); no rule asks the id itself.
	/// </summary>
	public enum UWGame
	{
		/// <summary>Ultima Underworld: The Stygian Abyss (1992).</summary>
		Uw1 = 1,

		/// <summary>Ultima Underworld II: Labyrinth of Worlds (1993) - its profile since phase 0 step 4,
		/// the install facts only until phase 4 brings its rules.</summary>
		Uw2 = 2
	}
}
