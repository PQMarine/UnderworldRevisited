using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The things the eating routine knows by their id (the original's eating routine, see
	/// UWItemUse.fEatLikeOriginal for where it is read): each with its own line of string block 1, the nutrition it carries
	/// instead of the OBJECTS.DAT value where it has one, and the extra it brings. Since
	/// 2026-10-14 (cleanup step 2.10) instead of the id switch in fEatLikeOriginal, with the wine
	/// bottle's early answer as a row too. Plain food (meat, fish, bread ...) has no row: its
	/// nutrition is the table value and its line the taste roll.
	///
	/// The rows are constant data; nothing here changes while the game runs.
	/// </summary>
	public static class UWFoodTable
	{
		/// <summary>What a row brings besides its line and nutrition.</summary>
		public enum Effect
		{
			None,

			/// <summary>The wine bottle cannot be opened: its line, and nothing is eaten.</summary>
			SealedBottle,

			/// <summary>Mana and hallucination before the line (UWItemUse.fMushroomEffects).</summary>
			Mushroom,

			/// <summary>Poisons after it is eaten.</summary>
			Toadstool
		}

		public readonly struct Row
		{
			public readonly int Id;

			/// <summary>String block 1, our numbering.</summary>
			public readonly int Message;

			/// <summary>The row's nutrition replaces the OBJECTS.DAT value (the original's
			/// constants for the plants, the rotworm and the stew).</summary>
			public readonly bool HasOwnNutrition;

			public readonly int Nutrition;

			public readonly Effect Effect;

			public Row(int piId, int piMessage, Effect peEffect = Effect.None)
			{
				Id = piId;
				Message = piMessage;
				HasOwnNutrition = false;
				Nutrition = 0;
				Effect = peEffect;
			}

			public Row(int piId, int piMessage, int piNutrition)
			{
				Id = piId;
				Message = piMessage;
				HasOwnNutrition = true;
				Nutrition = piNutrition;
				Effect = Effect.None;
			}
		}

		public const int MushroomObjectId = 0xB8;
		public const int ToadstoolObjectId = 0xB9;
		public const int AleObjectId = 0xBA;
		public const int WaterObjectId = 0xBD;
		public const int PortObjectId = 0xBE;
		public const int WineObjectId = 0xBF;
		public const int ThornyPlantObjectId = 0xCE;
		public const int PlainPlantObjectId = 0xCF;
		public const int DeadRotwormObjectId = 0xD9;
		public const int WormyStewObjectId = UWObjectIds.RotwormStew;

		// Block 1, our numbering.

		/// <summary>The wine bottle that cannot be opened.</summary>
		public const int WineBottleMessage = 128;

		/// <summary>"The mushroom causes your head to spin, and your vision to blur."</summary>
		public const int MushroomMessage = 233;

		/// <summary>"You manage to finish eating the leeches.  Barely." - what the leeches give
		/// when they are EATEN instead of applied, see UWItemUse.TryEat.</summary>
		public const int EatenLeechesMessage = 230;

		public const int EatenCandleMessage = 231;
		public const int ToadstoolMessage = 232;
		public const int PlainPlantMessage = 234;
		public const int RotwormMessage = 235;
		public const int WormyStewMessage = 236;
		public const int ThornyPlantMessage = 237;
		public const int WaterMessage = UWStringIds.WaterRefreshesMessage;
		public const int PortMessage = 239;
		public const int AleMessage = 240;

		// The nutrition the original gives the four things outside the comestibles.
		public const int ThornyPlantNutrition = 4;
		public const int PlainPlantNutrition = 0x17;
		public const int RotwormNutrition = 4;
		public const int WormyStewNutrition = 0x40;

		private static readonly Dictionary<int, Row> mORows = fBuildRows(
			new Row(WineObjectId, WineBottleMessage, Effect.SealedBottle),
			new Row(MushroomObjectId, MushroomMessage, Effect.Mushroom),
			new Row(ToadstoolObjectId, ToadstoolMessage, Effect.Toadstool),
			new Row(UWObjectIds.Candle, EatenCandleMessage),
			new Row(UWItemUse.LeechObjectId, EatenLeechesMessage),
			new Row(ThornyPlantObjectId, ThornyPlantMessage, ThornyPlantNutrition),
			new Row(PlainPlantObjectId, PlainPlantMessage, PlainPlantNutrition),
			new Row(DeadRotwormObjectId, RotwormMessage, RotwormNutrition),
			new Row(WormyStewObjectId, WormyStewMessage, WormyStewNutrition),
			new Row(WaterObjectId, WaterMessage),
			new Row(PortObjectId, PortMessage),
			new Row(AleObjectId, AleMessage));

		private static Dictionary<int, Row> fBuildRows(params Row[] pORows)
		{
			Dictionary<int, Row> lORows = new Dictionary<int, Row>();

			foreach (Row lORow in pORows)
				lORows.Add(lORow.Id, lORow);

			return lORows;
		}

		public static bool TryGet(int piObjectId, out Row pORow)
		{
			return mORows.TryGetValue(piObjectId, out pORow);
		}

		/// <summary>Every row, for the dispatch-coverage check.</summary>
		public static IEnumerable<Row> Rows
		{
			get { return mORows.Values; }
		}

		/// <summary>The things outside the comestibles that are eaten when USED from the pack
		/// (see UWItemUse.IsEatenOnUse for the routines): exactly the rows with their own
		/// nutrition.</summary>
		public static bool IsEatenOnUse(int piObjectId)
		{
			Row lORow;

			return mORows.TryGetValue(piObjectId, out lORow) && lORow.HasOwnNutrition;
		}
	}
}
