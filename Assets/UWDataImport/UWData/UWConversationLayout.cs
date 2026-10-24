using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE AND HOW THE CLASSIC CONVERSATION WRITES (cleanup step 5.12, 2026-10-20): the measures of
	/// CONV.BYT as the conversation's text needs them (the places themselves are
	/// UWClassicLayout.Conversation's), how the history and the answers wrap - the line break that
	/// ends a line and indents what follows, the page marks, the history's capacity -, the answers'
	/// numbering and click rows, the typed answer's limits, the trade slots' hit test and the
	/// portraits' choice. Until then all of it stood in the classic screen (UWConversationScreen),
	/// and the modern original look repeated the places as offsets minus 43. Engine-free (decision 26
	/// of the cleanup plan): UWConversationView keeps the state, UWClassicConversationRenderer and
	/// UWModernConversation draw it.
	///
	/// Coordinates are the original's screen pixels, x from the left and y from the TOP.
	/// </summary>
	public static class UWConversationLayout
	{
		// ------------------------------------------------- The parchment

		public const int HistoryLeft = UWClassicLayout.Conversation.HistoryLeft;

		public const int HistoryTop = UWClassicLayout.Conversation.HistoryTop;

		/// <summary>Up to just before the right vine edge (220). 159, not 158 (per user with a
		/// screenshot of the original, 2026-09-30): Ketchaval's "the last holding of the noble
		/// Goblins," is 159 pixels in font5x6p and stays on one line there, while every line the
		/// original wrapped would have run to 160 or more with its next word.</summary>
		public const int HistoryWidth = UWClassicLayout.Conversation.HistoryWidth;

		/// <summary>Thirteen six-pixel lines, down to row 130 - the lower edge starts at 127,
		/// so the last line already reaches into it. The file's sample does not get that far (its
		/// fifth line does not), but the reference's template goes down to 130.</summary>
		public const int HistoryHeight = UWClassicLayout.Conversation.HistoryHeight;

		// ------------------------------------------------- The scroll (the message box's measures)

		public const int ChoicesLeft = UWClassicLayout.Conversation.ChoicesLeft;

		public const int ChoicesTop = UWClassicLayout.Conversation.ChoicesTop;

		public const int ChoicesWidth = UWClassicLayout.Conversation.ChoicesWidth;

		public const int ChoicesHeight = UWClassicLayout.Conversation.ChoicesHeight;

		// ------------------------------------------------- The names

		/// <summary>
		/// NO CLIP: the original prints both names with string_to_screen at 0x30 and 0x90
		/// (UnderworldExhumed CONVERSE.C), which draws the whole string - a long name runs on towards
		/// the screen's edge. Until 2026-10-14 ours cut both at 88 pixels (reference audit, finding
		/// of the fix agents; a name may have 29 characters since 2026-10-12). Each name now has the
		/// room to the screen's right edge.
		/// </summary>
		public static int NameRoom(int piLeft)
		{
			return UWClassicLayout.Screen.Width - piLeft;
		}

		// ------------------------------------------------- Wrapping

		/// <summary>The markers in the text at which the original pauses: "\m" is documented
		/// (reference MessageDisplay), "\p" appears alongside it in the data (Ben on level 2) and
		/// is treated the same way - whether the original pauses there is not documented.</summary>
		public static readonly string[] PageBreaks = { "\\m", "\\p" };

		private static readonly string[] msLineBreaks = { "\r\n", "\n" };

		/// <summary>A history entry for a history that scrolls (the modern one, which never pauses):
		/// the page marks dropped, no carriage returns, trimmed; its line breaks stay.</summary>
		public static string WithoutPageBreaks(string psText)
		{
			string lsText = psText ?? string.Empty;

			foreach (string lsBreak in PageBreaks)
				lsText = lsText.Replace(lsBreak, string.Empty);

			return lsText.Replace("\r", string.Empty).Trim();
		}

		/// <summary>
		/// A LINE BREAK IN THE TEXT STARTS A NEW LINE, and what follows keeps its leading space (per
		/// user with a screenshot of the original, 2026-09-30: Ketchaval's "the Gray tribe.\n Years
		/// ago," - the original ends the line after "tribe." and indents " Years ago," by that
		/// space). Ours wrapped over it, the original font drawing the break as nothing.
		/// </summary>
		public static List<string> WrapWithBreaks(UWFont pOFont, string psText, int piWidth)
		{
			List<string> lOLines = new List<string>();

			foreach (string lsPart in psText.Split(msLineBreaks, StringSplitOptions.None))
				lOLines.AddRange(UWTextLayout.WrapText(pOFont, lsPart, piWidth));

			return lOLines;
		}

		/// <summary>How many lines the parchment holds in this font: thirteen in font5x6p, never
		/// fewer than two.</summary>
		public static int HistoryCapacity(UWFont pOFont)
		{
			int liLineHeight = pOFont.Height + UWTextLayout.LineSpacing;

			return Math.Max(2, HistoryHeight / liLineHeight);
		}

		// ------------------------------------------------- The answers

		/// <summary>The original numbers with a period: "1. Don't mention it." (that is how it appears in the
		/// sample in CONV.BYT).</summary>
		public static string FormatChoice(int piIndex, string psChoice)
		{
			return (piIndex + 1) + ". " + psChoice;
		}

		/// <summary>The answers numbered and wrapped to the scroll's width into pOLines; pOFirstLines
		/// gets the line each answer starts on.</summary>
		public static void WrapChoices(UWFont pOFont, IReadOnlyList<string> pOChoices, List<string> pOLines, List<int> pOFirstLines)
		{
			pOLines.Clear();
			pOFirstLines.Clear();

			for (int liIndex = 0; liIndex < pOChoices.Count; liIndex++)
			{
				pOFirstLines.Add(pOLines.Count);
				pOLines.AddRange(UWTextLayout.WrapText(pOFont, FormatChoice(liIndex, pOChoices[liIndex]), ChoicesWidth));
			}
		}

		/// <summary>
		/// The click rows of answer piIndex: it reaches from its first line to the next answer's. An
		/// answer that runs over two lines gets a correspondingly tall area - but THE LAST ANSWER
		/// ALWAYS GETS ONE ROW, whatever it wraps to (the classic screen's hit areas since they were
		/// built; kept as they are by cleanup step 5.12).
		/// </summary>
		public static void ChoiceRows(IReadOnlyList<int> pOFirstLines, int piIndex, out int piTopLine, out int piLineCount)
		{
			int liNextLine = piIndex + 1 < pOFirstLines.Count
				? pOFirstLines[piIndex + 1]
				: pOFirstLines[piIndex] + 1;

			piTopLine = pOFirstLines[piIndex];
			piLineCount = liNextLine - piTopLine;
		}

		// ------------------------------------------------- The typed answer (babl_ask)

		/// <summary>
		/// babl_ask's input (getInputText_ovr103_1117 through wdialog, SCROLLIO.C, read 2026-10-15): at
		/// most 0x32 = 50 characters, 0x20 to 0x7E, and a character is taken only while the text so
		/// far stays narrower than the scroll less the prompt and 20 pixels. Until then ours stopped
		/// at 40 characters whatever their width.
		/// </summary>
		public const int TypedAnswerMaxLength = 50;

		/// <summary>wdialog's margin: the text must stay this much narrower than the scroll.</summary>
		public const int TypedAnswerMargin = 20;

		public const string TypedAnswerPrompt = ">";

		/// <summary>Whether one more character may follow psTyped (wdialog's test). Without a font
		/// only the length counts.</summary>
		public static bool CanTypeMore(UWFont pOFont, string psTyped)
		{
			if (psTyped.Length >= TypedAnswerMaxLength)
				return false;

			return pOFont == null || pOFont.MeasureText(psTyped, 0, UWTextLayout.CharacterSpacing)
				< ChoicesWidth - pOFont.MeasureText(TypedAnswerPrompt, 0, UWTextLayout.CharacterSpacing) - TypedAnswerMargin;
		}

		/// <summary>The scroll's line while the answer is typed: the prompt, the text and an underscore
		/// as cursor, as with the name entry when saving.</summary>
		public static string TypedAnswerLine(string psTyped)
		{
			return TypedAnswerPrompt + psTyped + "_";
		}

		// ------------------------------------------------- The trade areas

		/// <summary>
		/// Which slot of a trade area lies under the point (original pixels), -1 if none. ONLY THE
		/// SLOT SQUARE (play_slot_hit_abs and npc_slot_hit_abs, BARTER.C, read 2026-10-15): the cross
		/// beside a slot is a drawn mark, not a button - a click on the item marks it. Until then a
		/// click on the cross toggled the mark too. 17 by 17, both edges included, as the original's
		/// ovr095_52F tests.
		/// </summary>
		public static int TradeSlotAt(bool pbNpc, float pfX, float pfY)
		{
			(int X, int Y)[] lySlots = pbNpc ? UWClassicLayout.Conversation.NpcTradeSlots : UWClassicLayout.Conversation.PlayerTradeSlots;

			for (int liSlot = 0; liSlot < lySlots.Length; liSlot++)
			{
				if (pfX >= lySlots[liSlot].X && pfX < lySlots[liSlot].X + UWClassicLayout.Conversation.TradeSlotClickSize
					&& pfY >= lySlots[liSlot].Y && pfY < lySlots[liSlot].Y + UWClassicLayout.Conversation.TradeSlotClickSize)
					return liSlot;
			}

			return -1;
		}

		/// <summary>A trade item's picture, centred in its circle: its top left.</summary>
		public static (int X, int Y) TradeItemPlace((int X, int Y) pySlot, int piWidth, int piHeight)
		{
			return (pySlot.X + ((UWClassicLayout.Conversation.TradeSlotSize - piWidth) / 2),
				pySlot.Y + ((UWClassicLayout.Conversation.TradeSlotSize - piHeight) / 2));
		}

		// ------------------------------------------------- The portraits

		/// <summary>Characters with their own face in CHARHEAD.GR.</summary>
		public const int LastNamedPortrait = 28;

		/// <summary>Female bodies lie in HEADS.GR five places after the male ones.
		/// </summary>
		public const int FemalePortraitOffset = 5;

		/// <summary>The partner's portrait: characters with their own face (whoami 1 to 28) from
		/// CHARHEAD.GR, all others from GENHEAD.GR by their object number (reference:
		/// conversationinitialisation.NPCPortrait and SetupConversationUI).</summary>
		public static void PartnerPortrait(int piSlot, UWNpc pONpc, out UWTexture.TextureTypes peType, out int piIndex)
		{
			if (piSlot > 0 && piSlot <= LastNamedPortrait)
			{
				peType = UWTexture.TextureTypes.CHARHEAD;
				piIndex = piSlot - 1;
			}
			else
			{
				peType = UWTexture.TextureTypes.GENHEAD;
				piIndex = pONpc != null ? (pONpc.ID & 0x3F) : 0;
			}
		}

		/// <summary>The player character's portrait in HEADS.GR, by body type and gender.</summary>
		public static int PlayerPortrait(UWPlayerData pOPlayer)
		{
			return pOPlayer != null ? pOPlayer.Body + (pOPlayer.IsFemale ? FemalePortraitOffset : 0) : 0;
		}

		// ------------------------------------------------- Colours

		/// <summary>Who a line of the history belongs to; Ui is the interface's own hint ([MORE]).</summary>
		public enum RoleEnum
		{
			Npc = 0,
			Player = 1,
			Narrator = 2,
			Ui = 3
		}

		/// <summary>The role of a history entry: partner, player or narrator.</summary>
		public static RoleEnum RoleOf(UWConversationSession.Line pOLine)
		{
			if (pOLine.IsNpc)
				return RoleEnum.Npc;

			return pOLine.IsPlayer ? RoleEnum.Player : RoleEnum.Narrator;
		}

		/// <summary>The classic colour of a role: the parchment's text (CONV.BYT's sample, index 0x2E),
		/// the player's own answers reddish brown (reference: MessageDisplay PC_SAY #883E14), the
		/// narrator (print) black as in the reference, UI hints white like [MORE] in the message
		/// box.</summary>
		public static UWColor32 ClassicColour(RoleEnum peRole)
		{
			switch (peRole)
			{
				case RoleEnum.Player:
					return UWClassicLayout.Conversation.PlayerText;
				case RoleEnum.Narrator:
					return UWClassicLayout.Conversation.NarratorText;
				case RoleEnum.Ui:
					return UWClassicLayout.Conversation.UiText;
				default:
					return UWClassicLayout.Conversation.NpcText;
			}
		}
	}
}
