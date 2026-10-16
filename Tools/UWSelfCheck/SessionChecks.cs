using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 2.7 (2026-10-14): the game session. Two sessions share nothing (no store, no
    /// holder, no list is the same object, and what one session's statics write the other does not
    /// read), and starting a game - New Game, Load, New Game again - resets every session-owned
    /// value, the three B21 leaks (lava quirk, cast pause, light colours) included. Every value is
    /// read back through the old static facades, so the check also proves they forward.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckSession(CheckContext pOContext)
        {
            Check.Group("Game session (UWGameSession: two sessions share nothing, a start resets everything)");

            UWGameSession lOBefore = UWGameSession.Current;

            try
            {
                fCheckSessionsShareNothing(pOContext);
                fCheckSessionStartResets(pOContext);
            }
            finally
            {
                UWGameSession.MakeCurrent(lOBefore);
            }
        }

        private static void fCheckSessionsShareNothing(CheckContext pOContext)
        {
            DataImport lOData = pOContext.Data;
            UWGameSession lOFirst = new UWGameSession(lOData.GameData);
            UWGameSession lOSecond = new UWGameSession(lOData.GameData);

            // Structurally: every store, holder and list of one session is its own object.
            List<string> lsShared = new List<string>();

            fCollectShared(lOFirst, lOSecond, "session", lsShared, 0);

            Check.Bool("session: two sessions share no object (" + string.Join(", ", lsShared) + ")", lsShared.Count == 0, true);
            Check.Bool("session: both run on the same installation", ReferenceEquals(lOFirst.Data, lOSecond.Data), true);

            // Behaviourally: what the statics write into one is not read from the other.
            UWGameSession.MakeCurrent(lOFirst);

            string lsEmpty = fDescribeSession();

            fDirtySession(pOContext);

            string lsDirty = fDescribeSession();

            Check.Bool("session: the statics write into the current session", lsDirty != lsEmpty, true);

            UWGameSession.MakeCurrent(lOSecond);
            Check.Str("session: the second session still reads empty", fDescribeSession(), lsEmpty);

            UWGameSession.MakeCurrent(lOFirst);
            Check.Str("session: the first session kept its values", fDescribeSession(), lsDirty);
        }

        private static void fCheckSessionStartResets(CheckContext pOContext)
        {
            DataImport lOData = pOContext.Data;

            UWGameSession.Begin(lOData.GameData);

            string lsEmpty = fDescribeSession();

            // NEW GAME: everything of the previous game is gone, with a new character seeded.
            fDirtySession(pOContext);
            UWGameSession.Begin(lOData.GameData);
            Check.Str("session: New Game starts with every session value reset", fDescribeSession(), lsEmpty);

            UWPlayerData lONewCharacter = UWPlayerData.FromPlain(UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter()));

            UWGameSession.Current.SeedFrom(lONewCharacter);
            fExpectSeeded("session: New Game", lONewCharacter);

            // LOAD: the same, seeded from each save game.
            List<string> lsSaves = new List<string>();

            if (!string.IsNullOrEmpty(pOContext.SavesPath) && Directory.Exists(pOContext.SavesPath))
                foreach (string lsSave in Directory.GetDirectories(pOContext.SavesPath).OrderBy(psPath => psPath, StringComparer.Ordinal))
                    if (File.Exists(Path.Combine(lsSave, "PLAYER.DAT")))
                        lsSaves.Add(lsSave);

            foreach (string lsSave in lsSaves)
            {
                UWPlayerData lOPlayer = new UWPlayerData(lsSave);
                string lsName = Path.GetFileName(lsSave);

                fDirtySession(pOContext);
                UWGameSession.Begin(lOData.GameData);
                Check.Str("session: Load " + lsName + " starts with every session value reset", fDescribeSession(), lsEmpty);

                UWGameSession.Current.SeedFrom(lOPlayer);
                fExpectSeeded("session: Load " + lsName, lOPlayer);
            }

            if (lsSaves.Count == 0)
                Check.Pass("session: Load", "no saves given (--saves <folder>)");

            // AND A NEW GAME AFTER THE LOAD: nothing of the loaded game survives.
            fDirtySession(pOContext);
            UWGameSession.Begin(lOData.GameData);
            Check.Str("session: New Game after Load starts with every session value reset", fDescribeSession(), lsEmpty);
        }

        /// <summary>The seeded stores hold exactly what PLAYER.DAT holds; nothing else was set.</summary>
        private static void fExpectSeeded(string psName, UWPlayerData pOPlayer)
        {
            bool lbQuests = UWQuestFlags.All.Count == Enumerable.Range(0, pOPlayer.QuestFlags).Count(piAt => pOPlayer.GetQuestFlag(piAt) != 0);
            bool lbVariables = UWGameVariables.All.Count == Enumerable.Range(0, pOPlayer.GameVariables).Count(piAt => pOPlayer.GetGameVariable(piAt) != 0);

            for (int liAt = 0; liAt < pOPlayer.QuestFlags; liAt++)
                lbQuests &= UWQuestFlags.Get(liAt) == pOPlayer.GetQuestFlag(liAt);

            for (int liAt = 0; liAt < pOPlayer.GameVariables; liAt++)
                lbVariables &= UWGameVariables.Get(liAt) == (pOPlayer.GetGameVariable(liAt) & UWGameVariables.ValueMask);

            Check.Bool(psName + ": the quest flags are the save game's", lbQuests, true);
            Check.Bool(psName + ": the game variables are the save game's", lbVariables, true);
            Check.Bool(psName + ": the story flags are the save game's",
                UWGameFlags.TalismansDestroyable == pOPlayer.TalismansDestroyable
                && UWGameFlags.GaramonBuried == pOPlayer.GaramonBuried
                && UWGameFlags.CupOfWonderFound == pOPlayer.CupOfWonderFound
                && UWGameFlags.KeyOfTruthGiven == pOPlayer.KeyOfTruthGiven
                && UWGameFlags.OrbDestroyed == pOPlayer.OrbDestroyed
                && UWGameFlags.OrbManaBackup == pOPlayer.OrbManaBackup
                && UWGameFlags.SilverTreeLevel == pOPlayer.SilverTreeLevel
                && UWGameFlags.MoonstoneLevel == pOPlayer.MoonstoneLevel, true);
        }

        /// <summary>Gives every session-owned value something other than its start value, through
        /// the statics the game uses (the quirks without a static writer directly).</summary>
        private static void fDirtySession(CheckContext pOContext)
        {
            DataImport lOData = pOContext.Data;

            UWQuestFlags.Set(5, 3);
            UWGameVariables.Set(31, 7);
            UWGameFlags.TalismansDestroyable = true;
            UWGameFlags.GaramonBuried = true;
            UWGameFlags.CupOfWonderFound = true;
            UWGameFlags.KeyOfTruthGiven = true;
            UWGameFlags.OrbDestroyed = true;
            UWGameFlags.OrbManaBackup = 42;
            UWGameFlags.SilverTreeLevel = 4;
            UWGameFlags.MoonstoneLevel = 2;
            UWCarriedSlots.Level = pOContext.Level(0);
            UWArmourProtection.ResistBlowsAddsArmour = true;

            UWSessionQuirks lOQuirks = UWGameSession.Current.Quirks;

            for (int liKind = 0; liKind < lOQuirks.LavaFlagDropped.Length; liKind++)
                lOQuirks.LavaFlagDropped[liKind] = true;

            lOQuirks.LastCastSeconds = 12.5;
            lOQuirks.CastDelaySeconds = 0.25;

            UWObjectLightColour.TryGet(lOData, UWObjectIds.FirstCreature - 1, out _, out _, out _);

            UWTrapLog.Detail = "dirty";
            UWTrapLog.Record(new UWObject(0x180), null, lOData, true);
            UWLevelWriter.Warnings.Add("dirty");

            UWGameSession.Current.CritterClock.Advance(64, false);
            UWGameSession.Current.CritterAlarm.Kind = 3;
            UWGameSession.Current.CritterAlarm.Index = 7;
        }

        /// <summary>Every session-owned value, read through the statics where there is one.</summary>
        private static string fDescribeSession()
        {
            UWGameSession lOSession = UWGameSession.Current;
            StringBuilder lOText = new StringBuilder();

            lOText.Append("quests ").Append(string.Join(",", UWQuestFlags.All.OrderBy(pO => pO.Key).Select(pO => pO.Key + "=" + pO.Value)));
            lOText.Append("; variables ").Append(string.Join(",", UWGameVariables.All.OrderBy(pO => pO.Key).Select(pO => pO.Key + "=" + pO.Value)));
            lOText.Append("; story ").Append(UWGameFlags.TalismansDestroyable).Append(UWGameFlags.GaramonBuried)
                .Append(UWGameFlags.CupOfWonderFound).Append(UWGameFlags.KeyOfTruthGiven).Append(UWGameFlags.OrbDestroyed)
                .Append(' ').Append(UWGameFlags.OrbManaBackup).Append(' ').Append(UWGameFlags.SilverTreeLevel)
                .Append(' ').Append(UWGameFlags.MoonstoneLevel).Append(' ').Append(UWSilverTreeRules.TreeLevel);
            lOText.Append("; carried level ").Append(UWCarriedSlots.Level != null);
            lOText.Append("; resist blows ").Append(UWArmourProtection.ResistBlowsAddsArmour);
            lOText.Append("; lava ").Append(UWCreatureMotion.LavaFlagDropped(UWCreatureMotion.Kind.Land))
                .Append(UWCreatureMotion.LavaFlagDropped(UWCreatureMotion.Kind.Flier))
                .Append(UWCreatureMotion.LavaFlagDropped(UWCreatureMotion.Kind.Swimmer));
            lOText.Append("; cast pause ").Append(lOSession.Quirks.LastCastSeconds).Append('+').Append(lOSession.Quirks.CastDelaySeconds);
            lOText.Append("; light colours ").Append(lOSession.LightColours.Count);
            lOText.Append("; trap log ").Append(UWTrapLog.Entries.Count).Append(' ').Append(UWTrapLog.Detail ?? "-");
            lOText.Append("; writer warnings ").Append(UWLevelWriter.Warnings.Count);
            lOText.Append("; critter clock ").Append(lOSession.CritterClock.Clock).Append('/').Append(lOSession.CritterClock.Phase);
            lOText.Append("; alarm ").Append(lOSession.CritterAlarm.Kind).Append('/').Append(lOSession.CritterAlarm.Index);

            return lOText.ToString();
        }

        /// <summary>
        /// Cleanup step 2.8 (2026-10-14): the session's generator is the original's rand
        /// (UWOriginalRandom) with the original's way of taking a range (rand() % n), every call one
        /// roll; the same seed repeats the sequence, two sessions draw independently, a scripted
        /// source sits in front without moving the generator, and UWRandom forwards to the current
        /// session.
        /// </summary>
        private static void fCheckSessionRandom()
        {
            Check.Group("Session random (UWSessionRandom: the original's rand, one per session)");

            UWGameSession lOBefore = UWGameSession.Current;

            try
            {
                UWSessionRandom lORandom = new UWSessionRandom(1);

                Check.Seq("session rng: seed 1 rolls the original's 346, 130, 10982",
                    new[] { lORandom.Next(UWOriginalRandom.Range), lORandom.Next(UWOriginalRandom.Range), lORandom.Next(UWOriginalRandom.Range) },
                    new[] { 346, 130, 10982 });

                // Every way of drawing is one roll of the original's generator, taken as it takes it.
                UWSessionRandom lOSession = new UWSessionRandom(0x4D2);
                UWOriginalRandom lOOriginal = new UWOriginalRandom(0x4D2);
                bool lbSame = true;

                for (int liAt = 0; liAt < 200; liAt++)
                {
                    lbSame &= lOSession.Next(7) == lOOriginal.Next() % 7;
                    lbSame &= lOSession.Next(8) == (lOOriginal.Next() & 7);
                    lbSame &= lOSession.Next(-3, 4) == -3 + (lOOriginal.Next() % 7);
                    lbSame &= lOSession.Next(1) == 0 && lOOriginal.Next() >= 0;
                    lbSame &= lOSession.NextDouble() == lOOriginal.Next() / (double)UWOriginalRandom.Range;
                }

                Check.Bool("session rng: Next(n) is rand() % n, Next(a, b) a + rand() % (b - a), NextDouble rand() / 0x8000, one roll each", lbSame, true);

                int liWide = new UWSessionRandom(5).Next(1 << 20);
                UWOriginalRandom lOWide = new UWOriginalRandom(5);
                int liExpected = (int)((((long)lOWide.Next() << 15) | (long)lOWide.Next()) % (1 << 20));

                Check.Int("session rng: a range beyond 0x8000 takes two rolls", liWide, liExpected);

                // The seed: the same seed repeats, the lower word counts (InitRNG), Reseed restarts.
                int[] liFirst = fDraw(new UWSessionRandom(77), 12);

                Check.Seq("session rng: the same seed draws the same sequence", fDraw(new UWSessionRandom(77), 12), liFirst);
                Check.Seq("session rng: only the seed's lower word counts, as InitRNG takes it", fDraw(new UWSessionRandom(77 + 0x10000), 12), liFirst);

                UWSessionRandom lOAgain = new UWSessionRandom(3);

                fDraw(lOAgain, 5);
                lOAgain.Reseed(77);
                Check.Seq("session rng: Reseed starts the sequence again", fDraw(lOAgain, 12), liFirst);

                // A scripted source in front; taken away, the generator continues where it was.
                UWSessionRandom lOScripted = new UWSessionRandom(77);
                int[] liHead = fDraw(lOScripted, 4);

                lOScripted.Use(new ConstantRandom(5));
                Check.Seq("session rng: a scripted source answers while it is in front", fDraw(lOScripted, 3), new[] { 5, 5, 5 });

                lOScripted.Use(null);
                Check.Seq("session rng: without it the generator continues where it was",
                    liHead.Concat(fDraw(lOScripted, 8)).ToArray(), liFirst);

                // Two sessions: each its own generator, the statics draw from the current one.
                UWGameSession lOA = new UWGameSession(null, 77);
                UWGameSession lOB = new UWGameSession(null, 77);

                UWGameSession.MakeCurrent(lOA);
                int[] liFromStatics = new int[6];

                for (int liAt = 0; liAt < liFromStatics.Length; liAt++)
                    liFromStatics[liAt] = UWRandom.Next(UWOriginalRandom.Range);

                Check.Seq("session rng: UWRandom draws from the current session's generator", liFromStatics, liFirst.Take(6).ToArray());
                Check.Seq("session rng: the other session's generator did not move", fDraw(lOB.Random, 12), liFirst);
                Check.Bool("session rng: UWRandom.Source is the current session's generator", ReferenceEquals(UWRandom.Source, lOA.Random), true);

                UWRandom.UseSeed(77);
                Check.Seq("session rng: UWRandom.UseSeed reseeds the current session", fDraw(lOA.Random, 12), liFirst);
            }
            finally
            {
                UWGameSession.MakeCurrent(lOBefore);
            }
        }

        /// <summary>
        /// Cleanup step 2.8 (2026-10-14), the barter's global draw: once the four slots are full,
        /// setup_to_barter (BARTER.C) passes over an item when the global rand masked to three bits
        /// is below five - five in eight, from the session's generator (ours rolled 0..6, five in
        /// seven, from the static). A scripted source answers 4, 5 and 7 for the three items after
        /// the first four: the first is passed over, the other two take slots 0 and 1.
        /// </summary>
        private static void fCheckBarterOverflow(CheckContext pOContext)
        {
            Check.Group("Barter overflow (setup_to_barter: five in eight from the session's generator)");

            DataImport lOData = pOContext.Data;
            UWLevel lOLevel = pOContext.Level(0);
            List<int> liValuable = new List<int>();
            UWCommonObjectProperties.Entry lOEntry;

            // Valuable things of a minor class other than 0 (the first of minor class 0 is kept back).
            for (int liId = 0x80; liId < 0x140 && liValuable.Count < 7; liId++)
                if (((liId >> 4) & 3) != 0 && lOData.CommonObjectProperties.TryGet(liId, out lOEntry) && lOEntry.Value > 0)
                    liValuable.Add(liId);

            if (lOLevel == null || liValuable.Count < 7)
            {
                Check.Fail("barter overflow", "no level or fewer than seven valuable objects");

                return;
            }

            UWNpc lONpc = new UWNpc((ushort)UWObjectIds.FirstCreature) { NPCLootSpawned = true };

            lONpc.EnsureContentsLoaded(null);

            foreach (int liId in liValuable)
                lONpc.Contents.Add(new UWObject((ushort)liId));

            // Any conversation will do: the function is called the way the machine calls it, by name.
            UWConversationSession lOConversation = new UWConversationSession(lOData, lONpc, 1, ConversationHostStub.Create(lOLevel));
            UWConversationTrade lOTrade = lOConversation.Trade;
            RecordingRandom lOScript = new RecordingRandom(4, 5, 7);

            if (lOTrade == null)
            {
                Check.Fail("barter overflow", "no trade: " + lOConversation.Error);

                return;
            }

            UWRandom.Use(lOScript);

            try
            {
                lOConversation.CallUnknown("setup_to_barter", 0, Array.Empty<int>(), Array.Empty<int>(), null);
            }
            finally
            {
                UWRandom.Use(null);
            }

            Check.Str("barter overflow: three rolls, each one of eight", string.Join(" ", lOScript.Ranges), "8 8 8");
            Check.Seq("barter overflow: the slots hold items 6, 7, 3 and 4 (item 5 rolled 4, passed over)",
                lOTrade.NpcItems.Select(pOItem => pOItem == null ? -1 : liValuable.IndexOf(pOItem.ID) + 1).ToArray(),
                new[] { 6, 7, 3, 4 });
        }

        /// <summary>Answers the given values in turn and remembers each range asked for.</summary>
        private sealed class RecordingRandom : IUWRandom
        {
            private readonly Queue<int> mOValues;

            public readonly List<string> Ranges = new List<string>();

            public RecordingRandom(params int[] piValues)
            {
                mOValues = new Queue<int>(piValues);
            }

            public int Next(int piMaxExclusive)
            {
                Ranges.Add(piMaxExclusive.ToString());

                return mOValues.Count > 0 ? mOValues.Dequeue() % Math.Max(1, piMaxExclusive) : 0;
            }

            public int Next(int piMin, int piMaxExclusive)
            {
                Ranges.Add(piMin + ".." + piMaxExclusive);

                return mOValues.Count > 0 ? piMin + (mOValues.Dequeue() % Math.Max(1, piMaxExclusive - piMin)) : piMin;
            }

            public double NextDouble()
            {
                Ranges.Add("double");

                return 0.0;
            }
        }

        /// <summary>A conversation host that answers every question with its default (no player)
        /// and the given level - built as a proxy, so the forty members need no hand-written body.</summary>
        public class ConversationHostStub : DispatchProxy
        {
            private UWLevel mOLevel;

            public static IUWConversationHost Create(UWLevel pOLevel)
            {
                IUWConversationHost lIHost = Create<IUWConversationHost, ConversationHostStub>();

                ((ConversationHostStub)(object)lIHost).mOLevel = pOLevel;

                return lIHost;
            }

            protected override object Invoke(MethodInfo pOMethod, object[] pOArguments)
            {
                if (pOMethod.Name == "get_CurrentLevel")
                    return mOLevel;

                if (pOMethod.Name == "get_CurrentLevelNumber")
                    return mOLevel != null ? mOLevel.LevelNumber : 0;

                return pOMethod.ReturnType.IsValueType && pOMethod.ReturnType != typeof(void)
                    ? Activator.CreateInstance(pOMethod.ReturnType)
                    : null;
            }
        }

        private static int[] fDraw(UWSessionRandom pORandom, int piCount)
        {
            int[] liRolls = new int[piCount];

            for (int liAt = 0; liAt < piCount; liAt++)
                liRolls[liAt] = pORandom.Next(UWOriginalRandom.Range);

            return liRolls;
        }

        /// <summary>A scripted source that always answers the same value (modulo the range).</summary>
        private sealed class ConstantRandom : IUWRandom
        {
            private readonly int miValue;

            public ConstantRandom(int piValue)
            {
                miValue = piValue;
            }

            public int Next(int piMaxExclusive) => piMaxExclusive <= 0 ? 0 : miValue % piMaxExclusive;

            public int Next(int piMin, int piMaxExclusive) => piMin + Next(piMaxExclusive - piMin);

            public double NextDouble() => 0.5;
        }

        /// <summary>Walks the public properties and fields of two sessions side by side and lists
        /// every reference both hold (the installation's data excepted - that one is shared on
        /// purpose). Holders of the engine-free layer are walked one level further.</summary>
        private static void fCollectShared(object pOFirst, object pOSecond, string psPath, List<string> psShared, int piDepth)
        {
            if (pOFirst == null || pOSecond == null || piDepth > 2)
                return;

            Type lOType = pOFirst.GetType();
            List<(string Name, object First, object Second)> lOMembers = new List<(string, object, object)>();

            foreach (PropertyInfo lOProperty in lOType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (lOProperty.GetIndexParameters().Length == 0)
                    lOMembers.Add((lOProperty.Name, lOProperty.GetValue(pOFirst), lOProperty.GetValue(pOSecond)));

            foreach (FieldInfo lOField in lOType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                lOMembers.Add((lOField.Name, lOField.GetValue(pOFirst), lOField.GetValue(pOSecond)));

            foreach ((string lsName, object lOA, object lOB) in lOMembers)
            {
                if (lOA == null || lOB == null || lOA is string || lOA.GetType().IsValueType || lOA is UWGameData)
                    continue;

                if (ReferenceEquals(lOA, lOB))
                    psShared.Add(psPath + "." + lsName);
                else if (lOA.GetType().Namespace == typeof(UWGameSession).Namespace)
                    fCollectShared(lOA, lOB, psPath + "." + lsName, psShared, piDepth + 1);
            }
        }
    }
}
