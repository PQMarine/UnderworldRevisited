using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>The gamepad presses of a frame the owners of the pad's press tell apart.</summary>
	[System.Flags]
	public enum UWPadPress
	{
		None = 0,

		/// <summary>Any button of the pad (UWGamepad.AnyPressed without the sticks) - what the places
		/// that wait for "any key" read.</summary>
		Button = 1,

		/// <summary>The button of the PadClick act's first binding (A by default), read on the pad
		/// (UWInput.PadPressed) - the prompts' "take" and "yes".</summary>
		Click = 2,

		/// <summary>The button of the PadContext act's first binding (X by default) - the count prompt's
		/// "all".</summary>
		Context = 4,

		/// <summary>The button of the PadClick or PadContext act's second binding (RT or LT by default) -
		/// the count prompt's "one" and "all" as the mouse's buttons there (per user, 2026-10-25).</summary>
		Triggers = 8
	}

	/// <summary>The places that wait for a pad press, held when the frame begins (UWPadOwners, the Unity
	/// side, gathers them once a frame before every other script).</summary>
	[System.Flags]
	public enum UWPadPressState
	{
		None = 0,

		/// <summary>A window picture is up and Interaction gets to it (Interaction.PictureTakesKeys).</summary>
		WindowPicture = 1,

		/// <summary>A long text waits for its next page and Interaction gets to it
		/// (Interaction.PagingTakesKeys).</summary>
		Paging = 2,

		/// <summary>A conversation's "[MORE]" waits for any key or button (UWConversationController).</summary>
		ConversationMore = 4,

		/// <summary>A finished conversation's last line waits for any key or button before it fades
		/// (UWConversationController.fCloseWithFade).</summary>
		ConversationEnd = 8,

		/// <summary>The classic "how many" prompt is open (UWItemDrag.IsAskingCount).</summary>
		CountPrompt = 16,

		/// <summary>The classic yes/no question is open and the pad's buttons answer it
		/// (Interaction.IsAskingYesNo, IUWUiScheme's PadButtonsAnswerYesNo).</summary>
		YesNo = 32,

		/// <summary>The modern bags' split box is open and the bags get to it (UWModernBags).</summary>
		BagSplit = 64
	}

	/// <summary>Who owns a frame's pad press, the places that wait for one first, the pad's pointer last.</summary>
	public enum UWPadPressOwner
	{
		WindowPicture,
		Paging,
		ConversationMore,
		ConversationEnd,
		CountPrompt,
		YesNo,
		BagSplit,

		/// <summary>The pad's pointer (UWGamepadPointer): it clicks with the press only when no owner
		/// above took it.</summary>
		Pointer
	}

	/// <summary>One owner of the pad's press: the state it needs, the presses it takes, and whether the
	/// back chain gives it the pad's B (UWBackChain, its own owner there).</summary>
	public sealed class UWPadPressRow
	{
		public readonly UWPadPressOwner Owner;

		public readonly UWPadPressState State;

		public readonly UWPadPress Takes;

		/// <summary>The back chain's owner whose pad B (UWBackKey.PadButton) this row takes as well;
		/// HasBack false for none.</summary>
		public readonly UWBackOwner BackOwner;

		public readonly bool HasBack;

		public UWPadPressRow(UWPadPressOwner peOwner, UWPadPressState peState, UWPadPress peTakes)
		{
			Owner = peOwner;
			State = peState;
			Takes = peTakes;
		}

		public UWPadPressRow(UWPadPressOwner peOwner, UWPadPressState peState, UWPadPress peTakes, UWBackOwner peBackOwner)
			: this(peOwner, peState, peTakes)
		{
			BackOwner = peBackOwner;
			HasBack = true;
		}
	}

	/// <summary>
	/// WHO OWNS A PRESS (cleanup step 7.8, 2026-10-25; section 8.5 of the cleanup plan): the latches
	/// that said "this press is somebody else's" replaced by owners decided in one place.
	///
	///   - THE PAD'S PRESS (UWGamepad.ConsumePress until then: each place that took a pad press set a
	///     frame mark, and the pad's pointer, the last script of the frame, sent no click for it): the
	///     table below - the places that wait for a pad press, with the state each needs (held when the
	///     frame begins) and the presses each takes; the pointer clicks only when no row holds and takes
	///     one of the frame's presses. The places themselves read the pad as before.
	///   - THE PAD'S POINTER'S BUTTONS (UWGamepadPointer.Takes): the PadPointer context
	///     (UWInputContexts.PadPointerTakes), put by PadPointerContext once a frame.
	///   - THE PRESS THAT SWITCHED THE MODERN POINTER (UWModernPointer.SwitchedFrame): a press hold
	///     (UWPressHold) the context change puts, which the readers of that press ask.
	/// </summary>
	public static class UWPressOwners
	{
		/// <summary>
		/// THE PAD PRESS'S OWNERS, as each took the press before (UWGamepad.ConsumePress), in the order
		/// the frame reached them (Interaction's: the yes/no question, the paging, the window picture;
		/// then the conversation, the classic drag's count prompt, the modern bags' split box):
		///   - the window picture, the paging and the conversation's "[MORE]" and end: any button;
		///   - the count prompt: its take (A), all (X), one and all as the mouse's buttons (RT, LT; per
		///     user 2026-10-25 - the pointer clicked with the same RT once the prompt was over and put the
		///     taken count straight back) and its back (B, as the back chain says);
		///   - the classic yes/no: yes (A) and no (B, as the back chain says);
		///   - the split box: its take (A) and its back (B, as the back chain says).
		/// The conversation's end is new as an owner: its wait ran as a coroutine, after every Update,
		/// so the pointer had clicked with the press already (cleanup step 7.8 decided: the end takes it).
		/// </summary>
		public static readonly UWPadPressRow[] PadRows =
		{
			new UWPadPressRow(UWPadPressOwner.YesNo, UWPadPressState.YesNo, UWPadPress.Click, UWBackOwner.YesNo),
			new UWPadPressRow(UWPadPressOwner.Paging, UWPadPressState.Paging, UWPadPress.Button),
			new UWPadPressRow(UWPadPressOwner.WindowPicture, UWPadPressState.WindowPicture, UWPadPress.Button),
			new UWPadPressRow(UWPadPressOwner.ConversationMore, UWPadPressState.ConversationMore, UWPadPress.Button),
			new UWPadPressRow(UWPadPressOwner.ConversationEnd, UWPadPressState.ConversationEnd, UWPadPress.Button),
			new UWPadPressRow(UWPadPressOwner.CountPrompt, UWPadPressState.CountPrompt, UWPadPress.Click | UWPadPress.Context | UWPadPress.Triggers, UWBackOwner.CountPrompt),
			new UWPadPressRow(UWPadPressOwner.BagSplit, UWPadPressState.BagSplit, UWPadPress.Click, UWBackOwner.BagSplit)
		};

		/// <summary>Whose the frame's pad press is: the first row whose state holds and that takes one
		/// of the presses (or the pad's B, when the back chain gives it to its back owner -
		/// pOBackTakes, asked only then); the pointer when none.</summary>
		public static UWPadPressOwner PadOwner(UWPadPress pePresses, UWPadPressState peStates, System.Func<UWBackOwner, bool> pOBackTakes)
		{
			foreach (UWPadPressRow lORow in PadRows)
			{
				if ((peStates & lORow.State) == 0)
					continue;

				if ((pePresses & lORow.Takes) != 0)
					return lORow.Owner;

				if (lORow.HasBack && pOBackTakes != null && pOBackTakes(lORow.BackOwner))
					return lORow.Owner;
			}

			return UWPadPressOwner.Pointer;
		}

		/// <summary>The pad's pointer may click with this frame's press: nobody above owns it.</summary>
		public static bool PointerClicks(UWPadPress pePresses, UWPadPressState peStates, System.Func<UWBackOwner, bool> pOBackTakes)
		{
			return PadOwner(pePresses, peStates, pOBackTakes) == UWPadPressOwner.Pointer;
		}

		/// <summary>The table as text, a row a line (the self-check prints it).</summary>
		public static string DescribePadRows()
		{
			StringBuilder lOText = new StringBuilder();

			foreach (UWPadPressRow lORow in PadRows)
			{
				lOText.Append(lORow.Owner).Append(": when ").Append(lORow.State).Append(", takes ").Append(lORow.Takes);

				if (lORow.HasBack)
					lOText.Append(" and B for ").Append(lORow.BackOwner);

				lOText.Append('\n');
			}

			return lOText.ToString();
		}

		/// <summary>
		/// THE PAD'S POINTER DRIVES (UWGamepadPointer, its rule moved here unchanged with cleanup step
		/// 7.8): not while the modern look holds the pointer (pbLookLocked), the letter grid is up, the
		/// count prompt asks or the classic yes/no is answered with the pad's buttons (pbYesNoOnPad);
		/// otherwise when R3 switched it on (pbOn), the modern pointer is free (pbModernFree) or a window
		/// holds the world - a menu, the options, a conversation, or no scheme at all (pbHeldByWindow).
		/// pbFree: nothing else has the pad's buttons.
		/// </summary>
		public static bool PadPointerDrives(bool pbLookLocked, bool pbLetterGrid, bool pbAskingCount, bool pbYesNoOnPad,
			bool pbOn, bool pbModernFree, bool pbHeldByWindow, out bool pbFree)
		{
			pbFree = false;

			if (pbLookLocked || pbLetterGrid || pbAskingCount || pbYesNoOnPad)
				return false;

			pbFree = true;

			return pbOn || pbModernFree || pbHeldByWindow;
		}

		/// <summary>
		/// THE PADPOINTER CONTEXT (UWGamepadPointer.ClicksActive): the pad's mouse buttons work while the
		/// pointer drives, and in the classic scheme whenever nothing else has them (its pointer is free
		/// anyway; per user, 2026-10-07).
		/// </summary>
		public static UWInputContext PadPointerContext(bool pbDrives, bool pbFree, bool pbScheme, bool pbModern)
		{
			return pbDrives || (pbFree && pbScheme && !pbModern) ? UWInputContext.PadPointer : UWInputContext.None;
		}
	}

	/// <summary>
	/// THE PRESS A CONTEXT CHANGE TOOK (cleanup step 7.8; UWModernPointer.SwitchedFrame until then):
	/// the right button that freed or locked the modern pointer uses nothing else in that frame. The
	/// change holds its press for the frame it came in; the readers of that press ask Holds. The frame
	/// number is passed in (Time.frameCount on the Unity side, UWInput).
	/// </summary>
	public sealed class UWPressHold
	{
		private int miFrame = -1;

		private readonly List<UWAct> mOActs = new List<UWAct>();

		/// <summary>The press of peAct changed the context in frame piFrame: held for that frame.</summary>
		public void Hold(UWAct peAct, int piFrame)
		{
			if (miFrame != piFrame)
			{
				miFrame = piFrame;
				mOActs.Clear();
			}

			if (!mOActs.Contains(peAct))
				mOActs.Add(peAct);
		}

		/// <summary>Whether the press of peAct is held in frame piFrame.</summary>
		public bool Holds(UWAct peAct, int piFrame)
		{
			return miFrame == piFrame && mOActs.Contains(peAct);
		}
	}
}
