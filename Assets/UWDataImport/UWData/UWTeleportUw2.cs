namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S TELEPORT TRAP (Docs/UW2-PLAN.md phase 2 step 5, 2026-10-29), as Underworld
	/// Exhumed's matched sources have it: UseTrap's TRAP_TELEPORT (uw2 event/TRIGGER.C:309-322) takes the
	/// target tile from quality and owner and the level from the height (1 to 80; 0 or the level the
	/// player is on keeps him there), and from the fine x a setup word: bit 0 arrives near the ceiling
	/// and falling, bit 1 sets a heading (the trap's eighth + 8, shifted by 2), bit 2 stops the automap
	/// (player->automap 0, map_scrap the level - not built: the second game's map screen comes with its
	/// interface). do_teleport (event/WORLDEV.C:180) records the target; new_player_pos (game/UWEDIT.C:466)
	/// changes the level, finds a free tile near the target strictly then loosely (find_good_x_and_y) and
	/// sets the player up there, turned to the heading when the word carries one.
	/// </summary>
	public static class UWTeleportUw2
	{
		/// <summary>The teleport trap's item, the same in both games (0x180 + TRAP_TELEPORT).</summary>
		public const int TrapId = 0x181;

		/// <summary>The fine x's bits.</summary>
		public const int FallsBit = 1;

		public const int HeadingBit = 2;

		public const int AutomapOffBit = 4;

		/// <summary>The setup word's flag for a heading (bit 5, from the eighth + 8 shifted by 2).</summary>
		public const int HeadingWordFlag = 0x20;

		public struct Target
		{
			/// <summary>The 1-based level; 0 for the level the player is on.</summary>
			public int Level;

			public int TileX;

			public int TileY;

			/// <summary>Arrives near the ceiling, falling (player_setup with how 0).</summary>
			public bool Falls;

			/// <summary>Whether the arrival sets the heading, and the 16-bit heading it sets.</summary>
			public bool HasHeading;

			public int Heading;

			/// <summary>Whether the trap stops the automap (not acted on yet).</summary>
			public bool AutomapOff;
		}

		/// <summary>The target of a teleport trap record.</summary>
		public static Target Decode(UWObject pOTrap)
		{
			int liFine = pOTrap.XPos;
			int liWord = liFine & FallsBit;

			if ((liFine & HeadingBit) != 0)
				liWord |= (pOTrap.Heading + 8) << 2;

			return new Target
			{
				Level = pOTrap.ZPos,
				TileX = pOTrap.Quality,
				TileY = pOTrap.Owner,
				Falls = (liWord & FallsBit) != 0,
				HasHeading = (liWord & HeadingWordFlag) != 0,
				Heading = ((liWord & ~0x23) << 11) & 0xFFFF,
				AutomapOff = (liFine & AutomapOffBit) != 0
			};
		}

		/// <summary>The level the target takes the player to: its own, or piCurrentLevel for 0.</summary>
		public static int LevelOf(Target pOTarget, int piCurrentLevel)
		{
			return pOTarget.Level == 0 ? piCurrentLevel : pOTarget.Level;
		}
	}
}
