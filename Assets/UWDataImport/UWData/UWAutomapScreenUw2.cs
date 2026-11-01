using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S MAP SCREEN, WITHOUT THE ENGINE (UW2 plan phase 3 step 6): what a click on it does
	/// and which level it shows, as Underworld Exhumed's uw2 ui/AUTOMAP.C has them. The level is the
	/// original's number, 1 to 80: eight levels a world, the world (level - 1) / 8, Britannia the first.
	/// Coordinates are the screen's, x from the left and y from the BOTTOM, as ManageDungeonMap's mx
	/// and my (it takes 0x13 and 7 off the pointer's place for the pen's point, which ours has not).
	/// </summary>
	public static class UWAutomapScreenUw2
	{
		public const int LevelsAWorld = 8;

		public const int WorldCount = 9;

		/// <summary>NUM_LEVELS: a level at or past it has no map.</summary>
		public const int LevelCount = 80;

		/// <summary>The first of the map scraps' slots, never loaded (ChangeAutoMapLevel).</summary>
		public const int FirstScrapLevel = 0x47;

		public enum ClickEnum
		{
			None,
			Close,
			Erase,
			Note,
			PreviousLevel,
			NextLevel,
			Gem
		}

		/// <summary>What a click at (x, y) means; piGem the world of a click on the gem, by the
		/// swap of the two facets that lie the other way round (6 and 7).</summary>
		public static ClickEnum ClickAt(int piX, int piY, out int piGem)
		{
			piGem = -1;

			if (piX > UWClassicLayoutUw2.Map.RightColumnLeft && piX < UWClassicLayoutUw2.Map.Right
				&& piY > UWClassicLayoutUw2.Map.CloseBottom && piY < UWClassicLayoutUw2.Map.CloseTop)
				return ClickEnum.Close;

			if (piX > UWClassicLayoutUw2.Map.RightColumnLeft && piX < UWClassicLayoutUw2.Map.Right
				&& piY > UWClassicLayoutUw2.Map.CloseTop && piY < UWClassicLayoutUw2.Map.EraserTop)
				return ClickEnum.Erase;

			if (piX > UWClassicLayoutUw2.Map.CornerLeft && piX < UWClassicLayoutUw2.Map.Right
				&& piY > UWClassicLayoutUw2.Map.PreviousBottom && piY < UWClassicLayoutUw2.Map.PreviousTop)
				return ClickEnum.PreviousLevel;

			if (piX > UWClassicLayoutUw2.Map.CornerLeft && piX < UWClassicLayoutUw2.Map.Right
				&& piY > 0 && piY < UWClassicLayoutUw2.Map.CloseBottom)
				return ClickEnum.NextLevel;

			if (piX > UWClassicLayoutUw2.Map.RightColumnLeft && piX < UWClassicLayoutUw2.Map.GemRight
				&& piY > UWClassicLayoutUw2.Map.GemBottom && piY < UWClassicLayoutUw2.Map.GemTop)
			{
				int liGem = GemRegion(piX, piY);

				piGem = (liGem & 6) == 6 ? 0xD - liGem : liGem;

				return ClickEnum.Gem;
			}

			return piX < 0 || piY > UWClassicLayoutUw2.Map.TopRow ? ClickEnum.None : ClickEnum.Note;
		}

		/// <summary>true_gem_region: 0 the gem's middle, 1 to 8 round its edge.</summary>
		public static int GemRegion(int piX, int piY)
		{
			int liX = piX - UWClassicLayoutUw2.Map.GemCentreX + 3;
			int liY = piY - UWClassicLayoutUw2.Map.GemCentreY + 1;
			int liRegion;

			if (Math.Abs(liY) > Math.Abs(liX) * 2)
				liRegion = liY > 0 ? 1 : 5;
			else if (Math.Abs(liX) > Math.Abs(liY) + 9)
				liRegion = liX > 0 ? 3 : 7;
			else
				liRegion = (((4 - (fOctant(liX, liY) >> 1)) & 3) * 2) + 2;

			if ((liRegion & 1) != 0)
			{
				if (Math.Abs(liX) <= 0xF && Math.Abs(liY) <= 0xF)
					liRegion = 0;
			}
			else if (Math.Abs(liX) + Math.Abs(liY) <= 0x15)
			{
				liRegion = 0;
			}

			return liRegion;
		}

		/// <summary>octant (sys/MISCUTIL.C): 0 to 7, split at the axes and the diagonals.</summary>
		private static int fOctant(int piX, int piY)
		{
			if (piY > 0)
			{
				if (Math.Abs(piX) < piY)
					return (piX < 0 ? 1 : 0) + 1;

				return (piX < 0 ? 1 : 0) * 3;
			}

			if (Math.Abs(piX) < -piY)
				return (piX > 0 ? 1 : 0) + 5;

			return ((piX > 0 ? 1 : 0) * 3) + 4;
		}

		/// <summary>The level a click on the upper corner leads to, or the level itself where the
		/// world ends: the arrows stay in the world.</summary>
		public static int Previous(int piLevel)
		{
			return (piLevel & 7) != 1 ? piLevel - 1 : piLevel;
		}

		public static int Next(int piLevel)
		{
			return (piLevel & 7) != 0 ? piLevel + 1 : piLevel;
		}

		/// <summary>The same depth in another world.</summary>
		public static int InWorld(int piLevel, int piWorld)
		{
			return ((piLevel - 1) & 7) + (piWorld << 3) + 1;
		}

		public static bool HasMap(int piLevel)
		{
			return piLevel >= 1 && piLevel < LevelCount && piLevel != FirstScrapLevel;
		}

		/// <summary>show_gem_parts: the facet of a world: the gem's middle for Britannia, else the
		/// world's number less one.</summary>
		public static int FacetOf(int piLevel)
		{
			int liWorld = (piLevel - 1) / LevelsAWorld;

			return liWorld == 0 ? UWClassicLayoutUw2.Map.MiddleFacet : liWorld - 1;
		}

		/// <summary>The number shown beside "Level", 1 to 8 within the world.</summary>
		public static int NumberInWorld(int piLevel)
		{
			return ((piLevel - 1) & 7) + 1;
		}
	}
}
