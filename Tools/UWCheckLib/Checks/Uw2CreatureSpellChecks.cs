using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// A CREATURE'S SNOWBALL AND ACID (per user 2026-11-22, SAVE3 on level 26: a yeti went through its throw and no
        /// snowball came). The yeti 0x52 casts spell 73 from both slots, class 13 minor 9; special_spells hands it to
        /// do_spell(5, 6) for any caster (uw2 SPELLS.C:1006-1010) - the snowball 0x1C. Until then our class 13 tested for
        /// the player first and the creature's cast did nothing.
        /// </summary>
        private static void fCheckUw2CreatureSpells(CheckContext pOContext)
        {
            Check.Group("A creature's snowball and acid (UW2)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("creature spells", "no second game here: skipped");

                return;
            }

            DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);
            UWGameSession lOBefore = UWGameSession.Current;

            UWGameSession.MakeCurrent(lOImport.Session);

            try
            {
                lOImport.ObjectClassProperties.TryGetCritter(0x52, out UWObjectClassProperties.Critter lOYeti);
                bool lbPacked = lOImport.Session.Game.Magic.TryUnpackSpell(73, out UWRunicMagic.Spell lOSpell);

                Check.Str("creature spells: the yeti's spells, and 73 unpacked", lOYeti.SpellOfSlot(1) + " " + lOYeti.SpellOfSlot(2) + " - "
                    + (lbPacked ? "class " + lOSpell.MajorClass + " minor " + lOSpell.MinorClass : "not packed"), "73 73 - class 13 minor 9");

                // The yeti's cast as critter_ai makes it: cast(73) through the spell number.
                FakeWorld lOYetiWorld = new FakeWorld();
                bool lbYeti = UWSpellCasting.CastSpell(73, new FakeSpellHost(lOYetiWorld, UWCastContext.ThePlayer) { HasPlayer = false, Data = lOImport });

                Check.Bool("creature spells: the yeti's spell 73 throws a snowball (" + string.Join(", ", lOYetiWorld.Log) + ")",
                    lbYeti && lOYetiWorld.Log.Contains("projectile 0x1C"), true);

                FakeWorld lOSlugWorld = new FakeWorld();
                bool lbSlug = UWSpellCasting.CastSpell(72, new FakeSpellHost(lOSlugWorld, UWCastContext.ThePlayer) { HasPlayer = false, Data = lOImport });

                Check.Bool("creature spells: the acid slug's spell 72 spits acid (" + string.Join(", ", lOSlugWorld.Log) + ")",
                    lbSlug && lOSlugWorld.Log.Contains("projectile 0x16"), true);

                foreach (bool lbPlayer in new[] { false, true })
                {
                    foreach (int liMinor in new[] { UWSpecialSpellRulesUw2.SnowballSpell, UWSpecialSpellRulesUw2.AcidSpell })
                    {
                        FakeWorld lOWorld = new FakeWorld();
                        FakeSpellHost lOHost = new FakeSpellHost(lOWorld, UWCastContext.ThePlayer) { HasPlayer = lbPlayer, Data = lOImport };
                        bool lbTook = UWSpecialSpellRulesUw2.Cast(liMinor, lOHost);
                        string lsWho = lbPlayer ? "the player's" : "a creature's";
                        string lsWhat = liMinor == UWSpecialSpellRulesUw2.SnowballSpell ? "snowball" : "acid";

                        Check.Bool("creature spells: " + lsWho + " " + lsWhat + " flies (" + string.Join(", ", lOWorld.Log) + ")",
                            lbTook && lOWorld.Log.Contains(liMinor == UWSpecialSpellRulesUw2.SnowballSpell ? "projectile 0x1C" : "projectile 0x16"), true);
                    }
                }
            }
            finally
            {
                UWGameSession.MakeCurrent(lOBefore);
            }
        }
    }
}
