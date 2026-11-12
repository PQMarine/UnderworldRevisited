using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE HOMING DART OF DEADLY SEEKER (UWHomingDartRules, UW2 plan G1 part C2, 2026-11-12): check_homing's box,
        /// merge_angles, get_theta, the steering, the trail, the hit points and the loner bit.
        /// </summary>
        private static void fCheckHomingDart()
        {
            Check.Group("Homing dart (UWHomingDartRules - Deadly Seeker)");

            // merge_angles: a quarter of the way at 16, the short way round across 0.
            fExpectInt("homing: merge_angles 0 towards 64 by 16/64", UWHomingDartRules.MergeAngles(0, 64, 16), 16);
            fExpectInt("homing: merge_angles 250 towards 10 the short way (across 0)", UWHomingDartRules.MergeAngles(250, 10, 32), 2);
            fExpectInt("homing: merge_angles 10 towards 250 the short way (across 0)", UWHomingDartRules.MergeAngles(10, 250, 32), 2);
            fExpectInt("homing: merge_angles all the way at 64", UWHomingDartRules.MergeAngles(30, 90, 64), 90);

            // get_theta: 0 along +y, 0x40 (high byte) along +x, 0x80 along -y.
            fExpectInt("homing: get_theta north", UWHomingDartRules.GetTheta(100, 100, 100, 120) >> 8, 0);
            fExpectInt("homing: get_theta east", UWHomingDartRules.GetTheta(100, 100, 120, 100) >> 8, 0x40);
            fExpectInt("homing: get_theta south", UWHomingDartRules.GetTheta(100, 100, 100, 80) >> 8, 0x80);
            fExpectInt("homing: get_theta no vector", UWHomingDartRules.GetTheta(5, 5, 5, 5), 0);

            // The box: octant 0 (north) - x 7 to 10, y 7 to 11 around tile 8/8 (process_area's inclusive bounds).
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWMobileRecord lODart = new UWMobileRecord
            {
                Object = new UWObject((ushort)UWHomingDartRules.ItemId), Index = 20, TileX = 8, TileY = 8, XPos = 4, YPos = 4,
                ZPos = 20, Octant = 0, HeadingByte = 0, Owner = 1, Hp = 10, VzField = 16
            };

            lOWorld.AddBody(8, 13, new UWMotionBody { Index = 40, ItemId = 0x44, XPos = 4, YPos = 4, ZPos = 20, IsMobile = true, IsCreature = true });
            fExpectBool("homing: a creature beyond the box is not found",
                UWHomingDartRules.FindTarget(lODart, lOWorld, (8 << 3) + 4, (8 << 3) + 4, out int _, out int _, out int _, out int _), false);

            lOWorld.AddBody(8, 8, new UWMotionBody { Index = 1, ItemId = 0x7F, XPos = 4, YPos = 2, ZPos = 20, IsMobile = true, IsCreature = true });
            lOWorld.AddBody(10, 11, new UWMotionBody { Index = 41, ItemId = 0x44, XPos = 0, YPos = 0, ZPos = 20, IsMobile = true, IsCreature = true });
            lOWorld.AddBody(9, 10, new UWMotionBody { Index = 42, ItemId = 0x44, XPos = 4, YPos = 4, ZPos = 100, IsMobile = true, IsCreature = true });

            bool lbFound = UWHomingDartRules.FindTarget(lODart, lOWorld, (8 << 3) + 4, (8 << 3) + 4, out int liTx, out int liTy, out int liTz, out int liDist);

            fExpectBool("homing: the nearest creature in the box, not the shooter (the player, index 1)",
                lbFound && liTx == (9 << 3) + 4 && liTy == (10 << 3) + 4 && liTz == 100 && liDist == 24, true);

            // One step towards it (north-east of the dart): the heading turns right, the pitch up, maybe a trail.
            UWHomingDartRules.Steer(lODart, lOWorld, lOWorld);

            fExpectBool("homing: steered towards the target in the north-east (heading 0 turns right)",
                lODart.HeadingByte > 0 && lODart.HeadingByte < 0x40, true);
            // 80 above: (16 * 8 + (80 / 24 + 16) * 4) / 12 = 17 - a small height difference does not move it at all.
            fExpectInt("homing: pitched up towards the much higher target", lODart.VzField, 17);

            // The loner bit ends it at once.
            lODart.Loner = true;
            UWHomingDartRules.Steer(lODart, lOWorld, lOWorld);
            fExpectInt("homing: after striking a creature (loner) the dart ends", lODart.Hp, 0);

            // Without a target it weaves and loses a hit point now and then; the trail shows half the time.
            FakeMotionWorld lOEmpty = new FakeMotionWorld();
            UWMobileRecord lOWeaver = new UWMobileRecord
            {
                Object = new UWObject((ushort)UWHomingDartRules.ItemId), Index = 21, TileX = 8, TileY = 8, XPos = 4, YPos = 4,
                ZPos = 20, Octant = 0, HeadingByte = 0x80, Owner = 1, Hp = 40, VzField = 16
            };
            int liMaxTurn = 0;

            for (int liStep = 0; liStep < 40; liStep++)
            {
                int liBefore = lOWeaver.HeadingByte;

                UWHomingDartRules.Steer(lOWeaver, lOEmpty, lOEmpty);
                liMaxTurn = System.Math.Max(liMaxTurn, System.Math.Abs(lOWeaver.HeadingByte - liBefore));
            }

            int liTrails = lOEmpty.Log.FindAll(psLine => psLine.StartsWith("effect " + UWHomingDartRules.TrailEffectId, System.StringComparison.Ordinal)).Count;

            fExpectBool("homing: weaving turns at most 4 a step", liMaxTurn <= 4, true);
            fExpectBool("homing: 40 steps lose some hit points, not all", lOWeaver.Hp < 40 && lOWeaver.Hp > 10, true);
            fExpectBool("homing: the trail shows about half the time", liTrails > 10 && liTrails < 30, true);
        }
    }
}
