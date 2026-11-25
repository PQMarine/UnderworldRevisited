using System;
using System.Collections.Generic;
using UnityEngine;
using UWDataImport.Presentation;
using UWDataImport.UWData;
using Defs = UWDataImport.UWData.UWUserSettingsTable;
using Element = UWModernLayout.ElementEnum;

/// <summary>
/// THE INSPECTOR'S ROWS AS LISTS PER PART (cleanup step 8.4, 2026-10-24; section 8.7 of the cleanup plan):
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

    /// <summary>The language the inspector's rows were built in (UWText.LanguageVersion).</summary>
    private int miInspectorLanguage = -1;

    /// <summary>The game the rows were built for (UWTextures.Game boxed, null without data).</summary>
    private object mOInspectorGame;

    /// <summary>Each part's own rows, between its Size and its back.</summary>
    private Dictionary<Element, InspectorRow[]> mOPartRows;

    /// <summary>A part's back: shape, colour, pattern, border (fOffersBack says which parts have one).</summary>
    private InspectorRow[] mOBackRows;

    private void fBuildInspectorRows()
    {
        mOOverviewRows = new[]
        {
            // The font left the editor on 2026-11-19 (per user): the Interface window sets it for every preset.
            fShow(UWText.T("editor.part.minimap"), Defs.MinimapHidden, true),
            fShow(UWText.T("editor.part.action_bar"), Defs.ActionBarHidden, true),
            // THE PARTS THE RUNNING GAME HAS (part U4e step 3 of the UW2 plan): the classic kinds' pieces the game does
            // not have are not offered. The pages and the tablet alone are the second game's too since stage U5 (the
            // tablet is the rune panel's own mode, its page per game since part U4c); the rest comes with U6 and U7.
            fOnly(fShow(UWText.T("editor.part.compass"), Defs.ModernCompass, false), () => fHas(UWModernPartEnum.Compass)),
            fOnly(fShow(UWText.T("editor.part.stone_shelf"), Defs.ModernStoneShelf, false), () => fHas(UWModernPartEnum.StoneShelf)),
            fOnly(fShow(UWText.T("editor.part.rune_hollow"), Defs.ModernRuneHollow, false), () => fHas(UWModernPartEnum.RuneHollow)),
            fOnly(fShow(UWText.T("editor.part.rune_tablet"), Defs.ModernRuneTablet, false), () => fHas(UWModernPartEnum.RunePanel)),
            fOnly(fShow(UWText.T("editor.part.stats_panel"), Defs.ModernStatsPanel, false), () => fHas(UWModernPartEnum.StatsPage)),
            fOnly(fShow(UWText.T("editor.part.character_page"), Defs.ModernCharacterPage, false), () => fHas(UWModernPartEnum.CharacterPage))
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
                new[] { UWText.T("editor.direction.text"), UWText.T("editor.direction.compass") }, false,
                pOVisibleWhen: () => fHas(UWModernPartEnum.Compass)) } },
            {
                Element.Compass, new[]
                {
                    fSwitch(Defs.ModernCompass),
                    // The free cross or the original's stone disc with the movement arrows (per user, 2026-10-10); the
                    // second game's compass lies in a hollow of its frame, so there the choice says so (per user, 2026-11-23).
                    fSwitch(Defs.ModernCompassDisc, psNames: fCompassLooks()),
                    // The disc's black outline, as the original's in the frame (per user, 2026-10-10).
                    fSwitch(Defs.ModernCompassOutline, pOVisibleWhen: () => UWModernLayout.IsCompassDisc),
                    // The second game's gem in the compass or apart, alone (per user, 2026-11-24).
                    fSwitch(Defs.ModernGemApart, psNames: new[] { UWText.T("setting.ModernGemApart.choice.compass"), UWText.T("setting.ModernGemApart.choice.apart") },
                        pOVisibleWhen: () => UWModernLayout.IsGemOfTheCompass && UWModernLayout.IsCompassShown)
                }
            },
            {
                Element.Vitals, new[]
                {
                    // The power gem between the flasks (Modern) or on a stand of its own (the
                    // original's; per user, 2026-10-09). The stand and the shelf are the first game's art.
                    // Where the gem lies in the compass (the second game's while it shows) the compass offers it.
                    fSwitch(Defs.ModernGemApart, pOVisibleWhen: () => fHasClassicPieces() && !fIsGemBackInCompass()),
                    // The flasks freed one by one (per user, 2026-10-10) - the shelf goes, the gem stands apart.
                    fSwitch(Defs.ModernFlasksApart, pOVisibleWhen: fHasClassicPieces)
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
            // The active spells as a row or in the frame's hollow, where the game has one (UW2 plan stage U6 step 2c).
            { Element.Spells, new[] { fSwitch(Defs.ModernSpellHollow, pOVisibleWhen: () => fHas(UWModernPartEnum.SpellHollow)) } },
            { Element.HealthFlask, lOFlaskStands },
            { Element.ManaFlask, lOFlaskStands },
            {
                // Back on the shelf takes the flasks back as well: without them there is none. Back into the second
                // game's compass, while it shows, leaves the flasks as they are.
                Element.PowerGem, new[]
                {
                    fOnly(fSwitch(Defs.ModernGemApart, UWText.T("editor.stands"), fStands(), true, 0, piAt =>
                    {
                        if (piAt == 1)
                            UWUserSettings.ModernFlasksApart = false;
                    }), () => !fIsGemBackInCompass()),
                    fOnly(fSwitch(Defs.ModernGemApart, UWText.T("editor.stands"),
                        new[] { UWText.T("editor.stands.apart"), UWText.T("editor.stands.in_compass") }, true, 0), fIsGemBackInCompass)
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
                    // The original's scroll, from each game's own frame (the second game's since UW2 plan stage U6 step 2b).
                    fSwitch(Defs.ModernScroll),
                    // The lines the scroll shows are its paper's rows, one more than the setting counts
                    // (UWModernScroll.Rows, 2026-10-20).
                    new InspectorRow { Id = Defs.ModernScrollLines.LabelId, VisibleWhen = () => UWUserSettings.ModernScroll, Make = fScrollLinesRow },
                    // Its width (GitHub issue 29, 2026-11-10): the paper between the rollers, WidthStep at a time; its
                    // right edge can be dragged too (fScrollGripAt).
                    new InspectorRow { Id = Defs.ModernScrollWidth.LabelId, VisibleWhen = () => UWUserSettings.ModernScroll, Make = fScrollWidthRow },
                    // The dark box's lines (issue 4, 2026-10-21: its height could not be changed).
                    new InspectorRow { Id = Defs.ModernMessageLines.LabelId, VisibleWhen = () => !UWUserSettings.ModernScroll, Make = fBoxLinesRow },
                    // The box's lines fade out after a time (GitHub issue 18, 2026-11-07); never by default.
                    new InspectorRow { Id = Defs.ModernMessageFade.LabelId, VisibleWhen = () => !UWUserSettings.ModernScroll, Make = fBoxFadeRow }
                }
            },
            { Element.CharacterPanel, new[] { new InspectorRow { Id = "layout.window", Make = fDockRow } } },
            { Element.RunePanel, new[] { new InspectorRow { Id = "layout.window", Make = fDockRow } } }
        };

        mOBackRows = fBackRows();
    }

    /// <summary>The compass's two looks: the cross, and the disc (the first game) or the hollow (the second, which has no
    /// disc - UWCompassGeometry.HasArrows tells the games apart, the disc carrying the arrows).</summary>
    private static string[] fCompassLooks()
    {
        string[] lsNames = (string[])Defs.ModernCompassDisc.ChoiceLabels.Clone();
        UWDataImport.DataImport lOData = UWModernUi.Data;

        if (lsNames.Length > 1 && lOData != null && !UWCompassGeometry.Of(lOData.Textures).HasArrows)
            lsNames[1] = UWText.T("editor.compass.hollow");

        return lsNames;
    }

    /// <summary>Whether the running game has the part (IUWModernSession.Has).</summary>
    private static bool fHas(UWModernPartEnum pePart)
    {
        return UWModernUi.Has(pePart);
    }

    /// <summary>The classic kinds' pieces without a part of their own (the stands): the game that builds Classic+ has
    /// them. The tablet alone left them in stage U5 step 3 (it is the rune panel's), the scroll in stage U6 step 2b.</summary>
    private static bool fHasClassicPieces()
    {
        return UWModernUi.Session == null || UWModernUi.Session.Supports(UWModernLayout.PresetEnum.ClassicPlus);
    }

    /// <summary>A row shown only while the game has its part.</summary>
    private static InspectorRow fOnly(InspectorRow pORow, Func<bool> pOHas)
    {
        Func<bool> lOBefore = pORow.VisibleWhen;

        pORow.VisibleWhen = lOBefore == null ? pOHas : () => pOHas() && lOBefore();

        return pORow;
    }

    /// <summary>Whether the gem goes back into the compass, not onto the shelf: the second game's, while its compass
    /// shows (UWModernLayout.IsGemOfTheCompass).</summary>
    private static bool fIsGemBackInCompass()
    {
        return UWModernLayout.IsGemOfTheCompass && UWModernLayout.IsCompassShown;
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

    private static InspectorItem fScrollWidthRow(Element peElement)
    {
        int liExtra = UWModernScroll.ExtraWidth;

        // Wider or narrower to the right only (per user, 2026-11-10), as the grip at its right edge.
        // The number is the scroll's own width (320 the original's); narrower too since issue 29's second wish.
        return fStepper(Defs.ModernScrollWidth, UWModernScroll.OwnWidth, liExtra > UWModernScroll.MinExtraWidth, liExtra < UWModernScroll.MaxExtraWidth, liAt =>
        {
            int liTo = liExtra + (liAt == 0 ? -UWModernScroll.WidthStep : UWModernScroll.WidthStep);

            if (UWModernLayout.TryGetRect(UWModernLayout.ElementEnum.Messages, out Rect lORect))
                fSetScrollWidth(lORect, liExtra, liTo, true);
            else
            {
                UWUserSettings.ModernScrollWidth = Mathf.Clamp(liTo, UWModernScroll.MinExtraWidth, UWModernScroll.MaxExtraWidth);
                fSaveSettings();
            }
        });
    }

    private static InspectorItem fBoxLinesRow(Element peElement)
    {
        int liLines = UWMessageLog.ModernShownCount;

        return fStepper(Defs.ModernMessageLines, liLines, liLines > UWModernScroll.MinLines, liLines < UWMessageLog.HistoryCount, liAt =>
        {
            fStepModernMessageLines(liAt == 0 ? -1 : 1);
            fSaveSettings();
        });
    }

    /// <summary>The message box's fade: Never, then the seconds of UWMessageLogModel.FadeChoices.</summary>
    private static InspectorItem fBoxFadeRow(Element peElement)
    {
        int liFade = UWMessageLogModel.StepFade(UWUserSettings.ModernMessageFade, 0);
        int[] laChoices = UWMessageLogModel.FadeChoices;
        string lsValue = liFade <= 0 ? UWText.T("editor.fade.never") : UWText.F("editor.fade.seconds", liFade);

        return fStepper(Defs.ModernMessageFade.Label, lsValue, liFade > laChoices[0], liFade < laChoices[laChoices.Length - 1], liAt =>
        {
            UWUserSettings.ModernMessageFade = UWMessageLogModel.StepFade(liFade, liAt == 0 ? -1 : 1);
            fSaveSettings();
        });
    }

    /// <summary>A panel moved is a free window; Dock puts it back at its edge.</summary>
    private static InspectorItem fDockRow(Element peElement)
    {
        // Docked with its handle moved along the edge, Dock puts the handle back at its height too.
        bool lbDocked = UWModernLayout.IsDocked(peElement) && !UWModernLayout.IsDockMoved(peElement);

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
        // Built anew when the language changed: some rows take their texts when they are built
        // (translation step 3R, a language switched while the game runs).
        // And when another game runs: rows take the game's words too (the compass's recess, UW2 plan stage U6 step 1).
        UWDataImport.DataImport lOData = UWModernUi.Data;
        object lOGame = lOData != null && lOData.Textures != null ? (object)lOData.Textures.Game : null;

        if (mOOverviewRows == null || miInspectorLanguage != UWText.LanguageVersion || !Equals(mOInspectorGame, lOGame))
        {
            miInspectorLanguage = UWText.LanguageVersion;
            mOInspectorGame = lOGame;
            fBuildInspectorRows();
        }

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
