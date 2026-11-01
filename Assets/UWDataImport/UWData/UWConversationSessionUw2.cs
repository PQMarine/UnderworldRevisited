using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S BUILT-INS (UW2 plan phase 3 step 7), as Underworld Exhumed's uw2 conv/BABLHACK.C and
	/// conv/CONVERSE.C have them. What the record holds is done here (the quests, the numbered variables, the X
	/// clocks, the skills set outright); teleports and the portrait are noted for the conversation screen, and
	/// the built-ins whose rules come in phase 4 write what they would do into RulesToCome.
	/// </summary>
	public partial class UWConversationSession
	{
		/// <summary>babl_hack's and the skills' answers while their rules are not built.</summary>
		public readonly List<string> RulesToCome = new List<string>();

		/// <summary>teleport_player: done when the conversation is over (do_babl_teleport).</summary>
		public bool HasPlayerTeleport { get; private set; }

		public int TeleportX { get; private set; }

		public int TeleportY { get; private set; }

		public int TeleportLevel { get; private set; }

		private UWPlayerFile fFileUw2 => mOGame != null ? mOGame.PlayerFileUw2 : null;

		/// <summary>x_skills: above this spends a skill point.</summary>
		private const int SkillAdvance = 10000;

		/// <summary>x_skills sets a skill up to this.</summary>
		private const int MaxSkillUw2 = 30;

		/// <summary>x_traps sets a variable up to this.</summary>
		private const int MaxVariableUw2 = 0x3FF;

		/// <summary>x_clock reads above this.</summary>
		private const int ClockReadAbove = 0x100;

		/// <summary>babl_hack's modes that need the record only.</summary>
		private const int HackRunningAway = 1;

		private const int HackTakeDebt = 3;

		private const int HackInPits = 4;

		private const int HackSpeech = 6;

		private const int HackScaleGreed = 8;

		private const int HackFudge = 9;

		private const int HackSignetRing = 10;

		private const int JospurDebtByte = 5;

		internal static void RegisterFunctionsUw2(UWConversationFunctionTable pOTable)
		{
			const UWConversationFunctionTable.Provider leSession = UWConversationFunctionTable.Provider.Session;

			pOTable
				.Register("sex", leSession, (pOSession, pOCall) => pOCall.Argument(pOSession.fIsFemale ? 1 : 0))
				.Register("x_skills", leSession, (pOSession, pOCall) => pOSession.fXSkillsUw2(pOCall.Argument(0), pOCall.Argument(1)))
				.Register("x_traps", leSession, (pOSession, pOCall) =>
				{
					int liValue = pOCall.Argument(1);

					if (liValue >= 0 && liValue <= MaxVariableUw2)
						UWPlayerRecordUw2.SetNumbered(pOSession.fFileUw2, pOCall.Argument(0), liValue);

					return UWPlayerRecordUw2.GetNumbered(pOSession.fFileUw2, pOCall.Argument(0));
				})
				.Register("x_clock", leSession, (pOSession, pOCall) => pOSession.fXClock(pOCall.Argument(0), pOCall.Argument(1)))
				.Register("x_exp", leSession, (pOSession, pOCall) =>
				{
					pOSession.fNote("x_exp " + pOCall.Argument(0));

					return pOSession.fFileUw2 != null ? (int)(UWPlayerRecordUw2.Experience(pOSession.fFileUw2) >> 4) : 0;
				})
				.Register("babl_hack", leSession, (pOSession, pOCall) => pOSession.fBablHack(pOCall))

				// pause(n): scroll_wait(n * 0x1F4). The machine runs on to the next question here, so the wait
				// would come after what is said behind it; the text stands at once.
				.Register("pause", leSession, (pOSession, pOCall) => 1)
				.Register("set_sequence", leSession, (pOSession, pOCall) =>
				{
					pOSession.fNote("set_sequence " + pOCall.Argument(0) + " " + pOCall.Argument(1) + " " + pOCall.Argument(2));

					return 0;
				})
				.Register("transform_talker", leSession, (pOSession, pOCall) =>
				{
					pOSession.fNote("transform_talker");

					return 0;
				})
				.Register("teleport_talker", leSession, (pOSession, pOCall) =>
				{
					pOSession.fNote("teleport_talker " + pOCall.Argument(0) + " " + pOCall.Argument(1));

					return 1;
				})
				.Register("teleport_player", leSession, (pOSession, pOCall) =>
				{
					pOSession.HasPlayerTeleport = true;
					pOSession.TeleportX = pOCall.Argument(0);
					pOSession.TeleportY = pOCall.Argument(1);
					pOSession.TeleportLevel = pOCall.Argument(2);

					return 0;
				})
				.Register("switch_pic", leSession, (pOSession, pOCall) =>
				{
					// In the history: the machine runs on to the next question, and the partner changes between lines.
					pOSession.mOLines.Add(new Line { Picture = pOCall.Argument(0) });

					return 1;
				});
		}

		/// <summary>args[-n] of a built-in: the argument pushed n-th from the last.</summary>
		private static int fFromEnd(UWConversationFunctionTable.Call pOCall, int piFromEnd)
		{
			return pOCall.Argument(pOCall.Arguments.Count - piFromEnd);
		}

		private void fNote(string psCall)
		{
			if (!RulesToCome.Contains(psCall))
				RulesToCome.Add(psCall);
		}

		/// <summary>x_skills: the character's rules come in phase 4; until then only the setting and the
		/// reading are done.</summary>
		private int fXSkillsUw2(int piSkill, int piValue)
		{
			if (piValue > SkillAdvance)
			{
				fNote("x_skills, a skill point on " + piSkill);

				return 0;
			}

			if (piValue == SkillAdvance)
				fNote("x_skills, get_skill " + piSkill);
			else if (piValue >= 0 && piValue <= MaxSkillUw2 && mIHost != null)
				mIHost.SetPlayerSkillByNumber(piSkill, piValue);

			return mIHost != null ? mIHost.GetPlayerSkillByNumber(piSkill) : 0;
		}

		private int fXClock(int piClock, int piValue)
		{
			if (fFileUw2 == null)
				return 0;

			if (piValue > ClockReadAbove)
				return UWPlayerRecordUw2.XClock(fFileUw2, piClock);

			UWPlayerRecordUw2.SetXClock(fFileUw2, piClock, piValue);

			return 0;
		}

		/// <summary>babl_hack's odd jobs: modes 0 and 2 (the pit fights), 5 (loners) and 7 (a wand's charges) come
		/// with their rules.</summary>
		private int fBablHack(UWConversationFunctionTable.Call pOCall)
		{
			int liMode = pOCall.Arguments.Count > 0 ? pOCall.Arguments[pOCall.Arguments.Count - 1] : 0;

			switch (liMode)
			{
				case HackRunningAway:
				case HackSpeech:
					return 0;

				case HackTakeDebt:
				{
					int liDebt = fFileUw2 != null ? fFileUw2.Record[UWPlayerRecordUw2.QuestBytesOffset + JospurDebtByte] : 0;

					if (fFileUw2 != null)
						fFileUw2.Record[UWPlayerRecordUw2.QuestBytesOffset + JospurDebtByte] = 0;

					return liDebt;
				}

				case HackScaleGreed:
					return Trade != null ? Trade.ScaleGreed(fFromEnd(pOCall, 2)) : 0;

				case HackFudge:
					return Trade != null ? Trade.SetFudge(fFromEnd(pOCall, 2)) : 0;

				case HackInPits:
					return UWPlayerRecordUw2.IsInPits(fFileUw2) ? 1 : 0;

				case HackSignetRing:
					fNote("babl_hack, the signet ring");

					return 0;
			}

			fNote("babl_hack " + liMode);

			return 0;
		}
	}
}
