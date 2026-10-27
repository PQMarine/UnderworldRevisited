using System;
using System.Collections.Generic;
using System.Text;
using UWDataImport.Presentation;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 8.4 (2026-10-24): what the settings table says about each setting's row - its
    /// texts and their ids, the choice texts per choice, the inverted switches, and the rules that
    /// grey a row or leave it out (the Motion dialog's rows by the motion mode, the volumes by their
    /// switches, the strengths by their modes, the factor by the resolution switch). Data-free.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckSettingsRows()
        {
            Check.Group("Settings rows (UWSettingDef texts and conditions - cleanup step 8.4)");

            fCheckSettingsRowTexts();
            fCheckSettingsRowConditions();
        }

        private static void fCheckSettingsRowTexts()
        {
            StringBuilder lOProblems = new StringBuilder();
            HashSet<string> lOIds = new HashSet<string>();
            int liDescribed = 0;

            foreach (UWSettingDef lODef in UWUserSettingsTable.All)
            {
                if ((lODef.LabelId == null) != (lODef.Label == null))
                    lOProblems.Append(lODef.Key).Append(" label without id; ");

                if ((lODef.HintId == null) != (lODef.Hint == null))
                    lOProblems.Append(lODef.Key).Append(" hint without id; ");

                if ((lODef.ChoiceIds == null) != (lODef.ChoiceLabels == null)
                    || (lODef.ChoiceIds != null && lODef.ChoiceIds.Length != lODef.ChoiceLabels.Length))
                    lOProblems.Append(lODef.Key).Append(" choice texts and ids differ; ");

                if (lODef.Hint != null && lODef.Label == null)
                    lOProblems.Append(lODef.Key).Append(" hint without label; ");

                foreach (string lsId in new[] { lODef.LabelId, lODef.HintId })
                {
                    if (lsId != null && !lOIds.Add(lsId))
                        lOProblems.Append("id twice: ").Append(lsId).Append("; ");
                }

                if (lODef.ChoiceIds != null)
                {
                    foreach (string lsId in lODef.ChoiceIds)
                    {
                        if (!lOIds.Add(lsId))
                            lOProblems.Append("id twice: ").Append(lsId).Append("; ");
                    }
                }

                if (lODef.Label != null)
                {
                    liDescribed++;

                    if (lODef.LabelId != "setting." + lODef.Key)
                        lOProblems.Append(lODef.Key).Append(" id not from its key; ");

                    if (lODef.Label.StartsWith(" ") || lODef.Label.Length == 0)
                        lOProblems.Append(lODef.Key).Append(" label with leading space or empty; ");
                }

                // As many choice texts as choices: per value among Choices, per mode, per switch two.
                if (lODef.ChoiceLabels != null)
                {
                    int liWanted = -1;

                    if (lODef is UWIntSettingDef lOInt && lOInt.Choices != null)
                        liWanted = lOInt.Choices.Length;
                    else if (lODef is UWIntSettingDef lOMode && lOMode.Normalize == UWSettingNormalize.InRangeOrFallback)
                        liWanted = lOMode.Max - lOMode.Min + 1;
                    else if (lODef.Kind == UWSettingKind.Bool)
                        liWanted = 2;

                    if (liWanted >= 0 && lODef.ChoiceLabels.Length != liWanted)
                        lOProblems.Append(lODef.Key).Append(" has ").Append(lODef.ChoiceLabels.Length).Append(" choice texts for ")
                            .Append(liWanted).Append(" choices; ");
                }

                if (lODef.Invert && lODef.Kind != UWSettingKind.Bool)
                    lOProblems.Append(lODef.Key).Append(" inverted but no switch; ");
            }

            Check.Str("rows: texts, ids and choices consistent", lOProblems.ToString(), string.Empty);
            // 73 since cleanup step 7.7B: the Pointer speed (UWPointer); 75 since cleanup phase 8d: the
            // windows' theme and its ground; 76 since translation wave 1 (2026-10-26): the Language; 77 since
            // 2026-10-27: Music from recordings.
            Check.Int("rows: settings described for the setup bar and the inspector", liDescribed, 77);

            // A few texts as the dialogs showed them (the words must not drift before step 8.5).
            Check.Str("rows: the frame rates' texts", string.Join(",", UWUserSettingsTable.OriginalFrameTicks.ChoiceLabels), "64,32,21,16");
            Check.Str("rows: the ramp's texts", string.Join(",", UWUserSettingsTable.SmoothRampTicks.ChoiceLabels), "0.6 s,0.3 s,0.15 s,At once");
            Check.Str("rows: a mode's texts", string.Join(",", UWUserSettingsTable.SwimViewMode.ChoiceLabels), "Off,Original,Smooth");
            Check.Str("rows: an id", UWUserSettingsTable.HeadBobMode.ChoiceIds[2], "setting.HeadBobMode.choice.smooth");

            // The ramp's and the frame rate's choices map to their ticks and back.
            UWUserData lOData = new UWUserData { SmoothRampTicks = 4, OriginalFrameTicks = 16 };

            Check.Int("rows: ramp 4 ticks is the third choice", UWUserSettingsTable.SmoothRampTicks.IndexOf(lOData), 2);
            Check.Int("rows: the fourth frame choice stores 16", UWUserSettingsTable.OriginalFrameTicks.ValueAt(3), 16);
            Check.Int("rows: frame 16 ticks is the fourth choice", UWUserSettingsTable.OriginalFrameTicks.IndexOf(lOData), 3);
            lOData.SmoothRampTicks = 5;
            Check.Int("rows: a ramp none of the choices reads 8, the second", UWUserSettingsTable.SmoothRampTicks.IndexOf(lOData), 1);
            Check.Int("rows: a mode stores its number", UWUserSettingsTable.HeadBobMode.ValueAt(2), 2);

            // Switches: index 1 is on, except where the entry is inverted (a part's Shown On / Off).
            lOData.ModernCompass = true;
            lOData.MinimapHidden = true;
            lOData.MotionSmoothing = 7;
            Check.Int("rows: the compass shown is Shown On", UWUserSettingsTable.SwitchIndex(UWUserSettingsTable.ModernCompass, lOData), 0);
            Check.Int("rows: the minimap hidden is Shown Off", UWUserSettingsTable.SwitchIndex(UWUserSettingsTable.MinimapHidden, lOData), 1);
            Check.Int("rows: MotionSmoothing 7 is Smooth", UWUserSettingsTable.SwitchIndex(UWUserSettingsTable.MotionSmoothing, lOData), 1);
            UWUserSettingsTable.WriteSwitch(UWUserSettingsTable.MotionSmoothing, lOData, false);
            Check.Int("rows: Original stores 0", lOData.MotionSmoothing, 0);
            UWUserSettingsTable.WriteSwitch(UWUserSettingsTable.ModernStoneShelf, lOData, true);
            Check.Bool("rows: a bool switch stores itself", lOData.ModernStoneShelf, true);
        }

        private static void fCheckSettingsRowConditions()
        {
            UWUserData lOData = new UWUserData { MotionSmoothing = 1 };

            // Motion: the ramp belongs to Smooth, the picture and the frame rate to Original.
            Check.Bool("rows: Smooth - the ramp takes input", UWUserSettingsTable.SmoothRampTicks.IsEnabled(lOData), true);
            Check.Bool("rows: Smooth - the picture is greyed", UWUserSettingsTable.MotionViewStepped.IsEnabled(lOData), false);
            Check.Bool("rows: Smooth - the frame rate is greyed", UWUserSettingsTable.OriginalFrameTicks.IsEnabled(lOData), false);
            lOData.MotionSmoothing = 0;
            Check.Bool("rows: Original - the ramp is greyed", UWUserSettingsTable.SmoothRampTicks.IsEnabled(lOData), false);
            Check.Bool("rows: Original - the picture takes input", UWUserSettingsTable.MotionViewStepped.IsEnabled(lOData), true);
            Check.Bool("rows: Original - the frame rate takes input", UWUserSettingsTable.OriginalFrameTicks.IsEnabled(lOData), true);
            Check.Bool("rows: the motion switch itself always takes input", UWUserSettingsTable.MotionSmoothing.IsEnabled(lOData), true);

            // The strengths only under Smooth; a mode out of range reads Original.
            foreach (int liMode in new[] { 0, 1, 2, 9 })
            {
                lOData.HeadBobMode = liMode;
                lOData.WeaponJitterMode = liMode;
                lOData.SwimViewMode = liMode;

                bool lbWanted = liMode == 2;

                Check.Bool("rows: head bob mode " + liMode + " shows the strength", UWUserSettingsTable.HeadBobStrength.IsVisible(lOData), lbWanted);
                Check.Bool("rows: weapon mode " + liMode + " shows the strength", UWUserSettingsTable.WeaponJitterStrength.IsVisible(lOData), lbWanted);
                Check.Bool("rows: swimming mode " + liMode + " shows the strength", UWUserSettingsTable.SwimViewStrength.IsVisible(lOData), lbWanted);
            }

            // The volumes by their switches.
            lOData.MusicEnabled = false;
            lOData.SoundEnabled = true;
            Check.Bool("rows: music off greys its volume", UWUserSettingsTable.MusicVolume.IsEnabled(lOData), false);
            Check.Bool("rows: sound on leaves its volume", UWUserSettingsTable.SoundVolume.IsEnabled(lOData), true);
            lOData.MusicEnabled = true;
            lOData.SoundEnabled = false;
            Check.Bool("rows: music on leaves its volume", UWUserSettingsTable.MusicVolume.IsEnabled(lOData), true);
            Check.Bool("rows: sound off greys its volume", UWUserSettingsTable.SoundVolume.IsEnabled(lOData), false);

            // The factor's slider only at the original's resolution.
            lOData.OriginalWorldResolution = false;
            Check.Bool("rows: no factor at the full resolution", UWUserSettingsTable.OriginalWorldResolutionFactor.IsVisible(lOData), false);
            lOData.OriginalWorldResolution = true;
            Check.Bool("rows: the factor at the original's", UWUserSettingsTable.OriginalWorldResolutionFactor.IsVisible(lOData), true);

            // The windows' ground only for a carved theme (cleanup phase 8d).
            lOData.WindowTheme = (int)UWWindowThemeEnum.Plain;
            Check.Bool("rows: Plain greys the windows' ground", UWUserSettingsTable.WindowBackground.IsEnabled(lOData), false);
            lOData.WindowTheme = (int)UWWindowThemeEnum.Stone;
            Check.Bool("rows: Stone leaves the windows' ground", UWUserSettingsTable.WindowBackground.IsEnabled(lOData), true);

            // Everything else is always there and takes input.
            StringBuilder lOOther = new StringBuilder();
            HashSet<UWSettingDef> lOConditional = new HashSet<UWSettingDef>
            {
                UWUserSettingsTable.SmoothRampTicks, UWUserSettingsTable.MotionViewStepped, UWUserSettingsTable.OriginalFrameTicks,
                UWUserSettingsTable.HeadBobStrength, UWUserSettingsTable.WeaponJitterStrength, UWUserSettingsTable.SwimViewStrength,
                UWUserSettingsTable.MusicVolume, UWUserSettingsTable.SoundVolume, UWUserSettingsTable.OriginalWorldResolutionFactor,
                UWUserSettingsTable.WindowBackground
            };

            foreach (UWSettingDef lODef in UWUserSettingsTable.All)
            {
                if (!lOConditional.Contains(lODef) && (lODef.EnabledWhen != null || lODef.VisibleWhen != null))
                    lOOther.Append(lODef.Key).Append("; ");
            }

            Check.Str("rows: no other setting has a condition", lOOther.ToString(), string.Empty);
        }
    }
}
