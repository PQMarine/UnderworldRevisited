using System;
using System.Collections.Generic;
using System.Globalization;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE WORDS THE PLAYER TYPES (translation wave 2, 2026-10-26): what a translation must keep
    /// consistent, or a quest cannot be solved in the language. The shrine's mantras (block 2 from
    /// 52, compared ignoring case with what is chanted, UWShrineRules) and the words the conversations
    /// compare a typed answer with (compare and contains, UWConversationTextRules), each with how many
    /// other strings name it as a whole word - the hints a player learns it from. A word no other
    /// string names is worth a look: its hint may say it in other words, or in a picture. With
    /// --strings the pack's words, so a translation is checked the same way.
    /// </summary>
    public static partial class Program
    {
        /// <summary>uwdump typed-words [path]: the mantras and the conversations' compared words.</summary>
        private static int fTypedWords(DumpContext pOContext, List<string> pOArgs)
        {
            DataImport lOData = pOContext.Data;
            UWStrings lOStrings = lOData.Strings;
            List<KeyValuePair<string, string>> lOAll = fAllStrings(lOStrings);

            Console.WriteLine("Mantras (block 2 from 52, chanted at a shrine, compared ignoring case):");

            for (int liAt = 0; liAt < UWShrineRules.MantraCount; liAt++)
            {
                const int liFirstMantra = 52;
                string lsKey = "2:" + (liFirstMantra + liAt);
                string lsMantra = UWText.Block(lOStrings, 2, liFirstMantra + liAt, null);

                if (lsMantra == null)
                    continue;

                Console.WriteLine(string.Format("  {0,-7} {1,-12} named in {2}", lsKey, lsMantra.Trim(), fNamedIn(lOAll, lsMantra.Trim(), lsKey)));
            }

            Console.WriteLine();
            Console.WriteLine("Words the conversations hand to compare or contains, mostly against a typed answer (a static scan of");
            Console.WriteLine("the code: a string put into a variable that is then handed to the function; contains: its keyword):");

            int liFound = 0;
            int liUnresolved = 0;

            for (int liSlot = 1; liSlot < lOData.Conversations.SlotCount; liSlot++)
            {
                UWConversations.Conversation lOConversation = lOData.Conversations.GetConversation(liSlot);

                if (lOConversation == null || lOConversation.Code == null || lOConversation.Code.Length == 0)
                    continue;

                foreach (KeyValuePair<string, int> lOCall in fComparedStrings(lOConversation, ref liUnresolved))
                {
                    // The VM's string numbers are one below the parsed list's (UWConversationVM).
                    int liIndex = lOCall.Value + 1;
                    string lsKey = lOConversation.StringBlock.ToString(CultureInfo.InvariantCulture) + ":" + liIndex;
                    string lsWord = UWText.Block(lOStrings, lOConversation.StringBlock, liIndex, null);

                    if (lsWord == null)
                        continue;

                    liFound++;
                    Console.WriteLine(string.Format("  slot {0,3} {1,-18} {2,-8} {3,-12} {4,-24} named in {5}", liSlot,
                        fOneLine(fPartnerName(lOData, liSlot)), lOCall.Key, lsKey, fOneLine(lsWord), fNamedIn(lOAll, lsWord.Trim(), lsKey)));
                }
            }

            Console.WriteLine();
            Console.WriteLine(liFound + " compared words found, " + liUnresolved + " calls whose word is no constant string (the player's name, a typed");
            Console.WriteLine("text compared with another). A translation keeps each word and its hints the same; the case does not count.");

            return 0;
        }

        /// <summary>Every non-empty string as (block:index, text).</summary>
        private static List<KeyValuePair<string, string>> fAllStrings(UWStrings pOStrings)
        {
            List<KeyValuePair<string, string>> lOAll = new List<KeyValuePair<string, string>>();

            foreach (KeyValuePair<int, UWStringBlock> lOBlock in pOStrings.Blocks)
            {
                for (int liIndex = 1; liIndex < lOBlock.Value.Strings.Count; liIndex++)
                {
                    string lsText = lOBlock.Value.Strings[liIndex];

                    if (!string.IsNullOrEmpty(lsText))
                        lOAll.Add(new KeyValuePair<string, string>(lOBlock.Key.ToString(CultureInfo.InvariantCulture) + ":" + liIndex, lsText));
                }
            }

            return lOAll;
        }

        /// <summary>"3 strings (3586:12, 3:40, ...)" - the other strings that hold the word as a whole
        /// word, ignoring case; "no other string" when none does.</summary>
        private static string fNamedIn(List<KeyValuePair<string, string>> pOAll, string psWord, string psOwnKey)
        {
            List<string> lsKeys = new List<string>();

            if (string.IsNullOrEmpty(psWord))
                return "(empty)";

            foreach (KeyValuePair<string, string> lOString in pOAll)
            {
                if (lOString.Key != psOwnKey && UWConversationTextRules.ContainsWord(lOString.Value, psWord))
                    lsKeys.Add(lOString.Key);
            }

            if (lsKeys.Count == 0)
                return "no other string";

            return lsKeys.Count + (lsKeys.Count == 1 ? " string (" : " strings (") + string.Join(", ", lsKeys.GetRange(0, Math.Min(4, lsKeys.Count)))
                + (lsKeys.Count > 4 ? ", ..." : string.Empty) + ")";
        }

        /// <summary>
        /// The calls of compare and contains whose argument is a variable holding a constant string -
        /// "PUSHI s, PUSHI_EFF v, SWAP, STO" puts string s into v; the call pushes its arguments' addresses
        /// (PUSHI_EFF), then their count (PUSHI), then CALLI. The function's name and the string's VM
        /// number; piUnresolved counts the calls without such an argument.
        /// </summary>
        private static List<KeyValuePair<string, int>> fComparedStrings(UWConversations.Conversation pOConversation, ref int piUnresolved)
        {
            List<KeyValuePair<string, int>> lOFound = new List<KeyValuePair<string, int>>();
            Dictionary<int, string> lOFunctions = new Dictionary<int, string>();

            foreach (UWConversations.Import lOImport in pOConversation.Imports)
            {
                if (lOImport.IsFunction && (lOImport.Name == "compare" || lOImport.Name == "contains"))
                    lOFunctions[lOImport.IdOrAddress] = lOImport.Name;
            }

            if (lOFunctions.Count == 0)
                return lOFound;

            // The instructions with their operands, in order.
            List<int> liOpcodes = new List<int>();
            List<int> liOperands = new List<int>();
            ushort[] lyCode = pOConversation.Code;

            for (int liAt = 0; liAt < lyCode.Length; liAt++)
            {
                int liOpcode = lyCode[liAt];
                bool lbOperand = liOpcode <= (int)UWConversationDisassembler.Opcode.OPNEG
                    && UWConversationDisassembler.HasImmediateOperand((UWConversationDisassembler.Opcode)liOpcode) && liAt + 1 < lyCode.Length;

                liOpcodes.Add(liOpcode);
                liOperands.Add(lbOperand ? lyCode[++liAt] : -1);
            }

            const int liPushi = (int)UWConversationDisassembler.Opcode.PUSHI;
            const int liPushiEff = (int)UWConversationDisassembler.Opcode.PUSHI_EFF;
            Dictionary<int, int> lOHeld = new Dictionary<int, int>();

            for (int liAt = 0; liAt < liOpcodes.Count; liAt++)
            {
                if (liOpcodes[liAt] == liPushi && liAt + 3 < liOpcodes.Count && liOpcodes[liAt + 1] == liPushiEff
                    && liOpcodes[liAt + 2] == (int)UWConversationDisassembler.Opcode.SWAP && liOpcodes[liAt + 3] == (int)UWConversationDisassembler.Opcode.STO)
                {
                    lOHeld[liOperands[liAt + 1]] = liOperands[liAt];

                    continue;
                }

                if (liOpcodes[liAt] != (int)UWConversationDisassembler.Opcode.CALLI || !lOFunctions.TryGetValue(liOperands[liAt], out string lsName))
                    continue;

                bool lbResolved = false;

                // The count, then the arguments' addresses below it.
                if (liAt >= 1 && liOpcodes[liAt - 1] == liPushi)
                {
                    int liCount = liOperands[liAt - 1];

                    // contains(keyword, text): only the keyword, argument 0, the first pushed (UWConversationVM
                    // reads argument N at SP - count + N - 1); compare: either argument.
                    for (int liArgument = 0; liArgument < liCount && liAt - 2 - liArgument >= 0; liArgument++)
                    {
                        int liPush = liAt - 2 - liArgument;

                        if (lsName == "contains" && liArgument != liCount - 1)
                            continue;

                        if (liOpcodes[liPush] == liPushiEff && lOHeld.TryGetValue(liOperands[liPush], out int liString))
                        {
                            lOFound.Add(new KeyValuePair<string, int>(lsName, liString));
                            lbResolved = true;
                        }
                    }
                }

                if (!lbResolved)
                    piUnresolved++;
            }

            return lOFound;
        }
    }
}
