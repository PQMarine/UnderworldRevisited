using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A running conversation: connects the virtual machine (UWConversationVM) with the
	/// game and keeps the history that the display shows.
	///
	/// This class is the host of the machine. Before the start it fills the imported
	/// game globals from NPC and player, receives the output during the conversation,
	/// and at the end writes back what the NPC should remember - attitude and whether you have
	/// already talked to each other.
	///
	/// ENGINE-FREE since 2026-09-18 (P3 of the engine separation): everything it needs from
	/// the game - the player's numbers, the world, the inventory, item descriptions - comes
	/// through IUWConversationHost. The Unity side implements that in UWConversationHost;
	/// UWConversationScreen only shows what this class keeps.
	/// </summary>
	public class UWConversationSession : UWConversationVM.IHost
	{
		/// <summary>One line of the history.</summary>
		public struct Line
		{
			/// <summary>The NPC speaks. Otherwise it is a message without a speaker, or a
			/// reply the player already chose.</summary>
			public bool IsNpc;

			public bool IsPlayer;

			public string Text;
		}

		private readonly DataImport mOData;

		private readonly UWNpc mONpc;

		private readonly IUWConversationHost mIHost;

		private readonly UWConversations.Conversation mOConversation;

		private readonly UWConversationVM mOVm;

		private readonly List<Line> mOLines = new List<Line>();

		/// <summary>Built-in function "gronk_door", see fGronkDoor.</summary>
		private const int GronkDoorFunction = 0x0025;

		/// <summary>The creature goal "attack" - the same number as UWCritter.GoalAttack on the
		/// Unity side.</summary>
		private const int GoalAttack = UWNpc.GoalAttack;

		/// <summary>
		/// The string number under which the player character's name is found. The conversations
		/// insert it via "@GS8" - the imported global play_name holds a
		/// string number. The reference appends the name to the string block for this; here it is
		/// a number that no block reaches, and fGetConversationString knows it.
		/// </summary>
		private const int PlayerNameString = 30000;

		/// <summary>The trade, see UWConversationTrade. Null without a conversation partner.</summary>
		public UWConversationTrade Trade { get; private set; }

		/// <summary>What has been said so far, oldest line first.</summary>
		public IReadOnlyList<Line> Lines
		{
			get { return mOLines; }
		}

		/// <summary>The reply options currently open. Empty when no
		/// choice is pending.</summary>
		public IReadOnlyList<string> Choices
		{
			get { return mOVm.PendingChoices; }
		}

		public string PartnerName { get; private set; }

		public bool IsFinished { get; private set; }

		/// <summary>Set when the machine aborted. For debugging only.</summary>
		public string Error { get; private set; }

		/// <summary>Functions the conversation called that do not exist yet -
		/// the same list as in the test run, here for observation in game.</summary>
		public readonly List<string> MissingFunctions = new List<string>();

		/// <param name="pIHost">The game around the conversation. May be null for a bare test
		/// run: the conversation then has no player, no world and no inventory.</param>
		public UWConversationSession(DataImport pOData, UWNpc pONpc, int piSlot, IUWConversationHost pIHost)
		{
			mOData = pOData;
			mONpc = pONpc;
			mIHost = pIHost;

			mOConversation = pOData != null && pOData.Conversations != null
				? pOData.Conversations.GetConversation(piSlot)
				: null;

			PartnerName = fGetPartnerName(piSlot);

			// The generic conversations (from 256) have no name in block 7 - the reference
			// then shows the object name, for the wisp "wisp".
			if (string.IsNullOrWhiteSpace(PartnerName) && pONpc != null)
				PartnerName = fGetObjectName(pONpc.ID);

			if (mOConversation == null)
			{
				IsFinished = true;
				Error = "No conversation in slot " + piSlot + ".";

				return;
			}

			mOVm = new UWConversationVM(mOConversation, this,
				piIndex => fGetConversationString(mOConversation.StringBlock, piIndex));

			// First the memory, then the globals on top - like the reference.
			miMemoryWords = pOData.ConversationGlobals != null ? pOData.ConversationGlobals.GetWords(piSlot) : null;
			mOVm.LoadMemory(miMemoryWords);

			fSetGlobals(piSlot);

			// Even without a creature (Garamon's ghost): the player's trade area accepts goods,
			// as in the original (per user, 2026-09-13). The trade functions check the
			// partner for null themselves.
			Trade = new UWConversationTrade(pOData, pONpc, pIHost,
				piString => Say(fGetConversationString(mOConversation.StringBlock, piString)),
				Print, RegisterString, fDescribe);
		}

		/// <summary>The memory of this conversation, see UWConversationGlobals.</summary>
		private int[] miMemoryWords;

		/// <summary>Texts that only come into being during the conversation (identify_inv describes an item),
		/// under numbers above all blocks.</summary>
		private readonly Dictionary<int, string> mODynamicStrings = new Dictionary<int, string>();

		private int miNextDynamicString = PlayerNameString + 1;

		/// <summary>Gives a text a string number that fGetConversationString knows.</summary>
		public int RegisterString(string psText)
		{
			int liNumber = miNextDynamicString++;

			mODynamicStrings[liNumber] = psText ?? string.Empty;

			return liNumber;
		}

		/// <summary>The description of an item without "You see" - for identify_inv.</summary>
		private string fDescribe(UWObject pOItem, int piDetail)
		{
			string lsText = mIHost != null ? mIHost.DescribeItem(pOItem, piDetail) : null;

			if (string.IsNullOrEmpty(lsText))
				return string.Empty;

			const string lsYouSee = "You see ";

			if (lsText.StartsWith(lsYouSee, StringComparison.Ordinal))
				lsText = lsText.Substring(lsYouSee.Length);

			return lsText.TrimEnd('.');
		}

		/// <summary>An item on the player's own side of the trade area: 1 + Lore against 15, rolled
		/// anew and kept nowhere (ovr095_683, see UWItemDescriptions.DetailFromLoreCheck).</summary>
		public void LookAtOwnTradeItem(UWObject pOItem)
		{
			int liLore = mIHost != null ? mIHost.GetPlayerSkill(UWPlayerData.Skill.Lore) : 0;

			LookAt(pOItem, 1 + (int)UWSkillCheck.Check(liLore, UWLoreCheck.OwnTradeTarget));
		}

		/// <summary>An item on the partner's side: 2 on a success of Lore against 20, else 1.</summary>
		public void LookAtPartnerTradeItem(UWObject pOItem)
		{
			int liLore = mIHost != null ? mIHost.GetPlayerSkill(UWPlayerData.Skill.Lore) : 0;

			LookAt(pOItem, UWSkillCheck.IsSuccess(UWSkillCheck.Check(liLore, UWLoreCheck.PartnerTradeTarget)) ? 2 : 1);
		}

		/// <summary>Describes an item of the trade area in the history - the right click there.
		/// </summary>
		public void LookAt(UWObject pOItem, int piDetail = UWItemDescriptions.DetailFromLoreCheck)
		{
			if (pOItem == null || mIHost == null)
				return;

			string lsText = mIHost.DescribeItem(pOItem, piDetail);

			if (string.IsNullOrEmpty(lsText))
				return;

			// Shown for a moment in the answer scroll, not written into the conversation (see
			// UWConversationScreen.ShowLookText) - where there is no such place, as a line.
			if (!mIHost.ShowLookText(lsText))
				Print(lsText);
		}

		/// <summary>Runs the conversation until it needs a reply or ends.</summary>
		public void Advance()
		{
			if (IsFinished || mOVm == null)
				return;

			UWConversationVM.RunState leState = mOVm.Run();

			if (leState == UWConversationVM.RunState.AwaitingChoice)
				return;

			if (leState == UWConversationVM.RunState.AwaitingInput)
			{
				IsAskingText = true;
				return;
			}

			if (leState == UWConversationVM.RunState.Failed)
				Error = mOVm.Error;

			IsFinished = true;

			fWriteBackGlobals();

			if (Trade != null)
				Trade.EndConversation();
		}

		/// <summary>The conversation waits for typed text (babl_ask) - such as a
		/// password or a name. Answered with SupplyText.</summary>
		public bool IsAskingText { get; private set; }

		/// <summary>The typed reply. The original compares in upper case (reference
		/// babl_ask), so it goes into the machine that way; in the history it appears as typed.
		/// </summary>
		public void SupplyText(string psText)
		{
			if (!IsAskingText || mOVm == null)
				return;

			IsAskingText = false;

			string lsText = psText ?? string.Empty;

			mOLines.Add(new Line { IsPlayer = true, Text = lsText });
			mOVm.SupplyTypedInput(RegisterString(lsText.ToUpperInvariant()));

			Advance();
		}

		/// <summary>Answers the open choice, one-based.</summary>
		public void Choose(int piChoice)
		{
			if (IsFinished || mOVm == null || mOVm.PendingChoices.Count == 0)
				return;

			if (piChoice < 1 || piChoice > mOVm.PendingChoices.Count)
				return;

			mOLines.Add(new Line { IsPlayer = true, Text = mOVm.PendingChoices[piChoice - 1] });

			mOVm.SupplyChoice(piChoice);

			Advance();
		}

		/// <summary>Aborts the conversation without playing it to the end.</summary>
		public void Abort()
		{
			if (IsFinished)
				return;

			IsFinished = true;

			fWriteBackGlobals();

			if (Trade != null)
				Trade.EndConversation();
		}

		public void Say(string psText)
		{
			if (fTryReprint(psText))
				return;

			mOLines.Add(new Line { IsNpc = true, Text = psText });
		}

		public void Print(string psText)
		{
			if (fTryReprint(psText))
				return;

			mOLines.Add(new Line { Text = psText });
		}

		/// <summary>
		/// THE REPRINT OF TYPED INPUT: two conversations say "\1@SS1\0" after babl_ask - the
		/// typed text between the scroll's colour codes \1 (the player's colour) and \0 (back to
		/// normal), as the reference notes (conversationio.TextSubstitute). Shown as it was, the codes
		/// stood in the history verbatim and the text twice, as SupplyText already writes the
		/// answer for every babl_ask (per user, 2026-10-04: "\1PIT\0" under "pit", "\1\0" after
		/// Escape). It becomes the player's line - or nothing, when it repeats the answer just
		/// written (the original compares in upper case, so the reprint is upper case).
		/// </summary>
		private bool fTryReprint(string psText)
		{
			if (psText == null || !psText.StartsWith("\\1") || !psText.EndsWith("\\0"))
				return false;

			string lsText = psText.Substring(2, psText.Length - 4);

			if (mOLines.Count > 0 && mOLines[mOLines.Count - 1].IsPlayer
				&& string.Equals(mOLines[mOLines.Count - 1].Text.Trim(), lsText.Trim(), System.StringComparison.OrdinalIgnoreCase))
				return true;

			if (lsText.Trim().Length > 0)
				mOLines.Add(new Line { IsPlayer = true, Text = lsText });

			return true;
		}

		/// <summary>The reference (get_quest) reads flag 36 for every flag from 36 on - that is what the
		/// original does; everything above is not a quest flag.</summary>
		private const int LastQuestFlag = 36;

		public int GetQuest(int piFlag)
		{
			if (piFlag < 0)
				return 0;

			return UWQuestFlags.Get(piFlag > LastQuestFlag ? LastQuestFlag : piFlag);
		}

		public void SetQuest(int piFlag, int piValue)
		{
			UWQuestFlags.Set(piFlag, piValue);
		}

		public int Random(int piMaximum)
		{
			return piMaximum <= 0 ? 0 : UWRandom.Next(1, piMaximum + 1);
		}

		public int CallUnknown(string psName, int piFunctionId, IReadOnlyList<int> pOArguments,
			IReadOnlyList<int> pOAddresses, UWConversationVM pOVm)
		{
			if (piFunctionId == GronkDoorFunction)
				return fGronkDoor(pOArguments);

			if (Trade != null && UWConversationTrade.Handles(psName))
			{
				int liResult = Trade.Call(psName, pOArguments, pOAddresses, pOVm);

				// An accepted deal is carried out at once, as in the reference right
				// after the function - the next function already sees the goods swapped.
				if (Trade.PendingSwap)
					Trade.Swap();

				return liResult;
			}

			switch (psName)
			{
				// sex(male, female): the string number that matches the character. Confirmed by
				// the pairs "lad"/"lass", "son"/"daughter", "He"/"She" in the data - the
				// first is always the male one.
				case "sex":
					return fArgument(pOArguments, mOData != null && mOData.InitialPlayer != null
						&& mOData.InitialPlayer.IsFemale ? 1 : 0);

				// compare(a, b): same text, ignoring case (reference).
				case "compare":
					return string.Equals(fStringOf(fArgument(pOArguments, 0)), fStringOf(fArgument(pOArguments, 1)),
						StringComparison.OrdinalIgnoreCase) ? 1 : 0;

				// contains(keyword, text): the keyword as a whole word in the text
				// (see Contains_ovr093_ED4, read 2026-10-01; until then ours took either text
				// anywhere in the other).
				case "contains":
				{
					// See UWConversationTextRules.
					string lsKeyword = fStringOf(fArgument(pOArguments, 0));
					string lsText = fStringOf(fArgument(pOArguments, 1));

					return UWConversationTextRules.ContainsWord(lsText, lsKeyword) ? 1 : 0;
				}

				case "length":
					return fStringOf(fArgument(pOArguments, 0)).Length;

				case "x_skills":
					return fXSkills(fArgument(pOArguments, 0), fArgument(pOArguments, 1));

				case "x_traps":
					return fXTraps(fArgument(pOArguments, 0), fArgument(pOArguments, 1));

				case "do_input_wait":
					return 0;
			}

			// Trade and inventory access are handled by UWConversationTrade above. For everything
			// else that is not implemented yet: instead of guessing,
			// the call is noted and answered with 0 - that is the value the
			// conversations read as "no, nothing, nobody", so it breaks the least.
			if (!MissingFunctions.Contains(psName))
				MissingFunctions.Add(psName);

			return 0;
		}

		/// <summary>
		/// "gronk_door" (uw-formats.txt 7.6, Id 0x25): opens or closes the door on a
		/// tile. With it an NPC lets the player in or locks him out - for the portcullis
		/// of the goblins on level 1 that is exactly the mechanism.
		///
		/// Arguments in push order: tile X, tile Y, and the mode. UW.EXE (GronkDoor_ovr094_649,
		/// read 2026-10-01): 0 opens (OpenDoor_seg040_352B_21E6), 1 closes (CloseDoor_seg040_352B_22FD),
		/// 2 toggles (DoorToggle_seg040_352B_23B7), anything else does nothing; the result is 1
		/// when the tile holds a door (or a moving one), 0 otherwise. Ours knew only "0 opens,
		/// anything else closes". Conversation 12 opens the goblins' portcullis with 3, 10, 0;
		/// the talking door (conversation 25, level 6, 4/2) closes itself with 1 and opens with 0.
		/// </summary>
		private int fGronkDoor(IReadOnlyList<int> pOArguments)
		{
			if (pOArguments == null || pOArguments.Count < 3 || mIHost == null)
				return 0;

			return mIHost.SetDoorState(pOArguments[0], pOArguments[1], pOArguments[2]) ? 1 : 0;
		}

		public const int GronkDoorOpen = 0;

		public const int GronkDoorClose = 1;

		public const int GronkDoorToggle = 2;

		private static int fArgument(IReadOnlyList<int> pOArguments, int piIndex)
		{
			return pOArguments != null && piIndex < pOArguments.Count ? pOArguments[piIndex] : 0;
		}

		/// <summary>A text by its number - from the conversation's block or from those created
		/// during the conversation (typed replies, see RegisterString).</summary>
		private string fStringOf(int piNumber)
		{
			return mOConversation != null ? fGetConversationString(mOConversation.StringBlock, piNumber) : string.Empty;
		}

		/// <summary>Special value for x_skills and x_traps: in x_skills 10000 raises a skill by
		/// one step and 10001 only queries it (that is how the calls appear in the data, e.g.
		/// with Shak and the cartographer); in x_traps every value from 10000 on only reads.</summary>
		private const int SkillIncrease = 10000;

		/// <summary>The highest skill value - UWCharacter.MaxSkillValue on the Unity side.</summary>
		private const int MaxSkillValue = UWPlayerData.MaxSkillValue;

		/// <summary>x_skills(number, value): sets or raises a skill and returns
		/// its value. The numbers are those of the save game (0 attack, 1 defence, then
		/// the eighteen named ones), as at the shrines.</summary>
		private int fXSkills(int piSkill, int piValue)
		{
			if (mIHost == null || !mIHost.HasPlayer)
				return 0;

			if (piValue == SkillIncrease)
				mIHost.TryIncreasePlayerSkill(piSkill);
			else if (piValue >= 0 && piValue <= MaxSkillValue)
				mIHost.SetPlayerSkillByNumber(piSkill, piValue);

			return mIHost.GetPlayerSkillByNumber(piSkill);
		}

		/// <summary>x_traps(variable, value): sets a game variable (those of the
		/// variable traps, see UWGameVariables) and returns its value; from 10000 on it is only
		/// read.</summary>
		private int fXTraps(int piVariable, int piValue)
		{
			if (piVariable < 0)
				return 0;

			if (piValue >= 0 && piValue < SkillIncrease)
				UWGameVariables.Set(piVariable, piValue);

			return UWGameVariables.Get(piVariable);
		}

		/// <summary>Clock of the original: this many units are one game minute (UW.EXE divides the
		/// clock at 0xCE by 0x3BC4).</summary>
		private const int ClockUnitsPerMinute = 15300;

		private const int MinutesPerDay = 1440;

		/// <summary>npc_health/play_health without a base value in the table.</summary>
		private const int HealthWithoutVitality = 0x80;

		/// <summary>
		/// Fills the imported game globals exactly like UW.EXE
		/// ImportConversationVariables_ovr103_0 (per user, 2026-09-13: compared BGLOBALS of Garamon's
		/// conversation against the original - dungeon_level was fixed at 1, game time,
		/// hunger, level, sex, name and the creature's table values were missing). Which globals
		/// exist is listed in the import table of the respective conversation - only what
		/// appears there is set (UWConversationVM.SetImportedGlobal).
		/// </summary>
		private void fSetGlobals(int piSlot)
		{
			mOVm.SetImportedGlobal("npc_whoami", mONpc != null ? mONpc.NPCwhoami : piSlot);

			mOVm.SetImportedGlobal("dungeon_level", mIHost != null ? mIHost.CurrentLevelNumber : 1);
			mOVm.SetImportedGlobal("play_name", PlayerNameString);
			mOVm.SetImportedGlobal("new_player_exp", 0);

			// npc_name is a string number: with its own conversation the partner name (block 7,
			// whoami + 16), otherwise the object name (block 4). Here a registered text.
			mOVm.SetImportedGlobal("npc_name", RegisterString(mONpc == null || mONpc.NPCwhoami != 0
				? PartnerName
				: fGetObjectName(mONpc.ID)));

			if (mONpc != null)
			{
				UWObjectClassProperties.Critter lOStats = default;
				bool lbHasStats = mOData != null && mOData.ObjectClassProperties != null
					&& mOData.ObjectClassProperties.TryGetCritter(mONpc.ID, out lOStats);

				bool lbHungerBit = mONpc.RawNpcBytes != null && mONpc.RawNpcBytes.Length > 17
					&& (mONpc.RawNpcBytes[17] & 0x80) != 0;

				mOVm.SetImportedGlobal("npc_hunger", lbHungerBit ? 0x10 : 0xC0);
				mOVm.SetImportedGlobal("npc_health", lbHasStats && lOStats.Vitality != 0
					? fHealth(mONpc.HitPoints, lOStats.Vitality)
					: HealthWithoutVitality);
				mOVm.SetImportedGlobal("npc_hp", mONpc.HitPoints);
				mOVm.SetImportedGlobal("npc_arms", lbHasStats ? (sbyte)lOStats.Weapon : 0);
				mOVm.SetImportedGlobal("npc_power", lbHasStats ? lOStats.Power + lOStats.SpellPower : 0);
				mOVm.SetImportedGlobal("npc_goal", mONpc.NPCGoal);
				mOVm.SetImportedGlobal("npc_gtarg", mONpc.NPCGTarg);
				mOVm.SetImportedGlobal("npc_talkedto", mONpc.NPCTalkedTo ? 1 : 0);
				mOVm.SetImportedGlobal("npc_level", lbHasStats ? lOStats.ConversationLevel : 0);

				// The home is stored in Quality and Owner (see UWCritter.Initialise).
				mOVm.SetImportedGlobal("npc_xhome", mONpc.Quality & 0x3F);
				mOVm.SetImportedGlobal("npc_yhome", mONpc.Owner & 0x3F);

				// Whoever is currently attacking the player (goal 5, target 1) counts as hostile, an
				// ally reads as 6.
				int liAttitude = mONpc.NPCGoal == GoalAttack && mONpc.NPCGTarg == 1
					? 0
					: mONpc.NPCIsAlly ? 6 : mONpc.NPCAttitude;

				mOVm.SetImportedGlobal("npc_attitude", liAttitude);
			}

			if (mIHost != null && mIHost.HasPlayer)
			{
				int liHp = mIHost.PlayerHitPoints;
				int liMana = mIHost.PlayerMana;
				int liMaxHp = mIHost.PlayerMaxHitPoints;

				// UW.EXE divides by the table slot of the player object (127). The game fills it
				// at runtime with the maximum vitality - in the comparison (SAVE1 against
				// SAVE2, 2026-09-13) 97 of 97 gave exactly 256.
				mOVm.SetImportedGlobal("play_hunger", mIHost.PlayerHunger);
				mOVm.SetImportedGlobal("play_health", liMaxHp != 0
					? fHealth(liHp, liMaxHp)
					: HealthWithoutVitality);
				mOVm.SetImportedGlobal("play_hp", liHp);
				mOVm.SetImportedGlobal("play_arms", mIHost.PlayerAttack + mIHost.PlayerStrength);
				mOVm.SetImportedGlobal("play_power", mIHost.PlayerDexterity + liMana
					+ mIHost.GetPlayerSkill(UWPlayerData.Skill.Missile));
				mOVm.SetImportedGlobal("play_mana", liMana);
				mOVm.SetImportedGlobal("play_level", mIHost.PlayerLevel);

				int liMinutes = mIHost.PlayerClock / ClockUnitsPerMinute;

				mOVm.SetImportedGlobal("game_time", liMinutes);
				mOVm.SetImportedGlobal("game_mins", liMinutes % MinutesPerDay);
				mOVm.SetImportedGlobal("game_days", liMinutes / MinutesPerDay);
				mOVm.SetImportedGlobal("play_poison", mIHost.PlayerPoison & 0xF);
				mOVm.SetImportedGlobal("play_drawn", mIHost.PlayerWeaponDrawn ? 1 : 0);
			}

			UWPlayerData lOPlayerData = mOData != null ? mOData.InitialPlayer : null;

			mOVm.SetImportedGlobal("play_sex", lOPlayerData != null && lOPlayerData.IsFemale ? 1 : 0);

			// The private globals outlive the conversation: they come from the memory loaded
			// before this (UWConversationGlobals, zeros in a new game) and are written back in
			// fWriteBackGlobals.
		}

		/// <summary>Health in signed 16-bit arithmetic like UW.EXE: (hp shl 8) as a
		/// signed word, divided as integers. Hit points from 128 on become
		/// negative - Garamon (234 of 40) reads as -140 in the original.</summary>
		private static int fHealth(int piHp, int piBase)
		{
			return (short)((piHp & 0xFF) << 8) / piBase;
		}

		private string fGetObjectName(int piObjectId)
		{
			try
			{
				string lsName = mOData.Strings.Blocks[4].Strings[piObjectId + 1].TrimEnd('\r', '\n');
				int liCut = lsName.IndexOf('&');

				if (liCut >= 0)
					lsName = lsName.Substring(0, liCut);

				// The article comes before the underscore ("a_wisp") - the name is only what
				// follows (per user, 2026-09-14: the conversation showed "a_wisp").
				int liArticle = lsName.IndexOf('_');

				if (liArticle >= 0)
					lsName = lsName.Substring(liArticle + 1);

				// Capitalised like a name: the original shows "Wisp" (per user, 2026-09-14).
				return lsName.Length > 0 ? char.ToUpperInvariant(lsName[0]) + lsName.Substring(1) : lsName;
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// Writes back like UW.EXE RetrieveImportedVariablesAfterConversation_ovr103_492:
		/// for the partner hunger bit, hit points, home, goal and target, "already talked to"
		/// and attitude; for the player hunger, hit points, mana, poison and the experience from
		/// new_player_exp. Before 2026-09-13 only attitude, goal and "already talked to" - experience
		/// from conversations was lost.
		/// </summary>
		private void fWriteBackGlobals()
		{
			// The conversation's memory - including the imported globals, that is how
			// the reference does it; they are set anew on the next start anyway.
			if (mOVm != null)
				mOVm.SaveMemory(miMemoryWords);

			if (mOVm != null)
				fWriteBackPlayer();

			if (mONpc == null)
				return;

			mONpc.NPCTalkedTo = true;

			if (mOVm == null)
				return;

			// ONLY WHAT THE CONVERSATION IMPORTS. What it does not know reads as zero - and
			// zero is the hostile attitude: Bragit attacked after the conversation (per user,
			// 2026-09-11) because his attitude was overwritten that way.
			int liValue;

			// Hunger: below 0x20 sets bit 7 of byte 0x19. A copy made from a template shares
			// the raw bytes with this one (UWObject.Clone) - so copy first.
			if (mOVm.TryGetImportedGlobal("npc_hunger", out liValue) && mONpc.RawNpcBytes != null
				&& mONpc.RawNpcBytes.Length > 17)
			{
				byte[] lyRaw = (byte[])mONpc.RawNpcBytes.Clone();

				lyRaw[17] = (byte)((lyRaw[17] & 0x7F) | ((short)liValue < 0x20 ? 0x80 : 0));
				mONpc.RawNpcBytes = lyRaw;
			}

			if (mOVm.TryGetImportedGlobal("npc_hp", out liValue))
				mONpc.HitPoints = (byte)(liValue & 0xFF);

			if (mOVm.TryGetImportedGlobal("npc_xhome", out liValue))
				mONpc.Quality = (ushort)(liValue & 0x3F);

			if (mOVm.TryGetImportedGlobal("npc_yhome", out liValue))
				mONpc.Owner = (ushort)(liValue & 0x3F);

			// An attitude above 3 means "follow me": friendly and allied (reference:
			// ConversationVM.ExportVariables).
			if (mOVm.TryGetImportedGlobal("npc_attitude", out liValue))
			{
				if (liValue > 3)
				{
					mONpc.NPCAttitude = 3;
					mONpc.NPCIsAlly = true;
				}
				else
					mONpc.NPCAttitude = (byte)Math.Max(0, Math.Min(3, liValue));
			}

			// The goal too: a conversation can mean "follow me" or "stay here" (since
			// 2026-09-11, the creature reads it in afterwards - see UWCritter.RefreshFromData).
			if (mOVm.TryGetImportedGlobal("npc_goal", out liValue))
				mONpc.NPCGoal = (byte)(liValue & 0xF);

			if (mOVm.TryGetImportedGlobal("npc_gtarg", out liValue))
				mONpc.NPCGTarg = (byte)(liValue & 0xFF);
		}

		/// <summary>The player values from RetrieveImportedVariablesAfterConversation: hunger,
		/// hit points, mana, poison, and new_player_exp as experience without halving
		/// (EXPChange). Only what the conversation imports.</summary>
		private void fWriteBackPlayer()
		{
			if (mIHost == null || !mIHost.HasPlayer)
				return;

			int liHunger, liHp, liMana, liPoison, liExperience;

			if (!mOVm.TryGetImportedGlobal("play_hunger", out liHunger))
				liHunger = mIHost.PlayerHunger;

			if (!mOVm.TryGetImportedGlobal("play_hp", out liHp))
				liHp = mIHost.PlayerHitPoints;

			if (!mOVm.TryGetImportedGlobal("play_mana", out liMana))
				liMana = mIHost.PlayerMana;

			if (!mOVm.TryGetImportedGlobal("play_poison", out liPoison))
				liPoison = mIHost.PlayerPoison;

			mIHost.ApplyConversationValues(liHunger, liHp, liMana, liPoison);

			if (mOVm.TryGetImportedGlobal("new_player_exp", out liExperience) && (short)liExperience != 0)
			{
				mIHost.ChangeExperience((short)liExperience, mIHost.CurrentLevelNumber);

				// Do not award twice in case values are written back again.
				mOVm.SetImportedGlobal("new_player_exp", 0);
			}
		}

		/// <summary>The conversation partner, for reading back his goal after the conversation.</summary>
		public UWNpc Npc => mONpc;

		private string fGetPartnerName(int piSlot)
		{
			try
			{
				return mOData.Strings.Blocks[UWConversations.PartnerNameStringBlock]
					.Strings[UWConversations.GetPartnerNameIndex(piSlot)].TrimEnd('\r', '\n');
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// A conversation text. With the project's usual offset of one - without it the NPC says
		/// the line before his greeting, and the greeting appears as the player's first
		/// reply (checked in the test run with Hagbard, 2026-08-30).
		/// </summary>
		private string fGetConversationString(int piBlock, int piIndex)
		{
			if (piIndex == PlayerNameString)
				return mOData != null && mOData.InitialPlayer != null && !string.IsNullOrEmpty(mOData.InitialPlayer.Name)
					? mOData.InitialPlayer.Name
					: "Avatar";

			string lsDynamic;

			if (mODynamicStrings.TryGetValue(piIndex, out lsDynamic))
				return lsDynamic;

			try
			{
				if (mOData == null || !mOData.Strings.Blocks.ContainsKey(piBlock))
					return string.Empty;

				var lOBlock = mOData.Strings.Blocks[piBlock];

				int liIndex = piIndex + 1;

				if (liIndex < 0 || liIndex >= lOBlock.Strings.Count)
					return string.Empty;

				return UWTextLayout.CleanText(lOBlock.Strings[liIndex].TrimEnd('\r', '\n'));
			}
			catch
			{
				return string.Empty;
			}
		}
	}
}
