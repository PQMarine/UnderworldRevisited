using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// MODERN'S PLACE AS THE ORIGINAL ACCEPTS IT (UWPlayerMotion.OriginalSpot, per user 2026-11-11): the save of
        /// gog:SAVE3 put the player at fine 66/25 of a tile whose south neighbour is a diagonal open to the
        /// north-east and whose west neighbour is rock. The original's box of radius 2 reaches into the diagonal's
        /// closed half there and the player was stuck; Modern's round body was not.
        /// </summary>
        private static void fCheckOriginalSpot(DataImport pOData)
        {
            Check.Group("Modern's place as the original accepts it (UWPlayerMotion.OriginalSpot)");

            FakeMotionWorld lOWorld = new FakeMotionWorld();

            // The corner of level 10 at 29/30, moved to 10/10: rock to the west, the diagonal (type 4) to the south.
            lOWorld.Type[9, 10] = 0;
            lOWorld.Type[10, 9] = 4;

            UWPlayerMotion lOPlayer = fMakeModernPlayer(lOWorld, pOData, 10, 10);
            int liZ = lOPlayer.Params.Z;

            lOPlayer.PlaceAt((10 << 8) | 66, (10 << 8) | 25, liZ, 0);

            bool lbFound = lOPlayer.OriginalSpot(out int liX, out int liY, out int liSpotZ);
            int liDx = ((liX & 0xFF) - 66) / 0x20;
            int liDy = ((liY & 0xFF) - 25) / 0x20;

            fExpectBool("original spot: the saved corner does not pass, a place is found", lbFound && (liX != ((10 << 8) | 66) || liY != ((10 << 8) | 25)), true);
            fExpectBool("original spot: the shift is whole eighths, at most two, in the same tile",
                (liX >> 8) == 10 && (liY >> 8) == 10 && ((liX & 0xFF) - 66) % 0x20 == 0 && ((liY & 0xFF) - 25) % 0x20 == 0
                && (liDx * liDx) + (liDy * liDy) <= 4, true);
            fExpectBool("original spot: the height stays on this floor", liSpotZ == liZ, true);

            lOPlayer.PlaceAt(liX, liY, liSpotZ, 0);
            lOPlayer.OriginalSpot(out int liAgainX, out int liAgainY, out int _);

            fExpectBool("original spot: the place found passes itself", liAgainX == liX && liAgainY == liY, true);

            lOPlayer.PlaceAt((10 << 8) | 0x80, (10 << 8) | 0x80, liZ, 0);
            lOPlayer.OriginalSpot(out int liCentreX, out int liCentreY, out int _);

            fExpectBool("original spot: the middle of the tile stays", liCentreX == ((10 << 8) | 0x80) && liCentreY == ((10 << 8) | 0x80), true);

            lOPlayer.PlaceAt((10 << 8) | 66, (10 << 8) | 25, liZ, 0);

            fExpectBool("original spot: leaving Modern moves the player there", lOPlayer.MoveToOriginalSpot()
                && (lOPlayer.Params.X & 0xFFFF) == liX && (lOPlayer.Params.Y & 0xFFFF) == liY, true);
        }
    }
}
