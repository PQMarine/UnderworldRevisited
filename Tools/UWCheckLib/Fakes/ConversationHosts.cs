using System;
using System.Collections.Generic;
using System.Reflection;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The two conversation hosts of the checks: the virtual machine's (UWConversationVM.IHost),
    /// which only listens, and the session's (IUWConversationHost), which answers every question
    /// with its default.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// A conversation host that only listens, for running a script's routine or a few
        /// hand-made words: what is said is kept, the quest flags read 0 and are not written,
        /// an unknown function answers 0. Random answers 1, or with RollDice a roll of 1 to n on
        /// UWRandom. One host since cleanup 9.1 (2026-10-25), which merged ScriptHost (it rolled)
        /// and SilentConversationHost (it answered 1).
        /// </summary>
        private sealed class FakeScriptHost : UWConversationVM.IHost
        {
            public readonly List<string> Said = new List<string>();

            /// <summary>Roll Random on UWRandom instead of answering 1.</summary>
            public bool RollDice;

            public void Say(string psText) { Said.Add(psText); }

            public void Print(string psText) { }

            public int GetQuest(int piFlag) { return 0; }

            public void SetQuest(int piFlag, int piValue) { }

            public int Random(int piMaximum)
            {
                if (!RollDice)
                    return 1;

                return piMaximum <= 0 ? 0 : UWRandom.Next(1, piMaximum + 1);
            }

            public int CallUnknown(string psName, int piFunctionId, IReadOnlyList<int> pOArguments,
                IReadOnlyList<int> pOAddresses, UWConversationVM pOVm)
            {
                return 0;
            }
        }

        /// <summary>A conversation host that answers every question with its default (no player)
        /// and the given level - built as a proxy, so the forty members need no hand-written body.</summary>
        public class ConversationHostStub : DispatchProxy
        {
            /// <summary>The conversation's world (cleanup step 3.6, 2026-10-17): a FakeWorld over the
            /// given level, its number the level's own (0 without one, as this stub answered the
            /// host's CurrentLevelNumber before).</summary>
            private object mOWorld;

            public static IUWConversationHost Create(UWLevel pOLevel)
            {
                return Create(new FakeWorld { Level = pOLevel, LevelIndex = (pOLevel != null ? pOLevel.LevelNumber : 0) - 1 });
            }

            /// <summary>A stub over a world the check holds - to read what the conversation did to it.</summary>
            internal static IUWConversationHost Create(IUWWorld pOWorld)
            {
                IUWConversationHost lIHost = Create<IUWConversationHost, ConversationHostStub>();

                ((ConversationHostStub)(object)lIHost).mOWorld = pOWorld;

                return lIHost;
            }

            protected override object Invoke(MethodInfo pOMethod, object[] pOArguments)
            {
                if (pOMethod.Name == "get_World")
                    return mOWorld;

                return pOMethod.ReturnType.IsValueType && pOMethod.ReturnType != typeof(void)
                    ? Activator.CreateInstance(pOMethod.ReturnType)
                    : null;
            }
        }
    }
}
