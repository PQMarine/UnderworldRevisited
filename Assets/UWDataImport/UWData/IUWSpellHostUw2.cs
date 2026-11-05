namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE SECOND GAME'S OWN SPELLS ASK OF THE CASTER BEYOND IUWSpellHost (UW2 plan phase 4, list item 3,
	/// stage 2): the record the player lives in, his vitals and the moves only the engine can make. The player's
	/// host on the second game (UWSpellHostUw2) has it; a creature's host has not, and the rules then do what
	/// the original does for a caster other than the player (mostly nothing).
	/// </summary>
	public interface IUWSpellHostUw2
	{
		/// <summary>The player's record (PLAYER.DAT's), where the second game keeps its state.</summary>
		UWPlayerFile Record { get; }

		/// <summary>The player's vitals.</summary>
		UWPlayerVitals Vitals { get; }

		/// <summary>can_place for the player's body at the tile's centre and his height (Portal, SPELLS2.C:719).</summary>
		bool PlayerFitsAt(UWTilePos pOTile);

		/// <summary>do_teleport and player_setup: the player onto the tile of the level (1-based; 0 the one he
		/// is on), the first free tile near it as a teleport trap finds one.</summary>
		void MovePlayer(int piLevel, UWTilePos pOTile);

		/// <summary>chg_plyp: the view's pitch by piSteps of the original's 0x400 (positive up), under the
		/// original's check (mvcheck, UTIL.C:19).</summary>
		void SwingView(int piSteps);
	}
}
