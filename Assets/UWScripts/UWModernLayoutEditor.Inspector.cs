using System;
using System.Collections.Generic;
using UnityEngine;
using UWDataImport.Presentation;
using UWDataImport.UWData;
using Defs = UWDataImport.UWData.UWUserSettingsTable;
using Element = UWModernLayout.ElementEnum;

/// <summary>
/// THE INSPECTOR'S ROWS AS LISTS PER PART (cleanup step 8.4, 2026-10-24; Docs/CLEANUP-PLAN.md 8.7):
/// what the inspector offers with nothing selected, for each part, and for every part's back. Until
/// then fGatherItems built them in one switch of some two hundred lines, every switch row writing
/// its read, its two names and its write by hand.
///
/// A ROW THAT IS A SETTING names its entry of the settings table (UWUserSettingsTable) and takes its
/// label, its two names and which of them is "on" from there (the entry's Invert: a part's Shown
/// On / Off); a row may name them otherwise where one setting has two rows (the compass as the
/// heading's "Direction", the flasks' and the gem's "Stands"). Each row is made anew in every pass
/// (fGatherItems) from the settings as they are, and its change is written through the table and
/// saved (in memory while the editor's session holds the file, UWUserSettings.SaveSuspended).
/// </summary>
public partial class UWModernLayoutEditor
{
    /// <summary>One row of a list: whether it is there now, and the item it shows for a part.</summary>
    private sealed class InspectorRow
    {
        /// <summary>The row's stable id, for the text table of step 8.5.</summary>
        public string Id;

        /// <summary>There only while this holds (null: always).</summary>
        public Func<bool> VisibleWhen;

        public Func<Element, InspectorItem> Make;
    }

    /// <summary>With nothing selected: the settings of the whole interface - its font (UWUiFonts;
    /// per user, 2026-10-09: the original's font as an option, Modern keeping its own) -, then the
    /// parts switched off, to be switched on again.</summary>
    private InspectorRow[] mOOverviewRows;

    /// <summary>Each part's own rows, between its Size and its back.</summary>
    private Dictionary<Element, InspectorRow[]> mOPartRows;

    /// <summary>A part's back: shape, colour, pattern, border (fOffersBack says which parts have one).</summary>
    private InspectorRow[] mOBackRows;

    private void fBuildInspectorRows()
    {
        mOOverviewRows = new[]
        {
            new InspectorRow
            {
                Id = Defs.InterfaceFont.LabelId,
                Make = peElement => fChoice(Defs.InterfaceFont.Label, Defs.InterfaceFont.ChoiceLabels, UWUserSettings.InterfaceFont == 1 ? 1 : 0,
                    liAt => { UWUserSettings.InterfaceFont = liAt; fSaveSettings(); })
            },
            fShow(UWText.T("editor.part.minimap"), Defs.MinimapHidden, true),
            fShow(UWText.T("editor.part.action_bar"), Defs.ActionBarHidden, true),
            fShow(UWText.T("editor.part.compass"), Defs.ModernCompass, false),
            fShow(UWText.T("editor.part.stone_shelf"), Defs.ModernStoneShelf, false),
            fShow(UWText.T("editor.part.rune_hollow"), Defs.ModernRuneHollow, false),
            fShow(UWText.T("editor.part.rune_tablet"), Defs.ModernRuneTablet, false),
            fShow(UWText.T("editor.part.stats_panel"), Defs.ModernStatsPanel, false),
            fShow(UWText.T("editor.part.character_page"), Defs.ModernCharacterPage, false)
        };

        InspectorRow[] lOFlaskStands =
        {
            // Apart or back on the shelf; the part being inspected stands apart (it is shown).
            fSwitch(Defs.ModernFlasksApart, UWText.T("editor.stands"), fStands(), true, 0)
        };

        mOPartRows = new Dictionary<Element, InspectorRow[]>
        {
            // Off hides the bar and its keys (per user, 2026-10-10); Show brings it back.
            { Element.ActionBar, new[] { fSwitch(Defs.ActionBarHidden) } },
            { Element.Minimap, new[] { fSwitch(Defs.MinimapHidden), fSwitch(Defs.MinimapTurns) } },
            { Element.Heading, new[] { fSwitch(Defs.ModernCompass, UWText.T("editor.direction"),
                new[] { UWText.T("editor.direction.text"), UWText.T("editor.direction.compass") }, false) } },
            {
                Element.Compass, new[]
                {
                    fSwitch(Defs.ModernCompass),
                    // The free cross or the original's stone disc with the movement arrows (per user, 2026-10-10).
                    fSwitch(Defs.ModernCompassDisc),
                    // The disc's black outline, as the original's in the frame (per user, 2026-10-10).
                    fSwitch(Defs.ModernCompassOutline, pOVisibleWhen: () => UWModernLayout.IsCompassDisc)
                }
            },
            {
                Element.Vitals, new[]
                {
                    // The power gem between the flasks (Modern) or on a stand of its own (the
                    // original's; per user, 2026-10-09).
                    fSwitch(Defs.ModernGemApart),
                    // The flasks freed one by one (per user, 2026-10-10) - the shelf goes, the gem stands apart.
                    fSwitch(Defs.ModernFlasksApart)
                }
            },
            {
                Element.Conversation, new[]
                {
                    // The original's look (per user, 2026-10-10: "free the conversation UI").
                    fSwitch(Defs.ModernConversationOriginal),
                    // The history's and the answers' text size: here only, no more in the game (per
                    // user, 2026-10-10).
                    new InspectorRow { Id = Defs.ConversationTextPercent.LabelId, Make = fConversationTextRow },
                    // The original look's black outline, on by default (per user, 2026-10-10).
                    fSwitch(Defs.ModernConversationNoOutline, pOVisibleWhen: () => UWModernLayout.IsConversationOriginal)
                }
            },
            // A part being inspected is shown: its Shown row reads On (review F 2.8, as before).
            { Element.CharacterPage, new[] { fSwitch(Defs.ModernCharacterPage, piFixedCurrent: 0) } },
            { Element.StatsPanel, new[] { fSwitch(Defs.ModernStatsPanel, piFixedCurrent: 0) } },
            // Off brings the rune panel back.
            { Element.RuneTablet, new[] { fSwitch(Defs.ModernRuneTablet, piFixedCurrent: 0) } },
            {
                Element.RuneHollow, new[]
                {
                    fSwitch(Defs.ModernRuneHollow, piFixedCurrent: 0),
                    // A black outline, as the compass disc's (per user, 2026-10-10).
                    fSwitch(Defs.ModernRuneHollowOutline)
                }
            },
            { Element.HealthFlask, lOFlaskStands },
            { Element.ManaFlask, lOFlaskStands },
            {
                // Back on the shelf takes the flasks back as well: without them there is none.
                Element.PowerGem, new[]
                {
                    fSwitch(Defs.ModernGemApart, UWText.T("editor.stands"), fStands(), true, 0, piAt =>
                    {
                        if (piAt == 1)
                            UWUserSettings.ModernFlasksApart = false;
                    })
                }
            },
            {
                // A deco panel (per user, 2026-10-09): shown or not, and its width in the shelf's own
                // pixels, a tile (12) at a time. The edges can be dragged with the mouse too (fGripAt).
                Element.StoneShelf, new[]
                {
                    fSwitch(Defs.ModernStoneShelf, piFixedCurrent: 0),
                    new InspectorRow { Id = Defs.ModernStoneShelfWidth.LabelId, Make = fShelfWidthRow }
                }
            },
            {
                Element.Messages, new[]
                {
                    fSwitch(Defs.ModernScroll),
                    // The lines the scroll shows are its paper's rows, one more than the setting counts
                    // (UWModernScroll.Rows, 2026-10-20).
                    new InspectorRow { Id = Defs.ModernScrollLines.LabelId, VisibleWhen = () => UWUserSettings.ModernScroll, Make = fScrollLinesRow },
                    // The dark box's lines (issue 4, 2026-10-21: its height could not be changed).
                    new InspectorRow { Id = Defs.ModernMessageLines.LabelId, VisibleWhen = () => !UWUserSettings.ModernScroll, Make = fBoxLinesRow }
                }
            },
            { Element.CharacterPanel, new[] { new InspectorRow { Id = "layout.window", Make = fDockRow } } },
            { Element.RunePanel, new[] { new InspectorRow { Id = "layout.window", Make = fDockRow } } }
        };

        mOBackRows = fBackRows();
    }

    /// <summary>The flasks' and the gem's Stands row: apart, or back on the shelf.</summary>
    private static string[] fStands()
    {
        return new[] { UWText.T("editor.stands.apart"), UWText.T("editor.stands.on_shelf") };
    }

    /// <summary>
    /// A switch of the settings table as two named segments: the entry's label and names unless
    /// given, "on" the second unless inverted. piFixedCurrent: the segment shown as chosen whatever
    /// the setting holds; pOAfter: what else a choice sets, before the file is written.
    /// </summary>
    private static InspectorRow fSwitch(UWBoolSettingDef pODef, string psLabel = null, string[] psNames = null, bool? pbInvert = null,
        int piFixedCurrent = -1, Action<int> pOAfter = null, Func<bool> pOVisibleWhen = null)
    {
        bool lbInvert = pbInvert ?? pODef.Invert;
        string lsLabel = psLabel ?? pODef.Label;
        string[] lsNames = psNames ?? pODef.ChoiceLabels;

        return new InspectorRow
        {
            Id = pODef.LabelId,
            VisibleWhen = pOVisibleWhen,
            Make = peElement => fChoice(lsLabel, lsNames,
                piFixedCurrent >= 0 ? piFixedCurrent : (pODef.Read(UWUserSettings.Data) != lbInvert ? 1 : 0),
                liAt =>
                {
                    pODef.Write(UWUserSettings.Data, (liAt == 1) != lbInvert);

                    if (pOAfter != null)
                        pOAfter(liAt);

                    fSaveSettings();
                })
        };
    }

    /// <summary>A part switched off, with Show to switch it on again: there while the setting holds
    /// pbHidden.</summary>
    private static InspectorRow fShow(string psPart, UWBoolSettingDef pODef, bool pbHidden)
    {
        return new InspectorRow
        {
            Id = pODef.LabelId + ".show",
            VisibleWhen = () => pODef.Read(UWUserSettings.Data) == pbHidden,
            Make = peElement => fAction(psPart, UWText.T("editor.show"), liAt => { pODef.Write(UWUserSettings.Data, !pbHidden); fSaveSettings(); })
        };
    }

    /// <summary>A number of the settings table with - and +, written in its format.</summary>
    private static InspectorItem fStepper(UWSettingDef pODef, int piValue, bool pbCanDown, bool pbCanUp, Action<int> pOChoose)
    {
        return fStepper(pODef.Label, UWSetupPageDrawer.FormatOf(pODef.Format)(piValue), pbCanDown, pbCanUp, pOChoose);
    }

    private static InspectorItem fConversationTextRow(Element peElement)
    {
        int liTextPercent = UWModernConversation.TextPercent;

        return fStepper(Defs.ConversationTextPercent, liTextPercent, liTextPercent > UWModernConversation.MinTextPercent,
            liTextPercent < UWModernConversation.MaxTextPercent, liAt =>
            {
                UWUserSettings.ConversationTextPercent = Mathf.Clamp(liTextPercent
                    + (liAt == 0 ? -UWModernConversation.TextPercentStep : UWModernConversation.TextPercentStep),
                    UWModernConversation.MinTextPercent, UWModernConversation.MaxTextPercent);
                fSaveSettings();
            });
    }

    private static InspectorItem fShelfWidthRow(Element peElement)
    {
        int liMax = UWModernStoneShelf.StoneShelfMaxWidth;
        int liWidth = Mathf.Min(UWModernStoneShelf.StoneShelfWidth, liMax);

        return fStepper(Defs.ModernStoneShelfWidth, liWidth, liWidth > UWHudArt.StoneShelfMinWidth, liWidth < liMax, liAt =>
        {
            UWUserSettings.ModernStoneShelfWidth = Mathf.Clamp(liWidth + (liAt == 0 ? -12 : 12), UWHudArt.StoneShelfMinWidth, liMax);
            fSaveSettings();
        });
    }

    private static InspectorItem fScrollLinesRow(Element peElement)
    {
        int liLines = UWModernScroll.Lines;

        return fStepper(Defs.ModernScrollLines, liLines + 1, liLines > UWModernScroll.MinLines, liLines < UWModernScroll.MaxLines, liAt =>
        {
            UWUserSettings.ModernScrollLines = Mathf.Clamp(liLines + (liAt == 0 ? -1 : 1), UWModernScroll.MinLines, UWModernScroll.MaxLines);
            fSaveSettings();
        });
    }

    private static InspectorItem fBoxLinesRow(Element peElement)
    {
        int liLines = Interaction.ModernMessageCount;

        return fStepper(Defs.ModernMessageLines, liLines, liLines > UWModernScroll.MinLines, liLines < Interaction.MessageHistoryCount, liAt =>
        {
            fStepModernMessageLines(liAt == 0 ? -1 : 1);
            fSaveSettings();
        });
    }

    /// <summary>A panel moved is a free window; Dock puts it back at its edge.</summary>
    private static InspectorItem fDockRow(Element peElement)
    {
        bool lbDocked = UWModernLayout.IsDocked(peElement);

        return fAction(UWText.T("editor.window"), lbDocked ? UWText.T("editor.window.docked") : UWText.T("editor.window.dock"), liAt => UWModernLayout.Reset(peElement), !lbDocked);
    }

    /// <summary>
    /// A part's generated back (UWModernBacks, UWBackdropArt; per user, 2026-10-09): its shape, and
    /// once it has one the colour, the pattern and the border - colour and pattern apart, per user.
    /// Every part has it; a leather part chooses between its leather and a back in its place, which
    /// is always a rectangle.
    /// </summary>
    private InspectorRow[] fBackRows()
    {
        return new[]
        {
            // "Background" written out ("Back" also reads as "go back"), "Default" for the leather -
            // the modern scheme's default, not the original's look (per user, 2026-10-09, after
            // "Original") -, "Custom" for a back of the part's own ("Leather / Own" did not please).
            new InspectorRow
            {
                Id = "layout.back.shape",
                Make = peElement =>
                {
                    UWModernBacks.Back lOBack = UWModernBacks.Get(peElement);

                    if (UWModernBacks.IsLeatherPart(peElement))
                    {
                        return fChoice(UWText.T("editor.back"), new[] { UWText.T("editor.back.default"), UWText.T("editor.back.custom") }, lOBack.Shape == UWBackdropArt.ShapeEnum.None ? 0 : 1, liAt =>
                        {
                            UWModernBacks.Back lONew = UWModernBacks.Get(peElement);

                            lONew.Shape = liAt == 0 ? UWBackdropArt.ShapeEnum.None : UWBackdropArt.ShapeEnum.Rect;
                            UWModernBacks.Set(peElement, lONew);
                        });
                    }

                    return fChoice(UWText.T("editor.back"), new[] { UWText.T("editor.back.none"), UWText.T("editor.back.oval"), UWText.T("editor.back.rect") }, (int)lOBack.Shape, liAt =>
                    {
                        UWModernBacks.Back lONew = UWModernBacks.Get(peElement);

                        lONew.Shape = (UWBackdropArt.ShapeEnum)liAt;
                        UWModernBacks.Set(peElement, lONew);
                    });
                }
            },
            // Each choice shown as a sample of itself (per user, the same day, instead of bare radio
            // buttons): the colours in the pattern chosen, the patterns in the colour chosen.
            new InspectorRow
            {
                Id = "layout.back.colour",
                Make = peElement =>
                {
                    UWModernBacks.Back lOBack = UWModernBacks.Get(peElement);
                    InspectorItem lOColours = fRadio(UWText.T("editor.back.colour"), 4, (int)lOBack.Colour, liAt =>
                    {
                        UWModernBacks.Back lONew = UWModernBacks.Get(peElement);

                        lONew.Colour = (UWBackdropArt.ColourEnum)liAt;
                        UWModernBacks.Set(peElement, lONew);
                    }, lOBack.Shape != UWBackdropArt.ShapeEnum.None);

                    lOColours.Previews = new Texture2D[4];

                    for (int liAt = 0; liAt < 4; liAt++)
                        lOColours.Previews[liAt] = fBackPreview(liAt, (int)lOBack.Pattern);

                    return lOColours;
                }
            },
            new InspectorRow
            {
                Id = "layout.back.pattern",
                Make = peElement =>
                {
                    UWModernBacks.Back lOBack = UWModernBacks.Get(peElement);
                    InspectorItem lOPatterns = fRadio(UWText.T("editor.back.pattern"), 4, (int)lOBack.Pattern, liAt =>
                    {
                        UWModernBacks.Back lONew = UWModernBacks.Get(peElement);

                        lONew.Pattern = (UWBackdropArt.PatternEnum)liAt;
                        UWModernBacks.Set(peElement, lONew);
                    }, lOBack.Shape != UWBackdropArt.ShapeEnum.None);

                    lOPatterns.Previews = new Texture2D[4];

                    for (int liAt = 0; liAt < 4; liAt++)
                        lOPatterns.Previews[liAt] = fBackPreview((int)lOBack.Colour, liAt);

                    return lOPatterns;
                }
            },
            new InspectorRow
            {
                Id = "layout.back.border",
                Make = peElement =>
                {
                    UWModernBacks.Back lOBack = UWModernBacks.Get(peElement);

                    return fChoice(UWText.T("editor.back.border"), new[] { UWText.T("editor.back.border.on"), UWText.T("editor.back.border.off") }, lOBack.Border ? 0 : 1, liAt =>
                    {
                        UWModernBacks.Back lONew = UWModernBacks.Get(peElement);

                        lONew.Border = liAt == 0;
                        UWModernBacks.Set(peElement, lONew);
                    }, lOBack.Shape != UWBackdropArt.ShapeEnum.None);
                }
            }
        };
    }

    /// <summary>What the inspector offers for the selection now: the overview with nothing
    /// selected, else the part's size, its own rows, its back where it has one, and Reset part.</summary>
    private void fGatherItems()
    {
        if (mOOverviewRows == null)
            fBuildInspectorRows();

        mOItems.Clear();

        if (miSelected < 0)
        {
            fAddRows(mOOverviewRows, default(Element));

            if (mOItems.Count == 1)
                mOItems.Add(fInfo(UWText.T("editor.overview_hint")));

            return;
        }

        Element leElement = (Element)miSelected;
        int liPercent = UWModernLayout.Percent(leElement);

        mOItems.Add(fStepper(UWText.T("editor.size"), liPercent + " %", true, true, liAt =>
        {
            if (UWModernLayout.TryGetRect(leElement, out Rect lORect))
                UWModernLayout.SetPercent(leElement, liPercent + (liAt == 0 ? -UWModernLayout.PercentStep : UWModernLayout.PercentStep), lORect);
        }));

        if (mOPartRows.TryGetValue(leElement, out InspectorRow[] lORows))
            fAddRows(lORows, leElement);

        if (fOffersBack(leElement))
            fAddRows(mOBackRows, leElement);

        mOItems.Add(fAction(string.Empty, UWText.T("editor.reset_part"), liAt => UWModernLayout.Reset(leElement)));
    }

    private void fAddRows(InspectorRow[] pORows, Element peElement)
    {
        foreach (InspectorRow lORow in pORows)
        {
            if (lORow.VisibleWhen == null || lORow.VisibleWhen())
                mOItems.Add(lORow.Make(peElement));
        }
    }
}
