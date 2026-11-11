using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// WHERE EFFECT PICTURES STAND (2026-11-11, per user with screenshots of Mass Confusion beside the original):
        /// put_effect's height on a creature, and the fixed hot spot every object picture hangs on.
        /// </summary>
        private static void fCheckEffectPlacement()
        {
            Check.Group("Effect placement (put_effect, the sprite hot spot)");

            // A creature of COMOBJ height 40 at zpos 16: 40 >> 3 = 5, seven of those above the feet.
            fExpectInt("effect: a spell's sparkle at seven eighths of the height", UWAreaSpellRules.CreatureEffectZPos(16, 40, 7), 16 + 35);
            fExpectInt("effect: a wound's splash at two eighths", UWAreaSpellRules.CreatureEffectZPos(16, 40, UWAreaSpellRules.WoundSplashEighths), 16 + 10);
            fExpectInt("effect: a height below eight counts as one eighth", UWAreaSpellRules.CreatureEffectZPos(16, 4, 7), 16 + 7);
            fExpectInt("effect: SET_Z keeps seven bits", UWAreaSpellRules.CreatureEffectZPos(120, 40, 7), (120 + 35) & 0x7F);

            // UW.EXE and UW2.EXE: x 8, y 16 in every object record.
            fExpectInt("effect: the hot spot across (both EXEs)", UWSpriteScale.AnchorX, 8);
            fExpectInt("effect: the hot spot up (both EXEs)", UWSpriteScale.AnchorY, 16);
        }
    }
}
