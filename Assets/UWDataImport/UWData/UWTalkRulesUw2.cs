namespace UWDataImport.UWData
{
	/// <summary>
	/// WHO TALKS IN THE SECOND GAME (UW2 plan phase 3 step 7): TalkTo, Underworld Exhumed uw2
	/// conv/CONVERSE.C:95. Anything not a creature cannot be talked to; a critter with goal 15, or any
	/// critter while time stands still, gives no response. Otherwise whoami 0x8C always talks, a critter with
	/// goal 10 always talks, and any other talks when its whoami is not 0xFF and it is an ally, or friendly and
	/// not fighting the player (goal 5, 6 or 9 with target 1). The conversation is the whoami's slot, or
	/// 0x100 + the creature class for whoami 0, and must exist in CNV.ARK. A wisp is talked to through
	/// talk_to_disembodied(0x30).
	/// </summary>
	public static class UWTalkRulesUw2
	{
		public enum AnswerEnum
		{
			/// <summary>The conversation starts.</summary>
			Talks,

			/// <summary>"You cannot talk to that!"</summary>
			NotACreature,

			/// <summary>"You get no response."</summary>
			NoResponse
		}

		/// <summary>The block of the two refusals: get_string(0xE00) and (0xE01) are STR_CONV, block 7.</summary>
		public const int RefusalBlock = 7;

		public const int NotACreatureString = 0;

		public const int NoResponseString = 1;

		/// <summary>The object a wisp stands for, the body talk_to_disembodied makes for it, and that body's
		/// conversation, attitude and goal.</summary>
		public const int WispId = 0x1CD;

		public const int WispBodyId = 0x40;

		public const int WispWhoami = 0x30;

		public const int WispAttitude = 3;

		public const int WispGoal = 10;

		/// <summary>A goal that never answers.</summary>
		private const int SilentGoal = 15;

		private const int ComeAndTalkGoal = 10;

		private const int FightGoal = 5;

		private const int FleeGoal = 6;

		private const int CorneredGoal = 9;

		/// <summary>The player's number as a goal's target.</summary>
		private const int PlayerTarget = 1;

		/// <summary>Whoami of a creature that never talks but on goal 10.</summary>
		private const int MuteWhoami = 0xFF;

		/// <summary>look_nothing's game_sprint(how + 0xA6) when the pointer is on nothing (ui/INTERACT.C): how is
		/// 0 use, 2 look, 3 get, 4 talk.</summary>
		public const int NothingFirstString = 0xA6;

		public const int NothingTalk = 4;

		/// <summary>The slots of the creature classes, whoami 0.</summary>
		public const int GenericFirstSlot = 0x100;

		private const int ClassMask = 0x3F;

		public static bool IsWisp(UWObject pOThing)
		{
			return pOThing != null && pOThing.ID == WispId;
		}

		/// <summary>Whether this critter will answer; piSlot its conversation. pOConversations null skips the
		/// test that the slot exists.</summary>
		public static AnswerEnum Answer(UWObject pOThing, UWConversations pOConversations, bool pbTimeStands, IUWConversationVariant pIVariant, out int piSlot)
		{
			piSlot = 0;

			UWNpc lONpc = pOThing as UWNpc;

			if (lONpc == null)
				return AnswerEnum.NotACreature;

			if (lONpc.NPCGoal == SilentGoal || pbTimeStands)
				return AnswerEnum.NoResponse;

			int liWho = lONpc.NPCwhoami;
			bool lbFighting = (lONpc.NPCGoal == FightGoal || lONpc.NPCGoal == FleeGoal || lONpc.NPCGoal == CorneredGoal)
				&& lONpc.NPCGTarg == PlayerTarget;
			bool lbWilling = (!lbFighting && lONpc.NPCAttitude != 0) || lONpc.NPCIsAlly;

			if (!pIVariant.AlwaysAnswers(liWho) && !(lbWilling && liWho != MuteWhoami) && lONpc.NPCGoal != ComeAndTalkGoal)
				return AnswerEnum.NoResponse;

			piSlot = liWho == 0 ? GenericFirstSlot + (lONpc.ID & ClassMask) : liWho;

			if (pOConversations != null)
			{
				UWConversations.Conversation lOConversation = piSlot < pOConversations.SlotCount ? pOConversations.GetConversation(piSlot) : null;

				if (lOConversation == null || lOConversation.Code == null || lOConversation.Code.Length == 0)
					return AnswerEnum.NoResponse;
			}

			return AnswerEnum.Talks;
		}

		/// <summary>talk_to_disembodied (event/WORLDEV.C): the wisp is no creature, so a rotworm is made with its
		/// conversation, friendly and come to talk, talked to and forgotten.</summary>
		public static UWNpc WispSpeaker()
		{
			return new UWNpc(WispBodyId)
			{
				ID = WispBodyId,
				NPCwhoami = WispWhoami,
				NPCAttitude = WispAttitude,
				NPCGoal = WispGoal
			};
		}
	}
}
