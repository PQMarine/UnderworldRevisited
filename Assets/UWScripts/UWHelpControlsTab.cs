using UnityEngine;
using UWDataImport.UWData;

/// <summary>The help's CONTROLS tab: the controls of the scheme in force (UWHelpControls), with
/// the gamepad's glyphs. Moved out of UWHelpWindow (cleanup 8.6, 2026-10-24).</summary>
public sealed class UWHelpControlsTab : UWHelpTab
{
    public override UWHelpTabId Id => UWHelpTabId.Controls;

    public override string TitleId => "help.tab.controls";

    /// <summary>The controls of the scheme in force (UWHelpControls): per section a heading, a dim
    /// line and the keys in the accent colour beside what they do. In either form (IUWHelpFlow,
    /// cleanup 8b.3, 2026-10-24); the keys' column and the glyphs' size are UWHelpFlowText's since.</summary>
    protected override void OnFlow(IUWHelpFlow pOFlow)
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;
        bool lbModern = lOScheme != null && UWControlScheme.IsModern;

        pOFlow.Line(lbModern ? UWText.T("help.controls.modern.title") : UWText.T("help.controls.classic.title"), UWHelpLookEnum.Heading);

        foreach (UWHelpControls.Section lOSection in UWHelpControls.Build(lbModern))
        {
            pOFlow.Space(10f);
            pOFlow.Line(lOSection.Title, UWHelpLookEnum.SubHeading);

            if (!string.IsNullOrEmpty(lOSection.Intro))
                pOFlow.Line(lOSection.Intro, UWHelpLookEnum.Dim);

            foreach (UWHelpControls.Row lORow in lOSection.Rows)
            {
                // GAMEPAD GLYPHS in the keys' column (UWHelpControls.AddPad, UWGlyphs), glyphs and text
                // centred on each other (per user, 2026-10-08: the text stood at the glyphs' top).
                if (lORow.Glyphs != null)
                    pOFlow.GlyphRow(lORow.Glyphs, lORow.Text);
                else
                    pOFlow.KeyRow(UWHelpFlowText.Keys(lORow.Keys), lORow.Text);

                pOFlow.Space(3f);
            }
        }
    }
}
