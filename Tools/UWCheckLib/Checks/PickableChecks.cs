using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// WHAT THE HAND TAKES (2026-11-16): pick_3d in both games (ui/INTERACT.C) sets releaseable = co->pickable &amp;&amp;
    /// !IsMobElem(obj), and struct ComObj's "pickable" is byte 7 bit 5 (UWCommonObjectProperties.Entry.Pickable), not byte
    /// 3 bit 5 ("pickup", Entry.CanBePickedUp). The first game's hand-checked scenery list, which the bit replaced, is
    /// held against it; the second game's objects the user checked in the original too.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckPickable(CheckContext pOContext)
        {
            Check.Group("What the hand takes: COMOBJ's pickable bit (byte 7 bit 5)");

            if (pOContext.DataPath != null)
            {
                DataImport lOUw1 = new DataImport(pOContext.DataPath, null, null, UWGameProfiles.Uw1);

                // The former UWFixedScenery: plant 0xC0, grass, debris 0xD0-0xD2, stalactite, anvil, rubble, chips, bones, the
                // blood stains, fountain, cauldron, urn, orb, campfire - not taken; a coin, a bottle, the plant 0xCE are.
                Check.Str("pickable: the first game's hand-checked scenery is not taken, its loose things are",
                    fPickableList(lOUw1, 0xC0, 0xC1, 0xD0, 0xD1, 0xD2, 0xD3, 0xD7, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0x12E, 0x12F, 0x8C, 0x117, 0x12A)
                    + " | " + fPickableList(lOUw1, 0xA0, 0xCE),
                    "000000000000000000 | 11");
            }

            if (pOContext.Uw2DataPath != null)
            {
                DataImport lOUw2 = new DataImport(pOContext.Uw2DataPath, null, null, UWGameProfiles.Uw2);

                // Per user on the original: the castle's plant 0xD9, the fountain 0x12E, the campfire 0x12A and the shelf 0x169 are
                // not taken; the key ring, coins, bottles and potions are.
                Check.Str("pickable: the second game's plant, fountain, campfire and shelf are not taken; key ring, coin, bottle, red potion are",
                    fPickableList(lOUw2, 0xD9, 0x12E, 0x12A, 0x169) + " | " + fPickableList(lOUw2, 0x8C, 0xA0, 0x13D, 0xE5),
                    "0000 | 1111");
            }
        }

        private static string fPickableList(DataImport pOData, params int[] paIds)
        {
            List<char> lOBits = new List<char>();

            foreach (int liId in paIds)
                lOBits.Add(pOData.CommonObjectProperties.TryGet(liId, out UWCommonObjectProperties.Entry lOEntry) && lOEntry.Pickable ? '1' : '0');

            return new string(lOBits.ToArray());
        }
    }
}
