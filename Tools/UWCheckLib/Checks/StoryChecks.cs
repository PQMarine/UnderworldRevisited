using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 3.6 (2026-10-17): the shrine's two quest mantras and the special deaths run on
    /// the fake world (FakeWorld, WorldChecks.cs) and UWGameFlags instead of hosts of their own
    /// (UWShrineRules.IHost on UWShrine, UWSpecialDeaths.IHost on UWCritter) - checked here for the
    /// first time without a scene.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckStory(DataImport pOData)
        {
            Check.Group("Shrine: the quest mantras on the world (cleanup step 3.6)");

            fCheckQuestMantras(pOData);

            Check.Group("Special deaths on the world (cleanup step 3.6)");

            fCheckSpecialDeaths();

            Check.Group("Conversation on the world (cleanup step 3.6)");

            fCheckConversationWorld(pOData);
        }

        /// <summary>gronk_door over the world's doors (open, close, toggle, where the door is
        /// heading decides), remove_talker through World.Remove - on the conversation host until
        /// cleanup step 3.6.</summary>
        private static void fCheckConversationWorld(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[0];
            UWTile lOTile = lOLevel.GetTile(1, 1);
            UWObject lODoorData = new UWObject(0x140) { TileX = 1, TileY = 1 };
            UWNpc lONpc = new UWNpc((ushort)UWObjectIds.FirstCreature);

            lOTile.ObjectsInTile.Add(lODoorData);

            try
            {
                FakeWorld lOWorld = new FakeWorld { Data = pOData, Level = lOLevel, LevelIndex = 0 };
                FakeDoor lODoor = new FakeDoor(lOWorld.Log, lODoorData, true);
                UWConversationSession lOSession = new UWConversationSession(pOData, lONpc, 1,
                    ConversationHostStub.Create(lOWorld));

                lOWorld.DoorList.Add(lODoor);

                fExpectInt("gronk_door: a door on the tile answers 1",
                    fCallTrade(lOSession, "gronk_door", new[] { 1, 1, UWConversationSession.GronkDoorOpen }), 1);
                fExpectBool("gronk_door: open opens the shut door, by no one (a thing)",
                    lOWorld.Log.Contains("door 1/1 opens") && lOWorld.Log.Contains("door 1/1 moved by Thing"), true);

                lOWorld.Log.Clear();
                fCallTrade(lOSession, "gronk_door", new[] { 1, 1, UWConversationSession.GronkDoorOpen });
                fExpectBool("gronk_door: open on an open door does nothing", lOWorld.Log.Contains("door 1/1 opens"), false);

                fCallTrade(lOSession, "gronk_door", new[] { 1, 1, UWConversationSession.GronkDoorToggle });
                fExpectBool("gronk_door: toggle closes it", lODoor.Closed, true);

                // The talking door closes itself and is told "open" while still swinging shut.
                lODoor.Moving = true;
                fCallTrade(lOSession, "gronk_door", new[] { 1, 1, UWConversationSession.GronkDoorOpen });
                fExpectBool("gronk_door: open turns a door swinging shut round", lODoor.Closed, false);

                fExpectInt("gronk_door: no door on the tile answers 0",
                    fCallTrade(lOSession, "gronk_door", new[] { 2, 2, UWConversationSession.GronkDoorOpen }), 0);

                lOWorld.Log.Clear();
                fCallTrade(lOSession, "remove_talker");
                fExpectBool("remove_talker: the partner leaves the world", lOWorld.Log.Contains("remove " + lONpc.ID), true);
            }
            finally
            {
                lOTile.ObjectsInTile.Remove(lODoorData);
            }
        }

        /// <summary>INSAHN (the Cup of Wonder) and FANLO (the Key of Truth) - UWShrineRules.Chant.</summary>
        private static void fCheckQuestMantras(DataImport pOData)
        {
            bool lbCupFound = UWGameFlags.CupOfWonderFound;
            bool lbKeyGiven = UWGameFlags.KeyOfTruthGiven;

            try
            {
                UWGameFlags.CupOfWonderFound = false;
                UWGameFlags.KeyOfTruthGiven = false;

                // On the cup's own tile and level: "very near".
                FakeWorld lOWorld = fNightWorld();
                List<string> lOLines = new List<string>();

                lOWorld.LevelIndex = UWShrineRules.CupLevel - 1;
                lOWorld.Body.Floor = new UWSpot(new UWTilePos(UWShrineRules.CupTileX, UWShrineRules.CupTileY), 3, 3, 16);

                fChant("INSAHN", pOData, lOWorld, lOLines);

                fExpectInt("cup mantra: one sentence", lOLines.Count, 1);
                fExpectBool("cup mantra: on its tile and level it is very near",
                    lOLines.Count == 1 && lOLines[0].EndsWith(UWShrineRules.VeryNearText + UWShrineRules.EndText), true);

                // Far away on level 1: a direction and the level phrase.
                lOWorld = fNightWorld();
                lOLines.Clear();
                lOWorld.LevelIndex = 0;
                lOWorld.Body.Floor = new UWSpot(new UWTilePos(UWShrineRules.CupTileX + 20, UWShrineRules.CupTileY), 3, 3, 16);

                fChant("INSAHN", pOData, lOWorld, lOLines);

                fExpectBool("cup mantra: far away on another level, a direction and the level",
                    lOLines.Count == 1 && lOLines[0].Contains(UWShrineRules.AndText), true);

                // Found: the mantra says nothing.
                UWGameFlags.CupOfWonderFound = true;
                lOLines.Clear();
                fChant("INSAHN", pOData, lOWorld, lOLines);
                fExpectInt("cup mantra: once the cup is found it says nothing", lOLines.Count, 0);

                // Without a world the quest mantras do nothing.
                UWGameFlags.CupOfWonderFound = false;
                lOLines.Clear();
                fChant("INSAHN", pOData, null, lOLines);
                fExpectInt("cup mantra: without a world nothing", lOLines.Count, 0);

                // The key: given once, onto the hand, with the original's odd line.
                lOWorld = fNightWorld();
                lOLines.Clear();
                fChant("FANLO", pOData, lOWorld, lOLines);

                fExpectBool("key mantra: the key goes to the hand",
                    lOWorld.Log.Contains("give " + UWShrineRules.KeyOfTruthObjectId + " to the hand"), true);
                fExpectBool("key mantra: \"None of your skills improved.\"", lOLines.Contains("general 31"), true);
                fExpectBool("key mantra: the key is marked as given", UWGameFlags.KeyOfTruthGiven, true);

                lOWorld = fNightWorld();
                fChant("FANLO", pOData, lOWorld, lOLines);
                fExpectBool("key mantra: only once", lOWorld.Log.Exists(psLine => psLine.StartsWith("give")), false);

                // Nothing could be given: nothing is marked, it can be chanted again.
                UWGameFlags.KeyOfTruthGiven = false;
                lOWorld = fNightWorld();
                lOWorld.HandTakes = false;
                lOLines.Clear();
                fChant("FANLO", pOData, lOWorld, lOLines);

                fExpectBool("key mantra: refused by the hand, nothing is marked", UWGameFlags.KeyOfTruthGiven, false);
                fExpectInt("key mantra: ... and nothing is said", lOLines.Count, 0);
            }
            finally
            {
                UWGameFlags.CupOfWonderFound = lbCupFound;
                UWGameFlags.KeyOfTruthGiven = lbKeyGiven;
            }
        }

        /// <summary>Chants a mantra; the scroll's lines go to pOLines ("general n" or the
        /// sentence).</summary>
        private static void fChant(string psMantra, DataImport pOData, IUWWorld pOWorld, List<string> pOLines)
        {
            UWShrineRules.Chant(psMantra, fNewSleeper(), pOData, piIndex => pOLines.Add("general " + piIndex),
                psLine => pOLines.Add(psLine), pOWorld);
        }

        /// <summary>SpecialDeathCases with both modes - UWSpecialDeaths.Apply.</summary>
        private static void fCheckSpecialDeaths()
        {
            int liDreams = UWQuestFlags.Get(UWPlayerData.DreamQuestFlag);
            int liPrisoner = UWQuestFlags.Get(6);
            bool lbTalismans = UWGameFlags.TalismansDestroyable;

            try
            {
                // The nameless one talks instead of dying and gets sixty hit points back.
                FakeWorld lOWorld = fNightWorld();
                UWCreatureRef lOTalker = fFakeCreature(1, 5, 5, 5, 5, piWhoami: UWSpecialDeaths.TalkerWhoami);

                fExpectBool("special death: the nameless one refuses his death",
                    UWSpecialDeaths.Apply(UWSpecialDeaths.TalkerWhoami, UWSpecialDeaths.Stage.Dying, lOWorld, lOTalker), false);
                fExpectBool("special death: ... and talks", lOWorld.Log.Contains("talk 65"), true);
                fExpectInt("special death: ... with sixty hit points", lOTalker.Record.HitPoints, 60);

                // The golem: 500 the first time, then he stands down and talks.
                lOWorld = fNightWorld();
                lOWorld.LevelIndex = 5;
                lOWorld.Body.Numbers = fNewSleeper();

                UWCreatureRef lOGolem = fFakeCreature(2, 5, 5, 5, 5, piWhoami: UWSpecialDeaths.MetalGolemWhoami);

                lOGolem.Record.Attacker = 1;
                lOGolem.Record.Frame = 3;

                int liExperience = lOWorld.Body.Numbers.Experience;

                fExpectBool("special death: the golem refuses his death",
                    UWSpecialDeaths.Apply(UWSpecialDeaths.MetalGolemWhoami, UWSpecialDeaths.Stage.Dying, lOWorld, lOGolem), false);

                int liGained = lOWorld.Body.Numbers.Experience - liExperience;

                fExpectBool("special death: the golem's reward the first time (" + liGained + ")", liGained > 0, true);
                fExpectInt("special death: the golem's marker set", lOGolem.Record.TileState, 1);
                fExpectInt("special death: he stands down - no attacker", lOGolem.Record.Attacker, 0);
                fExpectInt("special death: ... standing", lOGolem.Record.Animation, UWCritterBrain.AnimStanding);
                fExpectInt("special death: ... frame 0", lOGolem.Record.Frame, 0);
                fExpectBool("special death: ... the player leaves combat mode", lOWorld.Body.CombatModeLeft, true);
                fExpectBool("special death: ... and he talks", lOWorld.Log.Contains("talk 66"), true);

                liExperience = lOWorld.Body.Numbers.Experience;
                UWSpecialDeaths.Apply(UWSpecialDeaths.MetalGolemWhoami, UWSpecialDeaths.Stage.Dying, lOWorld, lOGolem);
                fExpectInt("special death: the golem's reward only once", lOWorld.Body.Numbers.Experience, liExperience);

                // An ordinary named death goes ahead; its consequence comes when it is dead.
                lOWorld = fNightWorld();
                UWQuestFlags.Set(6, 0);

                UWCreatureRef lOPrisoner = fFakeCreature(3, 5, 5, 5, 5, piWhoami: UWSpecialDeaths.PrisonerWhoami);

                fExpectBool("special death: the prisoner dies",
                    UWSpecialDeaths.Apply(UWSpecialDeaths.PrisonerWhoami, UWSpecialDeaths.Stage.Dying, lOWorld, lOPrisoner), true);
                fExpectInt("special death: ... and nothing happens while dying", UWQuestFlags.Get(6), 0);
                UWSpecialDeaths.Apply(UWSpecialDeaths.PrisonerWhoami, UWSpecialDeaths.Stage.Dead, lOWorld, lOPrisoner);
                fExpectInt("special death: the prisoner's quest flag 6 when dead", UWQuestFlags.Get(6), 1);

                // Garamon: the talismans can no longer be destroyed.
                UWGameFlags.TalismansDestroyable = true;
                UWSpecialDeaths.Apply(UWSpecialDeaths.GaramonWhoami, UWSpecialDeaths.Stage.Dead, fNightWorld(), default);
                fExpectBool("special death: Garamon's death makes the talismans indestructible", UWGameFlags.TalismansDestroyable, false);

                // Tyball: the cutscene, the dream bit, one ally per whoami, the prison's move triggers.
                lOWorld = fNightWorld();
                UWQuestFlags.Set(UWPlayerData.DreamQuestFlag, 0);

                lOWorld.Creatures.Add(fFakeCreature(4, 5, 5, 5, 5, pbDead: true, piWhoami: 0xD1, piIndex: 10));
                lOWorld.Creatures.Add(fFakeCreature(5, 5, 5, 5, 5, piWhoami: 0xD1, piIndex: 11));
                lOWorld.Creatures.Add(fFakeCreature(6, 5, 5, 5, 5, piWhoami: 0xD1, piIndex: 12));
                lOWorld.Creatures.Add(fFakeCreature(7, 5, 5, 5, 5, piWhoami: 0xDE, piIndex: 13));

                UWTile lOPrison = lOWorld.Level.GetTile(UWQuestPlaces.TyballPrisonTileX, UWQuestPlaces.TyballPrisonTileY);

                lOPrison.ObjectsInTile.Add(new UWObject(UWObjectMechanics.MoveTriggerId));
                lOPrison.ObjectsInTile.Add(new UWObject(0x95));
                lOPrison.ObjectsInTile.Add(new UWObject(UWObjectMechanics.MoveTriggerId));

                fExpectBool("special death: Tyball dies",
                    UWSpecialDeaths.Apply(UWSpecialDeaths.TyballWhoami, UWSpecialDeaths.Stage.Dead, lOWorld, default), true);
                fExpectBool("special death: Tyball's cutscene", lOWorld.Shown.Log.Contains("cutscene 2"), true);
                fExpectInt("special death: Tyball's dream bit", UWQuestFlags.Get(UWPlayerData.DreamQuestFlag) & 4, 4);
                fExpectInt("special death: one of the two living 0xD1 goes, the dead one is passed over",
                    lOWorld.Log.FindAll(psLine => psLine == "remove " + 0x45).Count
                    + lOWorld.Log.FindAll(psLine => psLine == "remove " + 0x46).Count, 1);
                fExpectBool("special death: ... the first in the list", lOWorld.Log.Contains("remove " + 0x45), true);
                fExpectBool("special death: 0xDE, the table's first entry, is not an ally", lOWorld.Log.Contains("remove " + 0x47), false);
                fExpectInt("special death: both move triggers of the prison go",
                    lOWorld.Log.FindAll(psLine => psLine == "remove " + UWObjectMechanics.MoveTriggerId).Count, 2);
                fExpectBool("special death: ... nothing else there", lOWorld.Log.Contains("remove " + 0x95), false);

                // Without a world nothing special happens.
                fExpectBool("special death: without a world the death goes ahead",
                    UWSpecialDeaths.Apply(UWSpecialDeaths.TalkerWhoami, UWSpecialDeaths.Stage.Dying, null, lOTalker), true);
            }
            finally
            {
                UWQuestFlags.Set(UWPlayerData.DreamQuestFlag, liDreams);
                UWQuestFlags.Set(6, liPrisoner);
                UWGameFlags.TalismansDestroyable = lbTalismans;
            }
        }
    }
}
