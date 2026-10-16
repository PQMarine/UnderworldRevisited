using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Spell rules pinned against the original (reference audit 2026-10-12, spells): how long an
    /// active spell lasts (set_curmagic), each pinned at its lowest and highest roll.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckSpellRules(DataImport pOData)
        {
            Check.Group("Spell rules (reference audit 2026-10-12)");

            fCheckSpellStability();

            // move_along: truncated steps, one added away from zero.
            fExpectMoveAlong("north", 0, 4, 0, 4);
            fExpectMoveAlong("east", 64, 4, 4, 0);
            fExpectMoveAlong("south", 128, 4, 0, -4);
            fExpectMoveAlong("west", 192, 4, -4, 0);
            fExpectMoveAlong("north-east", 32, 4, 3, 3);
            fExpectMoveAlong("a little east of north stays on the axis", 8, 4, 0, 4);
            fExpectMoveAlong("heading 20: two sideways, not one", 20, 4, 2, 4);
            fExpectMoveAlong("Tremor, north-west", 224, 5, -4, 4);

            // build_new_obj: a strike's picture between the floor and the ceiling.
            UWRandom.Use(new FixedRandomSource(false));
            int liLowStrike = UWAreaSpellRules.RollStrikeZPos(0x30);
            UWRandom.Use(new FixedRandomSource(true));
            int liHighStrike = UWAreaSpellRules.RollStrikeZPos(0x30);
            UWRandom.Use(null);
            fExpectInt("strike picture: on the floor at the lowest", liLowStrike, 0x30);
            fExpectInt("strike picture: under the ceiling at the highest", liHighStrike, 0x7F);

            // missile_newhit: the hit's type is the negated signed third byte of the ranged entry.
            fExpectInt("projectile type: fireball", UWRunicMagic.GetProjectileDamageType(0x14, pOData.ObjectProperties), 0x0B);
            fExpectInt("projectile type: lightning bolt", UWRunicMagic.GetProjectileDamageType(0x15, pOData.ObjectProperties), 0x23);
            fExpectInt("projectile type: acid", UWRunicMagic.GetProjectileDamageType(0x16, pOData.ObjectProperties), 0x10);
            fExpectInt("projectile type: magic missile", UWRunicMagic.GetProjectileDamageType(0x17, pOData.ObjectProperties), 0x43);
            fExpectInt("projectile type: an arrow is a missile", UWRunicMagic.GetProjectileDamageType(0x12, pOData.ObjectProperties), UWDamageTypes.Missile);

            // mous_in_3d's target mode: game_sprint(0x5E) for an object out of reach.
            Check.Str("message 95", UWText.General(pOData, UWMiscSpellRules.TargetOutOfReachMessage), "You cannot reach that.");
        }

        private static void fExpectMoveAlong(string psName, int piHeading, int piDistance, int piDx, int piDy)
        {
            UWTilePos lOAt = UWAreaSpellRules.MoveAlong(new UWTilePos(32, 32), piHeading, piDistance);

            fExpectInt("move_along " + psName + ": x", lOAt.X - 32, piDx);
            fExpectInt("move_along " + psName + ": y", lOAt.Y - 32, piDy);
        }

        /// <summary>set_curmagic (PLAYTIME.C): 0x00 2d3, 0x40 2d8 + 6, 0x80 3d20 + 24, 1 gives 1.</summary>
        private static void fCheckSpellStability()
        {
            int[] liClasses = { 0x00, 0x40, 0x80, 0xC0 };
            int[] liLowest = { 2, 8, 27, 1 };
            int[] liHighest = { 6, 22, 84, 1 };

            for (int liAt = 0; liAt < liClasses.Length; liAt++)
            {
                UWRandom.Use(new FixedRandomSource(false));
                int liMin = UWSpellCasting.RollStability(liClasses[liAt]);
                UWRandom.Use(new FixedRandomSource(true));
                int liMax = UWSpellCasting.RollStability(liClasses[liAt]);
                UWRandom.Use(null);

                fExpectInt("stability 0x" + liClasses[liAt].ToString("X2") + ": lowest", liMin, liLowest[liAt]);
                fExpectInt("stability 0x" + liClasses[liAt].ToString("X2") + ": highest", liMax, liHighest[liAt]);
            }
        }
    }
}
