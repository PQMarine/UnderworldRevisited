namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STEPS OF THE TRAPS' RULES WHERE THE GAMES PART (Docs/UW2-PLAN.md section 4.3, phase 0
	/// step 2, 2026-10-28), from Underworld Exhumed's list of differences (docs/UW1-UW2-DIFFERENCES.md,
	/// "Traps and triggers"). The bounds of change terrain (height 13 or 14, floors below 11 or 15,
	/// walls below 48 or 63) are numbers for the game's rule set; the trigger classes, the set- and
	/// check-variable traps and the traps only the second game has (special effect, jump, skill
	/// check, proximity, change-from, oscillator, pit, bridge, experience) come with its variant
	/// in phase 4, as do the hack traps and the plot deaths, which share nothing between the games.
	/// </summary>
	public interface IUWTrapVariant
	{
		/// <summary>Whether a cast of the fishing pole gets a bite (UW1: one time in five,
		/// WORLDEV.C:331; UW2: when a roll of 0 to 4 is at most (Track + 7) / 8, WORLDEV.C:538).</summary>
		bool Bites(int piTrackSkill);

		/// <summary>Whether the door trap puts its template's lock on (UW1: always, when the door is
		/// at rest; UW2: only when the trap's owner field is not zero, TRIGGER.C:633).</summary>
		bool SwapsLock(int piTrapOwner);
	}
}
