using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>What the layout editor's "Preset" starts from (the numbers are its segments' order).</summary>
	public enum UWLayoutStart
	{
		/// <summary>The own layout as saved.</summary>
		Custom = 0,

		/// <summary>The modern interface at its default places, no original pieces.</summary>
		Modern = 1,

		/// <summary>The original's pieces freed as elements (UWModernClassicPlus).</summary>
		ClassicModular = 2
	}

	/// <summary>
	/// THE LAYOUT EDITOR'S STARTING POINTS AS TABLES (cleanup step 8.4, 2026-10-24; the cleanup plan
	/// 8.7): for each preset the layout settings it copies into the own layout, entry and value. Until
	/// then UWModernLayoutEditor.fChooseStart set some twenty fields by hand, one line each.
	///
	/// "Preset" puts the own layout back as it was saved first, then writes these; Custom writes
	/// nothing. Modern clears the own places and every original piece and switch; Classic Modular
	/// brings its places (the layout the user laid out and took over as the preset, 2026-10-11),
	/// its pieces with their outlines, five scroll lines and no generated backs. The UI size and the
	/// font stay as they are. The one value no table can hold - the stone shelf as wide as the screen
	/// leaves beside the stats panel - the editor works out after the layout is reloaded.
	///
	/// Tools/UWSelfCheck pins every value against what fChooseStart set (LayoutPresets).
	/// </summary>
	public static class UWLayoutPresets
	{
		/// <summary>Classic Modular's places, as the own layout keeps them (UWUserSettings.ModernLayout,
		/// UWHudLayout's text; until step 8.4 UWModernClassicPlus.Layout, which reads it from here).</summary>
		public const string ClassicModularLayout =
			"2:0.5:1:0:-4:100;3:0.5:0:-57.3:40:100;4:0:0:0:6:80;10:0.5:0:-0.12:5.9:100;11:0.5:0:45.42:5.73:100;12:0:0:0:0:100;"
			+ "14:1:0:-100.3:5.7:100;15:1:0:-75.4:6:100;16:1:0:-127.6:5.7:100;17:0:0.5:-3.3:-35:100;18:1:0:0:0:100;19:1:0.5:-0.13:36:100;";

		/// <summary>
		/// THE SECOND GAME'S CLASSIC MODULAR PLACES (UW2 plan stage U6 step 3, 2026-11-24): the layout the user laid out
		/// in the second game's own layout from the editor's Classic Modular start and took over as the preset the same day,
		/// as the first game's on 2026-10-11 (until then a draft equal to the first game's places).
		/// </summary>
		public const string ClassicModularLayoutUw2 =
			"2:0.5:1:0:-4:100;3:0.5:0:-66.8:1.5:100;4:0:0:0:0:100;10:0.5:0:0.11:-5.79:100;11:0.5:0:45.42:5.73:100;12:0:0:0:0:100;"
			+ "14:1:0:-93.75:0.25:100;15:1:0:-67.41:-1.19:100;16:0.5:0:66.54:0.5:100;17:0:0.5:0:-31.99:100;18:1:0:0:0:100;19:1:0.5:6.77:39.83:100;";

		/// <summary>Classic Modular's places for a game.</summary>
		public static string ClassicModularLayoutOf(UWGame peGame)
		{
			return peGame == UWGame.Uw2 ? ClassicModularLayoutUw2 : ClassicModularLayout;
		}

		/// <summary>Lines of text Classic Modular's message scroll holds.</summary>
		public const int ClassicModularScrollLines = 5;

		/// <summary>The columns Classic Modular's scroll is wider than the game's original (UWModernScroll.ExtraWidth): the
		/// first game's the original's own width.</summary>
		public const int ClassicModularScrollWidth = 0;

		/// <summary>The second game's, narrowed as the user laid it out with its places (ClassicModularLayoutUw2,
		/// 2026-11-24; the takeover first missed it, the scroll stood at the original's width).</summary>
		public const int ClassicModularScrollWidthUw2 = -48;

		/// <summary>Classic Modular's scroll width for a game.</summary>
		public static int ClassicModularScrollWidthOf(UWGame peGame)
		{
			return peGame == UWGame.Uw2 ? ClassicModularScrollWidthUw2 : ClassicModularScrollWidth;
		}

		/// <summary>The segments' ids for the text table of step 8.5 (UWTextEnglish.Editor.cs).</summary>
		public static readonly string[] StartIds = { "layout.preset.custom", "layout.preset.modern", "layout.preset.classicmodular" };

		/// <summary>The segments' names in the language in force, in the order of UWLayoutStart (plain
		/// English until step 8.5, 2026-10-24).</summary>
		public static string[] StartNames => System.Array.ConvertAll(StartIds, UWText.T);

		/// <summary>Each preset's entries and values, in the order fChooseStart wrote them.</summary>
		public static readonly IReadOnlyDictionary<UWLayoutStart, KeyValuePair<UWSettingDef, object>[]> Values =
			new Dictionary<UWLayoutStart, KeyValuePair<UWSettingDef, object>[]>
			{
				{ UWLayoutStart.Custom, new KeyValuePair<UWSettingDef, object>[0] },
				{ UWLayoutStart.Modern, fStart(false) },
				{ UWLayoutStart.ClassicModular, fStart(true) }
			};

		/// <summary>Writes the preset's values into the settings (nothing for Custom); Classic Modular's places and scroll
		/// width are the game's (ClassicModularLayoutOf, ClassicModularScrollWidthOf).</summary>
		public static void Apply(UWLayoutStart peStart, UWUserData pOData, UWGame peGame = UWGame.Uw1)
		{
			foreach (KeyValuePair<UWSettingDef, object> lOValue in Values[peStart])
				lOValue.Key.SetRaw(pOData, lOValue.Value);

			if (peStart == UWLayoutStart.ClassicModular && peGame != UWGame.Uw1)
			{
				UWUserSettingsTable.ModernLayout.SetRaw(pOData, ClassicModularLayoutOf(peGame));
				UWUserSettingsTable.ModernScrollWidth.SetRaw(pOData, ClassicModularScrollWidthOf(peGame));

				// The second game's gem stays in its compass (UWModernLayout.IsGemOfTheCompass); "apart" frees it there.
				UWUserSettingsTable.ModernGemApart.SetRaw(pOData, false);
			}
		}

		/// <summary>Modern clears what Classic Modular brings; Classic Modular adds its scroll's
		/// lines, the outlines and no generated backs.</summary>
		private static KeyValuePair<UWSettingDef, object>[] fStart(bool pbClassic)
		{
			List<KeyValuePair<UWSettingDef, object>> lOValues = new List<KeyValuePair<UWSettingDef, object>>
			{
				fValue(UWUserSettingsTable.ModernLayout, pbClassic ? ClassicModularLayout : string.Empty),
				fValue(UWUserSettingsTable.ModernCompass, pbClassic),
				fValue(UWUserSettingsTable.ModernCompassDisc, pbClassic),
				fValue(UWUserSettingsTable.ModernScroll, pbClassic),
				fValue(UWUserSettingsTable.ModernGemApart, pbClassic),
				fValue(UWUserSettingsTable.ModernFlasksApart, pbClassic),
				fValue(UWUserSettingsTable.ModernStoneShelf, pbClassic),
				fValue(UWUserSettingsTable.ModernRuneHollow, pbClassic),
				fValue(UWUserSettingsTable.ModernSpellHollow, pbClassic),
				fValue(UWUserSettingsTable.ModernRuneTablet, pbClassic),
				fValue(UWUserSettingsTable.ModernStatsPanel, pbClassic),
				fValue(UWUserSettingsTable.ModernCharacterPage, pbClassic),
				fValue(UWUserSettingsTable.ModernConversationOriginal, pbClassic),
				fValue(UWUserSettingsTable.MinimapHidden, pbClassic),
				fValue(UWUserSettingsTable.ActionBarHidden, pbClassic),
				fValue(UWUserSettingsTable.ModernMessageLines, 0)
			};

			if (pbClassic)
			{
				lOValues.Add(fValue(UWUserSettingsTable.ModernScrollLines, ClassicModularScrollLines));
				lOValues.Add(fValue(UWUserSettingsTable.ModernScrollWidth, ClassicModularScrollWidth));
				lOValues.Add(fValue(UWUserSettingsTable.ModernCompassOutline, true));
				lOValues.Add(fValue(UWUserSettingsTable.ModernRuneHollowOutline, true));
				lOValues.Add(fValue(UWUserSettingsTable.ModernConversationNoOutline, false));
				lOValues.Add(fValue(UWUserSettingsTable.ModernBacks, string.Empty));
			}

			return lOValues.ToArray();
		}

		private static KeyValuePair<UWSettingDef, object> fValue(UWSettingDef pODef, object pOValue)
		{
			return new KeyValuePair<UWSettingDef, object>(pODef, pOValue);
		}
	}
}
