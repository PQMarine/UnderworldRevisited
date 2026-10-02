namespace UWDataImport.UWData
{
	/// <summary>
	/// A spell currently in effect on the player. No more than three at once - that is how many
	/// icon slots the original has next to the compass.
	///
	/// Stability is the remaining duration. It drops by one on every player tick, twenty-one
	/// seconds (UWPlayerTick), as the reference does; at one the spell is over. You can end it
	/// yourself at any time.
	///
	/// Engine-free since 2026-09-17 (P2 of the engine separation); was UWCharacter.ActiveSpellEffect.
	/// </summary>
	public struct UWActiveSpellEffect
	{
		public int MajorClass;

		public int MinorClass;

		public int Stability;
	}
}
