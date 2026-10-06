using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE EASY STEP ON THE MOTION CORE (2026-10-06): UWPlayerMotion.EasyStep with the
    /// original's CheckIfItemFitsInTile in UWMotionCore, on the small map with the real COMOBJ.
    /// Flat floor, wall, one floor step up and two, the ledge refused without W and walked off
    /// with it, water refused from the shore without W and walked in it, the shore from the
    /// water, levitation over the ledge holding its height, a solid object in the way, standing
    /// on a 3D model, the gate while falling.
    /// </summary>
    public static partial class Program
    {
        /// <summary>A fresh map and a player at the centre of tile 8,8 facing +y, the tile ahead
        /// painted as given.</summary>
        private static UWPlayerMotion fEasyPlayer(DataImport pOData, int piAheadType, int piAheadFloor, int piAheadTerrain,
            out FakeMotionWorld pOWorld)
        {
            pOWorld = new FakeMotionWorld();
            pOWorld.Paint(8, 9, 8, 12, piAheadType, piAheadFloor, piAheadTerrain);

            return fMakePlayer(pOWorld, pOData, 8, 8);
        }

        /// <summary>The first object id in the given range that matches, -1 if none.</summary>
        private static int fFindObject(DataImport pOData, int piFrom, int piTo, Func<UWCommonObjectProperties.Entry, bool> pOTest)
        {
            for (int liId = piFrom; liId <= piTo; liId++)
            {
                UWCommonObjectProperties.Entry lOEntry;

                if (pOData.CommonObjectProperties.TryGet(liId, out lOEntry) && pOTest(lOEntry))
                    return liId;
            }

            return -1;
        }

        private static void fCheckEasyStep(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Easy step (seg008_1B2A_216 with CheckIfItemFitsInTile on the core)");

            if (pOData == null || pOData.CommonObjectProperties == null)
            {
                fFail("easy step", "no COMOBJ.DAT");

                return;
            }

            FakeMotionWorld lOWorld;
            UWPlayerMotion lOPlayer;

            // ------------------------------------------------- flat floor
            lOPlayer = fEasyPlayer(pOData, 1, 2, 0, out lOWorld);
            fExpectBool("easy: a step forward on flat floor", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), true);
            fExpectInt("easy: ... half a tile along +y", lOPlayer.Params.Y, (8 << 8) + 0x80 + 0x80);
            fExpectInt("easy: ... nothing across", lOPlayer.Params.X, (8 << 8) + 0x80);
            fExpectInt("easy: ... the height kept", lOPlayer.Params.Z, 0x80);
            fExpectInt("easy: ... into the next tile", lOPlayer.CurrentTileY, 9);
            fExpectBool("easy: a step back", lOPlayer.EasyStep(UWEasyMovement.CommandStepBack, 0), true);
            fExpectInt("easy: ... a quarter tile", lOPlayer.Params.Y, (8 << 8) + 0x80 + 0x40);

            // ------------------------------------------------- wall and steps
            lOPlayer = fEasyPlayer(pOData, 0, 2, 0, out lOWorld);
            fExpectBool("easy: a wall ahead refuses the step", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), false);
            fExpectInt("easy: ... the player stays", lOPlayer.Params.Y, (8 << 8) + 0x80);

            lOPlayer = fEasyPlayer(pOData, 1, 3, 0, out lOWorld);
            fExpectBool("easy: one floor step up is taken", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), true);
            fExpectInt("easy: ... standing on it at once", lOPlayer.Params.Z, 3 * 8 * 8);

            lOPlayer = fEasyPlayer(pOData, 1, 4, 0, out lOWorld);
            fExpectBool("easy: two floor steps up refuse", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), false);

            // ------------------------------------------------- the ledge
            lOPlayer = fEasyPlayer(pOData, 1, 0, 0, out lOWorld);
            fExpectBool("easy: the ledge refuses a plain step", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), false);
            fExpectBool("easy: ... W steps off it", lOPlayer.EasyStep(UWEasyMovement.CommandRunForward, 0), true);
            fExpectInt("easy: ... and the fall begins", lOPlayer.Params.Gravity, -4);
            fExpectBool("easy: ... no step while falling", lOPlayer.EasyStep(UWEasyMovement.CommandRunForward, 0), false);

            int liMaxZ;
            bool lbSound;
            int liDamage;

            // Half a tile on, the box still leans two eighths over the edge and stands on it.
            fExpectBool("easy: ... settling", fRunUntilSettled(lOPlayer, 200, out liMaxZ, out lbSound, out liDamage) > 0, true);
            fExpectInt("easy: ... still on the edge with the back of the box", lOPlayer.Params.Z, 0x80);
            fExpectBool("easy: ... the next W step", lOPlayer.EasyStep(UWEasyMovement.CommandRunForward, 0), true);
            fExpectBool("easy: ... landing below", fRunUntilSettled(lOPlayer, 200, out liMaxZ, out lbSound, out liDamage) > 0, true);
            fExpectInt("easy: ... on floor 0", lOPlayer.Params.Z, 0);

            // ------------------------------------------------- water
            lOPlayer = fEasyPlayer(pOData, 1, 2, 1, out lOWorld);
            fExpectBool("easy: a plain step from the shore into water refuses", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), false);
            fExpectBool("easy: ... W goes in", lOPlayer.EasyStep(UWEasyMovement.CommandRunForward, 0), true);
            fExpectInt("easy: ... swimming", lOPlayer.Params.Contact, UWMotionTables.ContactWater);
            fExpectInt("easy: ... the tile state is water", lOPlayer.PreviousTileState, UWMotionTables.ContactWater);
            fExpectBool("easy: a plain step on in the water", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), true);
            lOPlayer.EasyStep(UWEasyMovement.CommandStepBack, 0);
            lOPlayer.EasyStep(UWEasyMovement.CommandStepBack, 0);
            fExpectBool("easy: a plain step back onto the shore", lOPlayer.EasyStep(UWEasyMovement.CommandStepBack, 0), true);
            fExpectInt("easy: ... on the ground again", lOPlayer.PreviousTileState, UWMotionTables.ContactGround);

            // ------------------------------------------------- levitation over the ledge
            lOPlayer = fEasyPlayer(pOData, 1, 0, 0, out lOWorld);
            lOPlayer.Abilities = UWPlayerMotion.AbilityLevitate;
            fExpectBool("easy: levitating, a plain step over the ledge", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), true);
            fExpectBool("easy: ... and another out over the drop", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), true);
            fExpectInt("easy: ... the height held", lOPlayer.Params.Z, 0x80);
            fExpectInt("easy: ... no gravity", lOPlayer.Params.Gravity, 0);
            fExpectInt("easy: ... in the air", lOPlayer.Params.Contact, UWMotionTables.ContactAirborne);

            // ------------------------------------------------- objects
            int liBlock = fFindObject(pOData, 0x140, 0x1BF, e => e.IsSolid && !e.Is3DModel && e.Height > 8 && (e.Radius & 7) > 0 && (e.Radius & 7) < 4);

            if (liBlock < 0)
                fFail("easy: a solid object that is no 3D model", "none in COMOBJ 0x140-0x1BF");
            else
            {
                lOPlayer = fEasyPlayer(pOData, 1, 2, 0, out lOWorld);
                lOWorld.AddBody(8, 9, new UWMotionBody { Index = 0x200, ItemId = liBlock, XPos = 4, YPos = 1, ZPos = 16 });
                fExpectBool("easy: a solid object in the way refuses (0x" + liBlock.ToString("X") + ")",
                    lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), false);
            }

            int liTable = fFindObject(pOData, 0x140, 0x1BF, e => e.IsSolid && e.Is3DModel && e.Height >= 8 && e.Height <= 16 && (e.Radius & 7) > 0 && (e.Radius & 7) < 4);

            if (liTable < 0)
                fFail("easy: a 3D model to stand on", "none in COMOBJ 0x140-0x1BF with a height of 8 to 16");
            else
            {
                UWCommonObjectProperties.Entry lOTable;

                // Floating at 16 over a floor of 0, so that no ledge corner is higher than the top.
                pOData.CommonObjectProperties.TryGet(liTable, out lOTable);
                lOWorld = new FakeMotionWorld();
                lOWorld.Paint(1, 1, 30, 30, 1, 0, 0);
                lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);
                lOPlayer.Abilities = UWPlayerMotion.AbilityLevitate;
                lOPlayer.Params.Z = 0x80;
                lOWorld.AddBody(8, 9, new UWMotionBody { Index = 0x200, ItemId = liTable, XPos = 4, YPos = 1, ZPos = 0 });
                fExpectBool("easy: a step onto a 3D model below the feet (0x" + liTable.ToString("X") + ")",
                    lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), true);
                fExpectInt("easy: ... standing on its top", lOPlayer.Params.Z, lOTable.Height * 8);
                fExpectInt("easy: ... as on the ground", lOPlayer.Params.Contact, UWMotionTables.ContactGround);
            }

            // ------------------------------------------------- the gate
            lOPlayer = fEasyPlayer(pOData, 1, 2, 0, out lOWorld);
            lOPlayer.Params.Speed = lOPlayer.MotionWeight;
            fExpectBool("easy: no step at the motion weight's speed", lOPlayer.MayEasyMove, false);
            lOPlayer.Params.Speed = lOPlayer.MotionWeight - 1;
            fExpectBool("easy: ... but just below it", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), true);
            fExpectInt("easy: ... and the step clears the speed", lOPlayer.Params.Speed, 0);
        }
    }
}
