namespace UWDataImport.UWData
{
	/// <summary>
	/// What the second game's schedules (UWScheduleRulesUw2, G6 part 1) ask of the game: the player's record (the X
	/// clocks, quests and numbered variables), the level they run on, the world (creatures moved, killed, removed), the
	/// trap host the scheduled triggers fire through, the creature table and the player's eye.
	/// </summary>
	public interface IUWScheduleHost
	{
		UWPlayerFile Record { get; }

		/// <summary>PlayerLevel, 1-based: the clock of each block the schedules keep for it, and the rows' level test.</summary>
		int LevelNumber { get; }

		IUWWorld World { get; }

		/// <summary>The scheduled triggers' firing (UseTrigger with mode 0xC, nobody as the setter) and the hacks they
		/// reach.</summary>
		IUWTrapHost Traps { get; }

		UWObjectClassProperties Properties { get; }

		/// <summary>talking_to: the partner of the conversation whose end runs the schedules, else null (ev_kill spares it).</summary>
		UWObject TalkingTo { get; }

		/// <summary>The player's square, fine spot and facing for player_looking (SCDEVENT.C:120).</summary>
		int PlayerX8 { get; }

		int PlayerY8 { get; }

		/// <summary>PlayerFacing's eighth ((PlayerFacing &gt;&gt; 13) &amp; 7).</summary>
		int PlayerFacingEighth { get; }
	}
}
