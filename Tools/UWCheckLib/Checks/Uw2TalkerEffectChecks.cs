using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S CONVERSATION EFFECTS ON THE TALKER (G5 part 2, 2026-11-16): babl_hack 5's loner bit, set_sequence,
    /// transform_creature and teleport_critter (uw2 conv/BABLHACK.C, event/WORLDEV.C:1775, critter/CRITTIME.C:374). The
    /// rules know no game, so they run on the first level's creatures here, which are put back afterwards.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2TalkerEffects(CheckContext pOContext)
        {
            Check.Group("UW2 conversation effects on the talker: the loner bit, set_sequence, transform, teleport");

            DataImport lOData = pOContext.Data;
            UWLevel lOLevel = pOContext.Level(0);
            List<UWNpc> lOCritters = lOLevel != null && lOLevel.ActiveMobiles != null ? fActiveCritters(lOLevel) : new List<UWNpc>();

            if (lOCritters.Count == 0)
            {
                Check.Fail("talker effects", "no creature on the level");

                return;
            }

            UWNpc lONpc = lOCritters[0];
            byte[] lyRaw = (byte[])lONpc.RawNpcBytes.Clone();
            ushort liId = lONpc.ID;
            byte liWhoAmI = lONpc.NPCwhoami;
            int liWho = new UWCritterRecord(lONpc).WhoAmI;

            try
            {
                UWCritterRecord lORecord = new UWCritterRecord(lONpc);

                // The loner bit and the sequence for the first creature with that whoami; none for an unknown whoami.
                lORecord.AttitudeLocked = false;
                bool lbLoner = UWConversationRulesUw2.MakeLoner(lOLevel, liWho);
                bool lbSequence = UWConversationRulesUw2.SetSequence(lOLevel, liWho, 7, 0x0B);
                Check.Str("babl_hack 5 and set_sequence: the loner bit, sequence 7 at frame 11 & 7, nothing for whoami 250",
                    lbLoner + ":" + lORecord.AttitudeLocked + " " + lbSequence + ":" + lORecord.RawSequence + "/" + lORecord.Frame + " "
                    + UWConversationRulesUw2.MakeLoner(lOLevel, 250), "True:True True:7/3 False");

                // transform_creature: creature 8 (0x48), whoami 27, powerful, terrain 5; the height kept while can_place refuses.
                FakeWorld lOWorld = new FakeWorld { Level = lOLevel, CreaturesFit = false };
                int liZ = lORecord.ZPos;
                UWConversationRulesUw2.TransformCreature(lONpc, 8, 27, 1, 5, lOData.ObjectClassProperties, lOWorld);
                Check.Str("transform_talker: id 0x48, whoami 27 in the field and the record, powerful, terrain 5, the height kept",
                    "0x" + lONpc.ID.ToString("X2") + " " + lONpc.NPCwhoami + "/" + lORecord.WhoAmI + " " + lORecord.IsStrong + " " + lORecord.TileState
                    + " " + (lORecord.ZPos == liZ), "0x48 27/27 True 5 True");

                // -1 keeps each value.
                UWConversationRulesUw2.TransformCreature(lONpc, -1, -1, -1, -1, lOData.ObjectClassProperties, lOWorld);
                Check.Str("transform_talker: -1 keeps the kind, the whoami, the powerful bit and the terrain",
                    "0x" + lONpc.ID.ToString("X2") + " " + lONpc.NPCwhoami + " " + lORecord.IsStrong + " " + lORecord.TileState, "0x48 27 True 5");

                // teleport_critter: its own tile asks nothing; an open tile 4/4 on the floor, a flier half way to 0x80; a solid
                // tile or no room refuses.
                UWTilePos lOOpen = fOpenTileNear(lOLevel, lORecord.TileX, lORecord.TileY);
                UWTilePos lOSolid = fSolidTileNear(lOLevel, lORecord.TileX, lORecord.TileY);
                int liFloor = (lOLevel.GetTile(lOOpen.X, lOOpen.Y).FloorHeight >> 4) << 3;

                lOWorld.CreaturesFit = true;
                lOWorld.Log.Clear();
                bool lbOwn = UWConversationRulesUw2.TeleportCritter(lOWorld, lONpc, lORecord.TileX, lORecord.TileY, false);
                bool lbWalker = UWConversationRulesUw2.TeleportCritter(lOWorld, lONpc, lOOpen.X, lOOpen.Y, false);
                bool lbFlier = UWConversationRulesUw2.TeleportCritter(lOWorld, lONpc, lOOpen.X, lOOpen.Y, true);
                bool lbSolid = UWConversationRulesUw2.TeleportCritter(lOWorld, lONpc, lOSolid.X, lOSolid.Y, false);
                lOWorld.CreaturesFit = false;
                bool lbNoRoom = UWConversationRulesUw2.TeleportCritter(lOWorld, lONpc, lOOpen.X, lOOpen.Y, false);

                string lsExpected = "True True True False False | teleport 72 to " + lOOpen.X + "/" + lOOpen.Y + " at 4/4 z " + liFloor
                    + " ; teleport 72 to " + lOOpen.X + "/" + lOOpen.Y + " at 4/4 z " + ((liFloor + 0x80) >> 1);
                Check.Str("teleport_talker: its own tile, an open tile on the floor, a flier half way up, a solid tile, no room",
                    lbOwn + " " + lbWalker + " " + lbFlier + " " + lbSolid + " " + lbNoRoom + " | " + string.Join(" ; ", lOWorld.Log), lsExpected);
            }
            finally
            {
                System.Array.Copy(lyRaw, lONpc.RawNpcBytes, lyRaw.Length);
                lONpc.ID = liId;
                lONpc.NPCwhoami = liWhoAmI;
            }
        }

        /// <summary>The nearest open tile to a spot that is not the spot itself.</summary>
        private static UWTilePos fOpenTileNear(UWLevel pOLevel, int piX, int piY)
        {
            return fTileNear(pOLevel, piX, piY, UWTile.TileTypeEnum.open);
        }

        private static UWTilePos fSolidTileNear(UWLevel pOLevel, int piX, int piY)
        {
            return fTileNear(pOLevel, piX, piY, UWTile.TileTypeEnum.solid);
        }

        private static UWTilePos fTileNear(UWLevel pOLevel, int piX, int piY, UWTile.TileTypeEnum peType)
        {
            for (int liRange = 1; liRange < 64; liRange++)
                for (int liY = piY - liRange; liY <= piY + liRange; liY++)
                    for (int liX = piX - liRange; liX <= piX + liRange; liX++)
                    {
                        UWTile lOTile = liX > 0 && liY > 0 && liX < 63 && liY < 63 ? pOLevel.GetTile(liX, liY) : null;

                        if (lOTile != null && lOTile.TileType == peType && (liX != piX || liY != piY))
                            return new UWTilePos(liX, liY);
                    }

            return new UWTilePos(piX, piY);
        }
    }
}
