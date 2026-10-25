using System.Collections.Generic;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 8.4 (2026-10-24): the layout editor's starting points as tables
    /// (UWLayoutPresets). Each preset applied to settings whose every field holds a marker must give
    /// exactly what UWModernLayoutEditor.fChooseStart set by hand until then - the values below are
    /// that method's lines - and must leave every other setting alone (the UI size, the font, the
    /// minimap's turning, the conversation's text size, the shelf's width the editor works out after
    /// the reload, and everything outside the layout).
    /// </summary>
    public static partial class Program
    {
        private static void fCheckLayoutPresets()
        {
            Check.Group("Layout presets (UWLayoutPresets - cleanup step 8.4)");

            Check.Str("presets: the segments' names", string.Join(",", UWLayoutPresets.StartNames), "Custom,Modern,Classic Modular");
            Check.Int("presets: Custom writes nothing", UWLayoutPresets.Values[UWLayoutStart.Custom].Length, 0);

            fCheckLayoutPreset(UWLayoutStart.Custom, new Dictionary<string, object>());

            // fChooseStart, Modern (lbClassic false): the own places cleared, every piece off, the
            // message box's lines back to its default (0).
            fCheckLayoutPreset(UWLayoutStart.Modern, new Dictionary<string, object>
            {
                { "ModernLayout", string.Empty }, { "ModernCompass", false }, { "ModernCompassDisc", false }, { "ModernScroll", false },
                { "ModernGemApart", false }, { "ModernFlasksApart", false }, { "ModernStoneShelf", false }, { "ModernRuneHollow", false },
                { "ModernRuneTablet", false }, { "ModernStatsPanel", false }, { "ModernCharacterPage", false },
                { "ModernConversationOriginal", false }, { "MinimapHidden", false }, { "ActionBarHidden", false }, { "ModernMessageLines", 0 }
            });

            // fChooseStart, Classic Modular: its places and pieces, then the five scroll lines, the
            // two outlines on, the conversation's outline on (NoOutline false) and no backs.
            fCheckLayoutPreset(UWLayoutStart.ClassicModular, new Dictionary<string, object>
            {
                { "ModernLayout", UWLayoutPresets.ClassicModularLayout }, { "ModernCompass", true }, { "ModernCompassDisc", true },
                { "ModernScroll", true }, { "ModernGemApart", true }, { "ModernFlasksApart", true }, { "ModernStoneShelf", true },
                { "ModernRuneHollow", true }, { "ModernRuneTablet", true }, { "ModernStatsPanel", true }, { "ModernCharacterPage", true },
                { "ModernConversationOriginal", true }, { "MinimapHidden", true }, { "ActionBarHidden", true }, { "ModernMessageLines", 0 },
                { "ModernScrollLines", 5 }, { "ModernCompassOutline", true }, { "ModernRuneHollowOutline", true },
                { "ModernConversationNoOutline", false }, { "ModernBacks", string.Empty }
            });

            // The places as the user laid them out and took them over (2026-10-11), pinned.
            Check.Str("presets: Classic Modular's places", UWLayoutPresets.ClassicModularLayout,
                "2:0.5:1:0:-4:100;3:0.5:0:-57.3:40:100;4:0:0:0:6:80;10:0.5:0:-0.12:5.9:100;11:0.5:0:45.42:5.73:100;12:0:0:0:0:100;"
                + "14:1:0:-100.3:5.7:100;15:1:0:-75.4:6:100;16:1:0:-127.6:5.7:100;17:0:0.5:-3.3:-35:100;18:1:0:0:0:100;19:1:0.5:-0.13:36:100;");
        }

        /// <summary>Applies the preset over markers in every field and compares every setting - twice,
        /// the switches' markers once on and once off, so that each written switch shows.</summary>
        private static void fCheckLayoutPreset(UWLayoutStart peStart, Dictionary<string, object> pOWanted)
        {
            fCheckLayoutPreset(peStart, pOWanted, 0);
            fCheckLayoutPreset(peStart, pOWanted, 1);
        }

        private static void fCheckLayoutPreset(UWLayoutStart peStart, Dictionary<string, object> pOWanted, int piParity)
        {
            UWSettingDef[] lODefs = UWUserSettingsTable.All;
            UWUserData lOData = new UWUserData();
            UWUserData lOMarkers = new UWUserData();

            for (int liAt = 0; liAt < lODefs.Length; liAt++)
            {
                object lOMarker = fSettingsMarkers(lODefs[liAt].RawOf(lOData)?.GetType() ?? typeof(string), liAt)[0];

                if (lOMarker is bool)
                    lOMarker = ((liAt + piParity) % 2) == 0;

                lODefs[liAt].SetRaw(lOData, lOMarker);
                lODefs[liAt].SetRaw(lOMarkers, lOMarker);
            }

            UWLayoutPresets.Apply(peStart, lOData);

            StringBuilder lOProblems = new StringBuilder();
            int liWritten = 0;

            foreach (UWSettingDef lODef in lODefs)
            {
                object lOActual = lODef.RawOf(lOData);
                object lOExpected = pOWanted.TryGetValue(lODef.Key, out object lOValue) ? lOValue : lODef.RawOf(lOMarkers);

                if (pOWanted.ContainsKey(lODef.Key))
                    liWritten++;

                if (!Equals(lOActual, lOExpected))
                    lOProblems.Append(lODef.Key).Append(" is ").Append(lOActual).Append(" not ").Append(lOExpected).Append("; ");

                if (pOWanted.ContainsKey(lODef.Key) && lODef.Store != UWSettingStore.Layout)
                    lOProblems.Append(lODef.Key).Append(" is no layout setting; ");
            }

            Check.Str("presets: " + peStart + " sets what fChooseStart set, nothing else (" + piParity + ")", lOProblems.ToString(), string.Empty);
            Check.Int("presets: " + peStart + " writes " + pOWanted.Count + " settings (" + piParity + ")", UWLayoutPresets.Values[peStart].Length, pOWanted.Count);
            Check.Int("presets: " + peStart + " - all of them found (" + piParity + ")", liWritten, pOWanted.Count);
        }
    }
}
