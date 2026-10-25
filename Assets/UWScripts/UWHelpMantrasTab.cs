using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>The help's MANTRAS tab: the mantras the manual names with the skills they draw from,
/// and folded away the others (one skill each, the two quest mantras). Moved out of
/// UWHelpWindow (cleanup 8.6, 2026-10-24).</summary>
public sealed class UWHelpMantrasTab : UWHelpTab
{
    private bool mbShowMoreMantras;

    public override UWHelpTabId Id => UWHelpTabId.Mantras;

    public override string TitleId => "help.tab.mantras";

    /// <summary>The list in either form (IUWHelpFlow, cleanup 8b.3, 2026-10-24).</summary>
    protected override void OnFlow(IUWHelpFlow pOFlow)
    {
        DataImport lOData = fData();

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
