using System;
using System.Collections.Generic;
using System.Globalization;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup phase 8d (2026-10-25): the windows' theme table (UWWindowThemes), its two settings
    /// (UWUserSettingsTable.WindowTheme, WindowBackground) and the carved grounds' generator
    /// (UWThemeGround). Data-free.
    ///
    /// PLAIN IS TODAY'S LOOK: every float of the Plain theme against the literals UWGuiTheme, UWKitRows,
    /// UWKitKeyNav, UWLetterGrid and UWModernPalette held before 8d, bit for bit, and Plain flat - no
    /// picture, no edge, no glow - so the kit's drawers lay exactly the pieces they laid before
    /// (UWKitRows.Ground fills a flat theme's ground with the one fill it always drew).
    ///
    /// THE GENERATOR: the same bytes twice, the clouds and the grain tiling, and a hash of each picture
    /// at a small and at its real size pinned in Pins/window-theme-hashes.txt.
    ///
    /// READABLE TEXT: the contrast (WCAG's ratio of relative luminances, the windows blending in gamma
    /// space as the project does) of each text role over the brightest spot either ground can show -
    /// the gradient's brightest texel, or the clouds' brightest under the light's brightest, with the
    /// grain's lightest speck on top - and over the faces drawn on the ground.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>The least contrast of the running text and of every other text role (WCAG AAA for
        /// the text, AA for the dim lines, the accent and the faces).</summary>
        private const double TextContrast = 7.0;

        private const double RoleContrast = 4.5;

        private static void fCheckWindowThemes(CheckContext pOContext)
        {
            Check.Group("Window themes (UWWindowThemes, UWThemeGround, the theme settings - cleanup phase 8d)");

            fCheckPlainIsToday();
            fCheckThemeSettings();
            fCheckThemeGrounds(pOContext);
            fCheckThemeContrast();
        }

        // ------------------------------------------------- Plain is the look of before

        private static void fCheckPlainIsToday()
        {
            UWWindowTheme lOPlain = UWWindowThemes.Plain;
            List<string> lOProblems = new List<string>();

            // The literals as they stood before cleanup phase 8d (UWGuiTheme's palette; Text and Accent
            // were UWModernPalette.HudText and Accent; Thumb UWKitRows.ThumbColour; Focus
            // UWKitKeyNav.FocusColour; the letter grid's UWLetterGrid's msPanel, msKey, msFocus =
            // UWModernPalette.Gold, and UWGuiTheme.LetterGridHint).
            fSameColour(lOProblems, "Background", lOPlain.Background, 0.05f, 0.05f, 0.06f, 1f);
            fSameColour(lOProblems, "Bar", lOPlain.Bar, 0.11f, 0.11f, 0.13f, 1f);
            fSameColour(lOProblems, "Panel", lOPlain.Panel, 0.14f, 0.14f, 0.16f, 1f);
            fSameColour(lOProblems, "Hover", lOPlain.Hover, 0.22f, 0.22f, 0.26f, 1f);
            fSameColour(lOProblems, "ButtonHover", lOPlain.ButtonHover, 0.30f, 0.30f, 0.35f, 1f);
            fSameColour(lOProblems, "Separator", lOPlain.Separator, 0.30f, 0.30f, 0.34f, 1f);
            fSameColour(lOProblems, "Text", lOPlain.Text, 0.86f, 0.86f, 0.84f, 1f);
            fSameColour(lOProblems, "DimText", lOPlain.DimText, 0.55f, 0.55f, 0.55f, 1f);
            fSameColour(lOProblems, "Accent", lOPlain.Accent, 0.80f, 0.64f, 0.34f, 1f);
            fSameColour(lOProblems, "Overlay", lOPlain.Overlay, 0.08f, 0.08f, 0.09f, 0.96f);
            fSameColour(lOProblems, "ReadingShade", lOPlain.ReadingShade, 0.03f, 0.03f, 0.04f, 0.94f);
            fSameColour(lOProblems, "Error", lOPlain.Error, 0.9f, 0.4f, 0.35f, 1f);
            fSameColour(lOProblems, "Thumb", lOPlain.Thumb, 0.62f, 0.62f, 0.66f, 1f);
            fSameColour(lOProblems, "Focus", lOPlain.Focus, 1f, 0.82f, 0.3f, 1f);
            fSameColour(lOProblems, "LetterGridPanel", lOPlain.LetterGridPanel, 0.11f, 0.08f, 0.05f, 0.94f);
            fSameColour(lOProblems, "LetterGridKey", lOPlain.LetterGridKey, 0.25f, 0.19f, 0.12f, 1f);
            fSameColour(lOProblems, "LetterGridFocus", lOPlain.LetterGridFocus, 0.925f, 0.77f, 0.44f, 1f);
            fSameColour(lOProblems, "LetterGridHint", lOPlain.LetterGridHint, 0.85f, 0.8f, 0.7f, 1f);

            Check.Str("plain: every role the colour of before 8d, bit for bit", string.Join("; ", lOProblems), string.Empty);
            Check.Str("plain: the rich text's accent", lOPlain.AccentHex, "CCA357");
            Check.Bool("plain: flat - no stone, no edges, no glow (the drawers add no piece)",
                !lOPlain.IsCarved && lOPlain.EdgeWidth == 0f && lOPlain.FocusGlow.A == 0f, true);

            // The grounds are the fills the windows drew: a dialog and the editor's window Panel, the bar
            // Bar, the help and the game menu Overlay, the first-run screen Background, the letter grid
            // its panel.
            Check.Bool("plain: each ground the fill of before",
                fSame(lOPlain.GroundColour(UWWindowGroundEnum.Dialog), lOPlain.Panel)
                && fSame(lOPlain.GroundColour(UWWindowGroundEnum.Bar), lOPlain.Bar)
                && fSame(lOPlain.GroundColour(UWWindowGroundEnum.Overlay), lOPlain.Overlay)
                && fSame(lOPlain.GroundColour(UWWindowGroundEnum.Screen), lOPlain.Background)
                && fSame(lOPlain.GroundColour(UWWindowGroundEnum.LetterGrid), lOPlain.LetterGridPanel), true);
            Check.Bool("plain: the first theme, and what an unknown one reads",
                UWWindowThemes.All[0] == lOPlain && UWWindowThemes.Get((UWWindowThemeEnum)7) == lOPlain
                && UWWindowThemes.Get((UWWindowThemeEnum)(-1)) == lOPlain, true);
            Check.Bool("stone: carved, the second theme", UWWindowThemes.Stone.IsCarved && UWWindowThemes.All[1] == UWWindowThemes.Stone
                && UWWindowThemes.Get(UWWindowThemeEnum.Stone) == UWWindowThemes.Stone, true);
        }

        private static void fSameColour(List<string> pOProblems, string psRole, UWThemeColour pOColour, float pfR, float pfG, float pfB, float pfA)
        {
            if (!fSame(pOColour, new UWThemeColour(pfR, pfG, pfB, pfA)))
                pOProblems.Add(string.Format(CultureInfo.InvariantCulture, "{0} is {1},{2},{3},{4}", psRole, pOColour.R, pOColour.G, pOColour.B, pOColour.A));
        }

        private static bool fSame(UWThemeColour pOA, UWThemeColour pOB)
        {
            return BitConverter.SingleToInt32Bits(pOA.R) == BitConverter.SingleToInt32Bits(pOB.R)
                && BitConverter.SingleToInt32Bits(pOA.G) == BitConverter.SingleToInt32Bits(pOB.G)
                && BitConverter.SingleToInt32Bits(pOA.B) == BitConverter.SingleToInt32Bits(pOB.B)
                && BitConverter.SingleToInt32Bits(pOA.A) == BitConverter.SingleToInt32Bits(pOB.A);
        }

        // ------------------------------------------------- The settings

        private static void fCheckThemeSettings()
        {
            UWIntSettingDef lOTheme = UWUserSettingsTable.WindowTheme;
            UWIntSettingDef lOGround = UWUserSettingsTable.WindowBackground;
            UWUserData lOData = new UWUserData();

            Check.Int("settings: a file without the keys reads Plain", lOTheme.Read(lOData), (int)UWWindowThemeEnum.Plain);
            Check.Int("settings: ... and the gradient", lOGround.Read(lOData), (int)UWWindowBackgroundEnum.Gradient);
            Check.Bool("settings: the ground greyed under Plain", lOGround.IsEnabled(lOData), false);

            lOData.WindowTheme = (int)UWWindowThemeEnum.Stone;
            lOData.WindowBackground = (int)UWWindowBackgroundEnum.Clouds;
            Check.Bool("settings: Stone and Clouds read as stored, the ground not greyed",
                lOTheme.Read(lOData) == (int)UWWindowThemeEnum.Stone && lOGround.Read(lOData) == (int)UWWindowBackgroundEnum.Clouds
                && lOGround.IsEnabled(lOData), true);

            lOData.WindowTheme = 9;
            lOData.WindowBackground = -3;
            Check.Bool("settings: a theme or ground the table does not know reads Plain and the gradient",
                lOTheme.Read(lOData) == (int)UWWindowThemeEnum.Plain && lOGround.Read(lOData) == (int)UWWindowBackgroundEnum.Gradient, true);

            Check.Str("settings: the choices", string.Join(",", lOTheme.ChoiceLabels) + " / " + string.Join(",", lOGround.ChoiceLabels),
                "Plain,Stone / Gradient,Clouds");
            Check.Int("settings: a choice per theme", lOTheme.ChoiceIds.Length, UWWindowThemes.All.Length);
            Check.Str("settings: the last two of the file", UWUserSettingsTable.All[UWUserSettingsTable.All.Length - 2].Key + ","
                + UWUserSettingsTable.All[UWUserSettingsTable.All.Length - 1].Key, "WindowTheme,WindowBackground");
        }

        // ------------------------------------------------- The generator

        private static void fCheckThemeGrounds(CheckContext pOContext)
        {
            UWStoneLook lOLook = UWWindowThemes.Stone.Stone;
            SortedDictionary<string, string> lONow = new SortedDictionary<string, string>(StringComparer.Ordinal);

            UWPicture lOGradient = UWThemeGround.Gradient(lOLook, UWThemeGround.GradientSize);
            UWPicture lOClouds = UWThemeGround.Clouds(lOLook, UWThemeGround.CloudTileSize);
            UWPicture lOLight = UWThemeGround.Light(lOLook, UWThemeGround.LightSize);
            UWPicture lOGrain = UWThemeGround.Grain(lOLook, UWThemeGround.GrainSize);

            Check.Bool("grounds: the same bytes twice", fCrc32(lOGradient) == fCrc32(UWThemeGround.Gradient(lOLook, UWThemeGround.GradientSize))
                && fCrc32(lOClouds) == fCrc32(UWThemeGround.Clouds(lOLook, UWThemeGround.CloudTileSize))
                && fCrc32(lOLight) == fCrc32(UWThemeGround.Light(lOLook, UWThemeGround.LightSize))
                && fCrc32(lOGrain) == fCrc32(UWThemeGround.Grain(lOLook, UWThemeGround.GrainSize)), true);
            Check.Bool("grounds: the gradient and the clouds opaque", fAllOpaque(lOGradient) && fAllOpaque(lOClouds), true);
            Check.Bool("grounds: the clouds tile without a seam", fTiles(lOClouds), true);
            Check.Bool("grounds: the grain at most its strength", fMaxAlpha(lOGrain) <= lOLook.GrainAlpha && fMaxAlpha(lOGrain) > 0, true);
            Check.Bool("grounds: the light darkest towards the far corner, torch-warm at the near",
                lOLight.Get(lOLight.Width - 1, lOLight.Height - 1).A > lOLight.Get(lOLight.Width / 2, lOLight.Height / 2).A
                && lOLight.Get(lOLight.Width / 5, lOLight.Height / 6).R > lOLight.Get(lOLight.Width / 5, lOLight.Height / 6).B, true);

            fPinPicture(lONow, "stone: gradient", lOGradient);
            fPinPicture(lONow, "stone: clouds", lOClouds);
            fPinPicture(lONow, "stone: light", lOLight);
            fPinPicture(lONow, "stone: grain", lOGrain);
            fPinPicture(lONow, "stone: gradient small", UWThemeGround.Gradient(lOLook, 32));
            fPinPicture(lONow, "stone: clouds small", UWThemeGround.Clouds(lOLook, 64));
            fPinPicture(lONow, "stone: light small", UWThemeGround.Light(lOLook, 32));
            fPinPicture(lONow, "stone: grain small", UWThemeGround.Grain(lOLook, 32));

            fComparePins(pOContext, "window-theme-hashes.txt", "window grounds", lONow, psName => psName.Substring(0, psName.IndexOf(':')));
        }

        private static void fPinPicture(SortedDictionary<string, string> pONow, string psName, UWPicture pOPicture)
        {
            pONow[psName] = string.Format("{0}x{1} {2:X8}", pOPicture.Width, pOPicture.Height, fCrc32(pOPicture));
        }

        private static bool fAllOpaque(UWPicture pOPicture)
        {
            foreach (UWColor32 lOColour in pOPicture.Pixels)
                if (lOColour.A != 255)
                    return false;

            return true;
        }

        private static int fMaxAlpha(UWPicture pOPicture)
        {
            int liMax = 0;

            foreach (UWColor32 lOColour in pOPicture.Pixels)
                liMax = Math.Max(liMax, lOColour.A);

            return liMax;
        }

        /// <summary>The step across the tile's edges (the last column to the first, the last row to the
        /// first) is no larger than the largest step between neighbours inside it.</summary>
        private static bool fTiles(UWPicture pOPicture)
        {
            int liInside = 0;
            int liSeam = 0;
            int liSize = pOPicture.Width;

            for (int liA = 0; liA < liSize; liA++)
            {
                for (int liB = 0; liB + 1 < liSize; liB++)
                {
                    liInside = Math.Max(liInside, fStep(pOPicture.Get(liB, liA), pOPicture.Get(liB + 1, liA)));
                    liInside = Math.Max(liInside, fStep(pOPicture.Get(liA, liB), pOPicture.Get(liA, liB + 1)));
                }

                liSeam = Math.Max(liSeam, fStep(pOPicture.Get(liSize - 1, liA), pOPicture.Get(0, liA)));
                liSeam = Math.Max(liSeam, fStep(pOPicture.Get(liA, liSize - 1), pOPicture.Get(liA, 0)));
            }

            return liSeam <= liInside;
        }

        private static int fStep(UWColor32 pOA, UWColor32 pOB)
        {
            return Math.Max(Math.Abs(pOA.R - pOB.R), Math.Max(Math.Abs(pOA.G - pOB.G), Math.Abs(pOA.B - pOB.B)));
        }

        // ------------------------------------------------- Contrast

        private static void fCheckThemeContrast()
        {
            foreach (UWWindowTheme lOTheme in UWWindowThemes.All)
            {
                if (!lOTheme.IsCarved)
                {
                    // Plain's own numbers, for the record: its text on its dialog ground.
                    double[] lyPanel = fRgb(lOTheme.Panel);

                    Check.Pass("contrast: plain, for comparison", string.Format(CultureInfo.InvariantCulture,
                        "text {0:0.0}, dim {1:0.0}, accent {2:0.0} on the dialog's ground",
                        fContrast(fRgb(lOTheme.Text), lyPanel), fContrast(fRgb(lOTheme.DimText), lyPanel), fContrast(fRgb(lOTheme.Accent), lyPanel)));
                    continue;
                }

                foreach (UWWindowBackgroundEnum leGround in new[] { UWWindowBackgroundEnum.Gradient, UWWindowBackgroundEnum.Clouds })
                    fCheckContrastOn(lOTheme, leGround);
            }
        }

        private static void fCheckContrastOn(UWWindowTheme pOTheme, UWWindowBackgroundEnum peGround)
        {
            double[] lyGround = fBrightestGround(pOTheme.Stone, peGround);
            string lsName = pOTheme.Id.ToString().ToLowerInvariant() + "/" + peGround.ToString().ToLowerInvariant();
            List<string> lOLow = new List<string>();
            double ldLeast = double.MaxValue;

            // Text straight on the ground: the rows, the hints, titles and the current choice, a failure.
            fNeed(lOLow, ref ldLeast, "text", pOTheme.Text, lyGround, TextContrast);
            fNeed(lOLow, ref ldLeast, "dim", pOTheme.DimText, lyGround, RoleContrast);
            fNeed(lOLow, ref ldLeast, "accent", pOTheme.Accent, lyGround, RoleContrast);
            fNeed(lOLow, ref ldLeast, "error", pOTheme.Error, lyGround, RoleContrast);
            fNeed(lOLow, ref ldLeast, "letter grid hint", pOTheme.LetterGridHint, lyGround, RoleContrast);

            // Text on the faces drawn on the ground: a row or tab under the pointer, a button, the game
            // menu's and the help's buttons, the current tab, a held button's dark text on the accent.
            double[] lyHover = fOver(lyGround, fRgb(pOTheme.Hover), pOTheme.Hover.A);
            double[] lyButtonHover = fOver(lyGround, fRgb(pOTheme.ButtonHover), pOTheme.ButtonHover.A);
            double[] lyPanel = fOver(lyGround, fRgb(pOTheme.Panel), pOTheme.Panel.A);

            fNeed(lOLow, ref ldLeast, "text on hover", pOTheme.Text, lyHover, RoleContrast);
            fNeed(lOLow, ref ldLeast, "accent on hover", pOTheme.Accent, lyHover, RoleContrast);
            fNeed(lOLow, ref ldLeast, "text on a lit button", pOTheme.Text, lyButtonHover, RoleContrast);
            fNeed(lOLow, ref ldLeast, "accent on a lit button", pOTheme.Accent, lyButtonHover, RoleContrast);
            fNeed(lOLow, ref ldLeast, "text on a panel face", pOTheme.Text, lyPanel, RoleContrast);
            fNeed(lOLow, ref ldLeast, "accent on a panel face", pOTheme.Accent, lyPanel, RoleContrast);
            fNeed(lOLow, ref ldLeast, "held button", pOTheme.Background, fRgb(pOTheme.Accent), RoleContrast);
            fNeed(lOLow, ref ldLeast, "text in a field", pOTheme.Text, fRgb(pOTheme.Background), TextContrast);
            fNeed(lOLow, ref ldLeast, "letter grid key", new UWThemeColour(1f, 1f, 1f), fRgb(pOTheme.LetterGridKey), RoleContrast);
            fNeed(lOLow, ref ldLeast, "letter grid focus", new UWThemeColour(0f, 0f, 0f), fRgb(pOTheme.LetterGridFocus), RoleContrast);

            Check.Str(string.Format(CultureInfo.InvariantCulture, "contrast: {0}, every text role over the brightest spot (ground luminance {1:0.000}, least {2:0.0})",
                lsName, fLuminance(lyGround), ldLeast), string.Join("; ", lOLow), string.Empty);
        }

        private static void fNeed(List<string> pOLow, ref double pdLeast, string psRole, UWThemeColour pOText, double[] pyGround, double pdNeeded)
        {
            double ldContrast = fContrast(fRgb(pOText), pyGround);

            pdLeast = Math.Min(pdLeast, ldContrast);

            if (ldContrast < pdNeeded)
                pOLow.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1:0.00} < {2:0.0}", psRole, ldContrast, pdNeeded));
        }

        /// <summary>The brightest colour the carved ground can show under a text (0 to 1, gamma space):
        /// the gradient's brightest texel, or the clouds' brightest under the light's brightest; with
        /// the grain's lightest speck over it.</summary>
        private static double[] fBrightestGround(UWStoneLook pOLook, UWWindowBackgroundEnum peGround)
        {
            double[] lyBest = null;

            if (peGround == UWWindowBackgroundEnum.Gradient)
            {
                foreach (UWColor32 lOColour in UWThemeGround.Gradient(pOLook, UWThemeGround.GradientSize).Pixels)
                    lyBest = fBrighter(lyBest, fRgb(lOColour));
            }
            else
            {
                double[] lyCloud = null;

                foreach (UWColor32 lOColour in UWThemeGround.Clouds(pOLook, UWThemeGround.CloudTileSize).Pixels)
                    lyCloud = fBrighter(lyCloud, fRgb(lOColour));

                foreach (UWColor32 lOColour in UWThemeGround.Light(pOLook, UWThemeGround.LightSize).Pixels)
                    lyBest = fBrighter(lyBest, fOver(lyCloud, fRgb(lOColour), lOColour.A / 255.0));
            }

            return fOver(lyBest, new[] { 1.0, 1.0, 1.0 }, pOLook.GrainAlpha / 255.0);
        }

        private static double[] fBrighter(double[] pyBest, double[] pyColour)
        {
            return pyBest == null || fLuminance(pyColour) > fLuminance(pyBest) ? pyColour : pyBest;
        }

        private static double[] fRgb(UWThemeColour pOColour)
        {
            return new double[] { pOColour.R, pOColour.G, pOColour.B };
        }

        private static double[] fRgb(UWColor32 pOColour)
        {
            return new double[] { pOColour.R / 255.0, pOColour.G / 255.0, pOColour.B / 255.0 };
        }

        /// <summary>pyOver at pdAlpha over pyUnder, blended in gamma space (the project's colour space).</summary>
        private static double[] fOver(double[] pyUnder, double[] pyOver, double pdAlpha)
        {
            return new[]
            {
                (pyUnder[0] * (1.0 - pdAlpha)) + (pyOver[0] * pdAlpha),
                (pyUnder[1] * (1.0 - pdAlpha)) + (pyOver[1] * pdAlpha),
                (pyUnder[2] * (1.0 - pdAlpha)) + (pyOver[2] * pdAlpha)
            };
        }

        /// <summary>WCAG 2's relative luminance of a colour in sRGB, 0 to 1.</summary>
        private static double fLuminance(double[] pyColour)
        {
            return (0.2126 * fLinear(pyColour[0])) + (0.7152 * fLinear(pyColour[1])) + (0.0722 * fLinear(pyColour[2]));
        }

        private static double fLinear(double pdChannel)
        {
            return pdChannel <= 0.03928 ? pdChannel / 12.92 : Math.Pow((pdChannel + 0.055) / 1.055, 2.4);
        }

        private static double fContrast(double[] pyA, double[] pyB)
        {
            double ldA = fLuminance(pyA);
            double ldB = fLuminance(pyB);

            return (Math.Max(ldA, ldB) + 0.05) / (Math.Min(ldA, ldB) + 0.05);
        }
    }
}
