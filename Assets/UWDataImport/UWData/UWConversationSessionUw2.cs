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

		/// <summary>teleport_talker: the talker's tile, done when the conversation is over (do_babl_teleport).</summary>
		public bool HasTalkerTeleport { get; private set; }

		public int TalkerTeleportX { get; private set; }

		public int TalkerTeleportY { get; private set; }

		private UWLevel fLevelUw2 => mIHost != null && mIHost.World != null ? mIHost.World.Level : null;

		/// <summary>do_babl_teleport's second half (BABLHACK.C:318): teleport_critter on the talker, on the player's level.
		/// False when it stays where it is (no room, a solid tile) or nothing was asked.</summary>
		public bool DoTalkerTeleport()
		{
			if (!HasTalkerTeleport || mONpc == null || mIHost == null)
				return false;

			HasTalkerTeleport = false;

			bool lbFlier = mOData != null && mOData.ObjectClassProperties != null
				&& mOData.ObjectClassProperties.TryGetCritter(mONpc.ID, out UWObjectClassProperties.Critter lOCritter) && lOCritter.IsFlier;

			return UWConversationRulesUw2.TeleportCritter(mIHost.World, mONpc, TalkerTeleportX, TalkerTeleportY, lbFlier);
		}

		private void fTransformTalker(int piItem, int piWhoAmI, int piPowerful, int piTerrain)
		{
			if (mONpc == null)
				return;

			UWConversationRulesUw2.TransformCreature(mONpc, piItem, piWhoAmI, piPowerful, piTerrain,
				mOData != null ? mOData.ObjectClassProperties : null, mIHost != null ? mIHost.World : null);

			if (mIHost != null && mIHost.World != null)
				mIHost.World.RedrawObject(mONpc);
		}

		private UWPlayerFile fFileUw2 => mOGame != null ? mOGame.PlayerFileUw2 : null;

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
				// x_exp(n): player_get_exp(n) - halved as every gain of the second game (UWPlayerVariantUw2) - and the
				// total / 16 back (BABLHACK.C:276, G5 part 1).
				.Register("x_exp", leSession, (pOSession, pOCall) =>
				{
					if (pOSession.mIHost != null)
						pOSession.mIHost.ChangeExperience(pOCall.Argument(0), pOSession.fDungeonLevel);

					return pOSession.fFileUw2 != null ? (int)(UWPlayerRecordUw2.Experience(pOSession.fFileUw2) >> 4) : 0;
				})
				.Register("babl_hack", leSession, (pOSession, pOCall) => pOSession.fBablHack(pOCall))

				// pause(n): scroll_wait(n * 0x1F4). The machine runs on to the next question here, so the wait
				// would come after what is said behind it; the text stands at once.
				.Register("pause", leSession, (pOSession, pOCall) => 1)
				// set_sequence(whoami, sequence, frame): the first such creature's animation (G5 part 2).
				.Register("set_sequence", leSession, (pOSession, pOCall) =>
				{
					UWConversationRulesUw2.SetSequence(pOSession.fLevelUw2, pOCall.Argument(0), pOCall.Argument(1), pOCall.Argument(2));

					return 0;
				})

				// transform_talker(item, whoami, powerful, terrain): the talker becomes another creature, drawn anew (G5 part 2).
				.Register("transform_talker", leSession, (pOSession, pOCall) =>
				{
					pOSession.fTransformTalker(pOCall.Argument(0), pOCall.Argument(1), pOCall.Argument(2), pOCall.Argument(3));

					return 0;
				})

				// teleport_talker(x, y): noted; do_babl_teleport moves the talker when the conversation is over (G5 part 2).
				.Register("teleport_talker", leSession, (pOSession, pOCall) =>
				{
					pOSession.HasTalkerTeleport = true;
					pOSession.TalkerTeleportX = pOCall.Argument(0);
					pOSession.TalkerTeleportY = pOCall.Argument(1);

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

		/// <summary>x_skills(skill, value) (BABLHACK.C:402, G5 part 1): above 10000 a skill point on grant_skill_advance,
		/// 1 when it rose; 10000 get_skill; 0 to 30 set; else read. The skill's value back but for the point.</summary>
		private int fXSkillsUw2(int piSkill, int piValue)
		{
			if (mIHost == null)
				return 0;

			switch (UWConversationRulesUw2.SkillCall(piValue))
			{
				case UWConversationRulesUw2.SkillCallEnum.AdvanceByPoint:
					return mIHost.AdvancePlayerSkillByPoint(piSkill) ? 1 : 0;

				case UWConversationRulesUw2.SkillCallEnum.RaiseWithoutPoint:
					mIHost.TryIncreasePlayerSkill(piSkill);
					break;

				case UWConversationRulesUw2.SkillCallEnum.Set:
					mIHost.SetPlayerSkillByNumber(piSkill, piValue);
					break;
			}

			return mIHost.GetPlayerSkillByNumber(piSkill);
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

		/// <summary>babl_hack's odd jobs; the pit fights (0, 1, 2) are UWArenaRulesUw2's (G6 part 4).</summary>
		private int fBablHack(UWConversationFunctionTable.Call pOCall)
		{
			int liMode = pOCall.Arguments.Count > 0 ? pOCall.Arguments[pOCall.Arguments.Count - 1] : 0;

			switch (liMode)
			{
				case HackSpeech:
					return 0;

				// The pits (G6 part 4, UWArenaRulesUw2): the challenge, running away, a fight's opponents.
				case UWArenaRulesUw2.HackChallenge:
				{
					UWLevel lOLevel = mIHost != null && mIHost.World != null ? mIHost.World.Level : null;

					UWArenaRulesUw2.Challenge(fFileUw2, mONpc != null && lOLevel != null ? System.Math.Max(0, UWAreaWalk.MobileIndexOf(lOLevel, mONpc)) : 0);

					return 0;
				}

				case HackRunningAway:
					return UWArenaRulesUw2.TakeRunningAway();

				case UWArenaRulesUw2.HackSetUpFight:
				{
					IUWWorld lIWorld = mIHost != null ? mIHost.World : null;

					return UWArenaRulesUw2.SetUpFight(fFileUw2, fFromEnd(pOCall, 4), fFromEnd(pOCall, 3), fFromEnd(pOCall, 2),
						(piPower, piX, piY) => UWArenaRulesUw2.PlacePitFighter(lIWorld, piPower, piX, piY));
				}

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

				// The Guardian's signet ring on either hand (G5 part 1).
				case UWConversationRulesUw2.HackSignetRing:
				{
					UWInventoryModel lOInventory = mIHost != null && mIHost.World != null && mIHost.World.Player != null
						? mIHost.World.Player.Inventory : null;

					return lOInventory != null && UWConversationRulesUw2.WearsSignetRing(lOInventory.GetEquipped(UWArmorItemMap.BodySlot.RightRing),
						lOInventory.GetEquipped(UWArmorItemMap.BodySlot.LeftRing)) ? 1 : 0;
				}

				// The first creature with that whoami a loner: Nelson's Patterson (G5 part 2).
				case UWConversationRulesUw2.HackLoner:
					UWConversationRulesUw2.MakeLoner(fLevelUw2, fFromEnd(pOCall, 2));

					return 0;

				// useNSpellCharges(thing, n) on a carried thing: Merzan's recharge (G5 part 1).
				case UWConversationRulesUw2.HackWandCharges:
					return UWConversationRulesUw2.ChangeCharges(Trade != null ? Trade.Resolve(fFromEnd(pOCall, 2)) : null, fFromEnd(pOCall, 3));
			}

			fNote("babl_hack " + liMode);

			return 0;
		}
	}
}
