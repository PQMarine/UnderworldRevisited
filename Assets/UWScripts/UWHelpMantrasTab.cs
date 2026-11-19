using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>The help's MANTRAS tab: the mantras the manual names with the skills they draw from, and
/// below them either those the character has LEARNED in the game with where from, the pieces of INSAHN
/// in the order seen and how many are still to find (UWKnowledge, GitHub issue 19, 2026-11-08) - or,
/// with the Gameplay window's "Show every mantra and spell", folded away all the others (one skill
/// each, the two quest mantras), as before. Moved out of UWHelpWindow (cleanup 8.6, 2026-10-24).</summary>
public sealed class UWHelpMantrasTab : UWHelpTab
{
    private bool mbShowMoreMantras;

    public override UWHelpTabId Id => UWHelpTabId.Mantras;

    public override string TitleId => fIsSecondGame ? "help.tab.teachers" : "help.tab.mantras";

    /// <summary>The second game has no shrines: the tab shows its teachers instead (fDrawTeachers).</summary>
    private static bool fIsSecondGame => UWGameSession.Current != null && UWGameSession.Current.PlayerFileUw2 != null;

    /// <summary>The list in either form (IUWHelpFlow, cleanup 8b.3, 2026-10-24).</summary>
    protected override void OnFlow(IUWHelpFlow pOFlow)
    {
        DataImport lOData = fData();

        if (fIsSecondGame)
        {
            fDrawTeachers(pOFlow, lOData);

            return;
        }

        pOFlow.Line(UWText.T("help.mantras.title"), UWHelpLookEnum.Heading);
        pOFlow.Line(UWText.T("help.mantras.intro"), UWHelpLookEnum.Dim);

        pOFlow.Space(10f);

        foreach (KeyValuePair<int, string> lOMantra in UWHelpContent.ManualMantras)
        {
            pOFlow.Line("<b>" + UWShrineRules.GetMantra(lOMantra.Key, lOData) + "</b>", UWHelpLookEnum.Text);
            pOFlow.Line(UWText.F("help.mantras.group", UWText.T(lOMantra.Value), fGroupSkills(lOMantra.Key, lOData)), UWHelpLookEnum.Dim);
            pOFlow.Space(EntrySpacing);
        }

        pOFlow.Space(16f);

        if (!UWUserSettings.KnowledgeShowAll)
        {
            fDrawLearned(pOFlow, lOData);
            fDrawLizardWords(pOFlow, false);

            return;
        }

        fDrawLizardWords(pOFlow, true);
        pOFlow.Space(10f);

        if (pOFlow.Fold(mbShowMoreMantras ? UWText.T("help.mantras.hide_more")
                : UWText.T("help.mantras.show_more")))
            mbShowMoreMantras = !mbShowMoreMantras;

        if (!mbShowMoreMantras)
            return;

        pOFlow.Space(10f);
        pOFlow.Line(UWText.T("help.mantras.single"), UWHelpLookEnum.SubHeading);

        for (int liMantra = 0; liMantra < UWShrineRules.FirstSpecialMantra; liMantra++)
            pOFlow.Line(string.Format("<b>{0}</b>   <color=#{1}>{2}</color>",
                UWShrineRules.GetMantra(liMantra, lOData), UWGuiTheme.AccentHex, UWShrineRules.GetSkillName(liMantra, lOData)), UWHelpLookEnum.Text);

        pOFlow.Space(10f);
        pOFlow.Line(UWText.T("help.mantras.quests"), UWHelpLookEnum.SubHeading);

        pOFlow.Line("<b>" + UWShrineRules.GetMantra(UWShrineRules.CupOfWonderMantra, lOData) + "</b>", UWHelpLookEnum.Text);
        pOFlow.Line(UWText.T(UWHelpContent.CupOfWonderNote), UWHelpLookEnum.Dim);
        pOFlow.Space(EntrySpacing);
        pOFlow.Line("<b>" + UWShrineRules.GetMantra(UWShrineRules.KeyOfTruthMantra, lOData) + "</b>", UWHelpLookEnum.Text);
        pOFlow.Line(UWText.T(UWHelpContent.KeyOfTruthNote), UWHelpLookEnum.Dim);
    }

    /// <summary>The mantras the character has learned beyond the manual's, how many are still to find, and
    /// the pieces of INSAHN seen so far - only the letters, in the order seen (per user: the order is the
    /// player's to find).</summary>
    private static void fDrawLearned(IUWHelpFlow pOFlow, DataImport pOData)
    {
        UWKnowledge lOKnown = UWKnowledge.Current;
        int liLearned = 0;
        int liHidden = 0;

        pOFlow.Line(UWText.T("help.knowledge.learned"), UWHelpLookEnum.SubHeading);

        for (int liMantra = 0; liMantra < UWShrineRules.FirstSpecialMantra; liMantra++)
        {
            liHidden++;

            if (!lOKnown.KnowsMantra(liMantra, out UWKnowledge.Entry lOEntry))
                continue;

            liLearned++;
            pOFlow.Line(string.Format("<b>{0}</b>   <color=#{1}>{2}</color>",
                UWShrineRules.GetMantra(liMantra, pOData), UWGuiTheme.AccentHex, UWShrineRules.GetSkillName(liMantra, pOData)), UWHelpLookEnum.Text);
            pOFlow.Line(UWHelpKnowledge.SourceText(lOEntry), UWHelpLookEnum.Dim);
            pOFlow.Space(EntrySpacing);
        }

        liHidden += 2;

        if (lOKnown.KnowsMantra(UWShrineRules.CupOfWonderMantra, out UWKnowledge.Entry lOCup))
        {
            liLearned++;
            pOFlow.Line("<b>" + UWShrineRules.GetMantra(UWShrineRules.CupOfWonderMantra, pOData) + "</b>", UWHelpLookEnum.Text);
            // Vaguer than the manual's note in the learned list (per user 2026-11-08).
            pOFlow.Line(UWText.T("help.knowledge.insahn"), UWHelpLookEnum.Dim);
            pOFlow.Line(UWHelpKnowledge.SourceText(lOCup), UWHelpLookEnum.Dim);
            pOFlow.Space(EntrySpacing);
        }
        else if (lOKnown.Pieces().Count > 0)
        {
            pOFlow.Line("<b>" + string.Join("  ", lOKnown.Pieces()) + "</b>", UWHelpLookEnum.Text);
            pOFlow.Line(UWText.T("help.mantras.pieces"), UWHelpLookEnum.Dim);
            pOFlow.Space(EntrySpacing);
        }

        // FANLO (per user 2026-11-08): until it was prayed at a shrine it stands without its name - the book's
        // title as the entry and a hint - and does not count as learned yet (as INSAHN's pieces); prayed, it
        // has its name and says what it does.
        bool lbFanloClue = false;

        if (lOKnown.KnowsMantra(UWShrineRules.KeyOfTruthMantra, out UWKnowledge.Entry lOKey))
        {
            if (lOKnown.ChantedFanlo)
            {
                liLearned++;
                pOFlow.Line("<b>" + UWShrineRules.GetMantra(UWShrineRules.KeyOfTruthMantra, pOData) + "</b>", UWHelpLookEnum.Text);
                pOFlow.Line(UWText.T(UWHelpContent.KeyOfTruthNote), UWHelpLookEnum.Dim);
            }
            else
            {
                lbFanloClue = true;

                if (pOData != null)
                    pOFlow.Line("<i>" + pOData.GetBookText(UWKnowledgeSources.FanloBook) + "</i>", UWHelpLookEnum.Text);

                pOFlow.Line(UWText.T("help.knowledge.fanlo"), UWHelpLookEnum.Dim);
            }

            pOFlow.Line(UWHelpKnowledge.SourceText(lOKey), UWHelpLookEnum.Dim);
            pOFlow.Space(EntrySpacing);
        }

        if (liLearned == 0 && !lbFanloClue && lOKnown.Pieces().Count == 0)
            pOFlow.Line(UWText.T("help.knowledge.none_yet"), UWHelpLookEnum.Dim);

        if (liLearned < liHidden)
            pOFlow.Line(UWText.F("help.knowledge.to_find", liHidden - liLearned), UWHelpLookEnum.Dim);
    }

    /// <summary>
    /// THE SECOND GAME'S TEACHERS (2026-11-18, per user: the Mantras tab meant nothing there, its skills are trained by
    /// people): every conversation that can advance a skill for a skill point (UWTrainersUw2, read from the scripts),
    /// shown once met - its name as the conversation showed it, the world and level it was held on - or every one with
    /// "Show every mantra and spell". Above them the skill points to spend. What a teacher wants first is the script's
    /// own and not listed.
    /// </summary>
    private void fDrawTeachers(IUWHelpFlow pOFlow, DataImport pOData)
    {
        pOFlow.Line(UWText.T("help.teachers.title"), UWHelpLookEnum.Heading);
        pOFlow.Line(UWText.T("help.teachers.intro"), UWHelpLookEnum.Dim);

        UWPlayerFile lOFile = UWGameSession.Current.PlayerFileUw2;

        pOFlow.Space(10f);
        pOFlow.Line(UWText.F("help.teachers.points", UWPlayerRecordUw2.SkillPoints(lOFile)), UWHelpLookEnum.Text);
        pOFlow.Space(16f);

        Dictionary<int, List<int>> lOAll = UWTrainersUw2.All(pOData != null ? pOData.Conversations : null);
        UWKnowledge lOKnown = UWKnowledge.Current;
        bool lbAll = UWUserSettings.KnowledgeShowAll;
        List<KeyValuePair<int, UWKnowledge.Met>> lOMet = new List<KeyValuePair<int, UWKnowledge.Met>>();
        List<int> lOUnmet = new List<int>();

        foreach (int liSlot in lOAll.Keys)
        {
            if (lOKnown.HasMetTeacher(liSlot, out UWKnowledge.Met lOWhere))
                lOMet.Add(new KeyValuePair<int, UWKnowledge.Met>(liSlot, lOWhere));
            else
                lOUnmet.Add(liSlot);
        }

        // By the place they were met, the first world's first.
        lOMet.Sort((pOA, pOB) => pOA.Value.Level != pOB.Value.Level ? pOA.Value.Level.CompareTo(pOB.Value.Level) : pOA.Key.CompareTo(pOB.Key));
        lOUnmet.Sort();

        foreach (KeyValuePair<int, UWKnowledge.Met> lOEntry in lOMet)
            fDrawTeacher(pOFlow, pOData, lOAll[lOEntry.Key], string.IsNullOrEmpty(lOEntry.Value.Who) ? UWText.T("help.teachers.unknown") : lOEntry.Value.Who,
                UWText.F("help.teachers.met", fPlace(lOEntry.Value.Level)));

        if (lbAll)
        {
            foreach (int liSlot in lOUnmet)
            {
                string lsName = UWTrainersUw2.NameOf(pOData, liSlot);

                fDrawTeacher(pOFlow, pOData, lOAll[liSlot], lsName.Length > 0 ? lsName : UWText.T("help.teachers.unknown"), UWText.T("help.teachers.not_met"));
            }

            return;
        }

        if (lOMet.Count == 0)
            pOFlow.Line(UWText.T("help.knowledge.none_yet"), UWHelpLookEnum.Dim);

        if (lOUnmet.Count > 0)
            pOFlow.Line(UWText.F("help.teachers.to_find", lOUnmet.Count), UWHelpLookEnum.Dim);
    }

    private static void fDrawTeacher(IUWHelpFlow pOFlow, DataImport pOData, List<int> pOSkills, string psName, string psWhere)
    {
        List<string> lONames = new List<string>();

        foreach (int liSkill in pOSkills)
            lONames.Add(UWCharacterText.SkillName(pOData != null ? pOData.Strings : null, liSkill).Trim());

        pOFlow.Line("<b>" + UWText.Capitalised(psName) + "</b>", UWHelpLookEnum.Text);
        pOFlow.Line(string.Format("<color=#{0}>{1}</color>", UWGuiTheme.AccentHex, string.Join(", ", lONames)), UWHelpLookEnum.Text);
        pOFlow.Line(psWhere, UWHelpLookEnum.Dim);
        pOFlow.Space(EntrySpacing);
    }

    /// <summary>The world and its level a dungeon level (1-based) names: eight levels to a world (LEVELS_PER_WORLD).</summary>
    private static string fPlace(int piLevel)
    {
        if (piLevel < 1)
            return UWText.T("help.teachers.unknown_place");

        int liWorld = (piLevel - 1) / LevelsPerWorld;

        return liWorld < WorldCount
            ? UWText.F("help.teachers.place", UWText.T(WorldIds[liWorld]), ((piLevel - 1) % LevelsPerWorld) + 1)
            : UWText.F("help.teachers.level", piLevel);
    }

    /// <summary>Eight levels to a world (LEVELS_PER_WORLD), nine worlds with names (help.world.0 to 8).</summary>
    private const int LevelsPerWorld = 8;

    private static readonly string[] WorldIds = { "help.world.0", "help.world.1", "help.world.2", "help.world.3", "help.world.4",
        "help.world.5", "help.world.6", "help.world.7", "help.world.8" };

    private static int WorldCount => WorldIds.Length;

    /// <summary>
    /// THE LIZARD WORDS (GitHub issue 20, 2026-11-08): those Murgo has mimed with their meanings, or every one with
    /// "Show every mantra and spell" - the first game's only (UWLizardWords). The words are what the setting
    /// "Lizard words" shows in the lizardmen's conversations.
    /// </summary>
    private void fDrawLizardWords(IUWHelpFlow pOFlow, bool pbAll)
    {
        if (UWGameSession.Current != null && UWGameSession.Current.PlayerFileUw2 != null)
            return;

        // Only with Learned (per user, 2026-11-08): Off plays as the original, All shows every meaning in
        // the conversations anyway.
        if (UWLizardWords.Mode != UWLizardWordsEnum.Learned)
            return;

        UWKnowledge lOKnown = UWKnowledge.Current;
        bool lbAny = false;

        pOFlow.Space(16f);
        pOFlow.Line(UWText.T("help.lizard.title"), UWHelpLookEnum.SubHeading);

        foreach (UWLizardWords.Word lOWord in UWLizardWords.Words)
        {
            bool lbKnown = lOKnown.HasFlag(lOWord.Flag);

            if (!pbAll && !lbKnown)
                continue;

            lbAny = true;
            pOFlow.Line(string.Format("<b>{0}</b>   <color=#{1}>{2}</color>", lOWord.Text, UWGuiTheme.AccentHex, UWText.T(lOWord.TextId)),
                UWHelpLookEnum.Text);
        }

        if (!lbAny)
            pOFlow.Line(UWText.T("help.lizard.none_yet"), UWHelpLookEnum.Dim);
        else if (!pbAll)
            pOFlow.Line(UWText.T("help.lizard.learned_from"), UWHelpLookEnum.Dim);
    }

    /// <summary>"Attack, Defense, ..." - the skills a group mantra draws from.</summary>
    private static string fGroupSkills(int piMantra, DataImport pOData)
    {
        if (!UWShrineRules.TryGetGroup(piMantra, out int liFirst, out int liCount, out int _))
            return string.Empty;

        List<string> lONames = new List<string>();

        for (int liSkill = liFirst; liSkill < liFirst + liCount; liSkill++)
            lONames.Add(UWShrineRules.GetSkillName(liSkill, pOData).Trim());

        return string.Join(", ", lONames);
    }
}
