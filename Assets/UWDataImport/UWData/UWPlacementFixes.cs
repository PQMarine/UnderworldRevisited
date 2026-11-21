namespace UWDataImport.UWData
{
	/// <summary>
	/// THINGS THE ORIGINAL'S LEVEL DATA PUTS IN THE WRONG PLACE, drawn where they were meant to be (a deviation, under the
	/// Game option "Fix bugs of the original"; UWRulesDeviations.MisplacedObjectsMoved). Only where a thing is DRAWN:
	/// the level data, and so a save, keep the original's values.
	///
	/// The second game's level 12, 28/33 (per user 2026-11-20 with pictures of the original): the writing 0x166 over
	/// the door hangs at fine 7/4, facing 2 - three eighths in front of the lintel, facing the lintel, so from the
	/// corridor (30/33) one sees it only looking right up, and it reads as a plaque floating under the ceiling. Meant was
	/// the lintel's outside, over the door, which is its WEST side (per user the same day: the corridor side is the
	/// inside; the player of SAVE3 stood in the doorway at fine 3.9/4.1 to show where): fine 3/4, still facing 2, the
	/// front west (facing 2 turns a wall thing's front west, as the first game's plaque on level 6, 8/51, shows against
	/// its wall). The data's writing is the lintel's sign moved four eighths east. A data position comes in whole eighths
	/// only; the drawing may go a part of one further towards the wall behind (Nudge; per user the same day: "a little
	/// nearer the lintel") - half an eighth there, by eye.
	/// </summary>
	public static class UWPlacementFixes
	{
		/// <summary>The deviation's default (UWRulesDeviations.MisplacedObjectsMoved).</summary>
		public const bool MisplacedObjectsMovedApplied = true;

		private struct Fix
		{
			public UWGame Game;

			/// <summary>1-based, as UWLevel.LevelNumber.</summary>
			public int Level;

			public int TileX;

			public int TileY;

			public int Id;

			public int XPos;

			public int YPos;

			public int Heading;

			public int NewXPos;

			public int NewYPos;

			public int NewHeading;

			/// <summary>How far the drawing goes on towards the wall behind it, in eighths of a tile.</summary>
			public float Nudge;
		}

		private static readonly Fix[] maFixes =
		{
			new Fix { Game = UWGame.Uw2, Level = 12, TileX = 28, TileY = 33, Id = 0x166, XPos = 7, YPos = 4, Heading = 2, NewXPos = 3, NewYPos = 4, NewHeading = 2, Nudge = 0.5f },
		};

		/// <summary>How many places are fixed.</summary>
		public static int Count => maFixes.Length;

		/// <summary>Where a thing is drawn instead, when it is one of the misplaced ones - matched by game, level, tile, id
		/// and the original's fine position and facing, so a thing moved in the game keeps its own place.</summary>
		public static bool TryGet(UWGame peGame, int piLevel, int piTileX, int piTileY, UWObject pOObject,
			out int piXPos, out int piYPos, out int piHeading)
		{
			return TryGet(peGame, piLevel, piTileX, piTileY, pOObject, out piXPos, out piYPos, out piHeading, out _);
		}

		/// <param name="pfNudge">How far the drawing goes on towards the wall behind it, in eighths (Fix.Nudge).</param>
		public static bool TryGet(UWGame peGame, int piLevel, int piTileX, int piTileY, UWObject pOObject,
			out int piXPos, out int piYPos, out int piHeading, out float pfNudge)
		{
			piXPos = 0;
			piYPos = 0;
			piHeading = 0;
			pfNudge = 0f;

			if (pOObject == null)
				return false;

			foreach (Fix lOFix in maFixes)
			{
				if (lOFix.Game != peGame || lOFix.Level != piLevel || lOFix.TileX != piTileX || lOFix.TileY != piTileY
					|| lOFix.Id != pOObject.ID || lOFix.XPos != pOObject.XPos || lOFix.YPos != pOObject.YPos
					|| lOFix.Heading != pOObject.Heading)
					continue;

				piXPos = lOFix.NewXPos;
				piYPos = lOFix.NewYPos;
				piHeading = lOFix.NewHeading;
				pfNudge = lOFix.Nudge;

				return true;
			}

			return false;
		}
	}
}
