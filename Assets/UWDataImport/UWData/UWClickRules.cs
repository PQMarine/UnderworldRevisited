namespace UWDataImport.UWData
{
	/// <summary>
	/// What a mouse click does - the right button in the 3D viewport with its press, drag
	/// threshold and release, the command modes, and which button carries the use over the
	/// UI. Engine-free, so every rule here is a plain function of what is known at the click:
	/// the mode, the kind of thing under the pointer, the phase of the gesture.
	///
	/// EVERY RULE IN HERE WAS MEASURED BY THE USER ON THE ORIGINAL (2026-09-18 and 19); the
	/// cases are listed as checks in Tools/UWSelfCheck, one per measurement.
	///
	/// THE COUNTER-CHECK AGAINST seg010 WAS DONE ON 2026-09-22 and found nothing against these
	/// rules. The original keeps TWO tables. Keys go into records of 12 bytes through
	/// RegisterEventHandler_seg010_105, and seg010_3FD walks them forwards, matching the key
	/// code, testing the event mask and calling the handler with the registered number. AREAS
	/// go into records of 18 bytes through RegisterEvent_seg010_83 - x0, y0, x1, y1, number,
	/// mask, handler - and seg010_308 to 3DD (58768-58880) walks them BACKWARDS, from the last
	/// registered to the first, so a later area wins over an earlier one where they overlap.
	/// A click is in an area when x0 &lt;= x &lt;= x1 and y0 &lt;= y &lt;= y1, both ends included,
	/// and the handler is given the position RELATIVE to the area's corner. The y is counted
	/// from the BOTTOM of the 200 line screen (see UWHudCompass, where five areas confirm it).
	///
	/// THE 3D VIEWPORT IS NOT IN THAT TABLE. What a click does in the world is decided by the
	/// pointer state machine of seg022_230E instead, which is why these rules had to be
	/// measured rather than read - and why they stay measurements.
	///
	/// WHAT THE TABLE DID SETTLE is the command icons: their whole column is one area
	/// (SetInteractionMode_seg024_24DC_13D5, x 8 to 32), and the icon is picked from the
	/// position inside it, (y + 2) / 18, which is the six buttons. See NextMode.
	///
	/// THE GESTURE: the press measures the target once and decides. In the default state the
	/// look comes at once, a pickable thing follows the pointer from the sixth pixel, anything
	/// else is used on the release after the threshold. A mode changes only the press over
	/// its kind of target: talk and use REMEMBER the target and act on the release wherever
	/// the pointer is by then, get takes at once, look looks at once, and the refusals come
	/// on the press. The left button walks in every mode; over the UI the modes change
	/// nothing but the use, which there replaces the look.
	/// </summary>
	public static class UWClickRules
	{
		/// <summary>What lies under the pointer, as far as the rules care.</summary>
		public enum TargetKind
		{
			/// <summary>Nothing at all - beyond the light, or the press was outside the viewport.</summary>
			Nothing,

			/// <summary>A wall, the floor, the ceiling, a door frame: something that is drawn but is
			/// no object.</summary>
			Geometry,

			Creature,

			/// <summary>An object that is neither a creature nor pickable: a door, a switch, a
			/// sign, a container in the world.</summary>
			Thing,

			/// <summary>Something that can be taken onto the pointer - also the one behind a blood
			/// pool or a bone pile.</summary>
			Pickable
		}

		/// <summary>The open gesture of the right button, from the press to the release.</summary>
		public enum GestureKind
		{
			/// <summary>Nothing is open: the press ended the gesture, or the release did.</summary>
			None,

			/// <summary>The default state's gesture: a drag may start, the release may use.</summary>
			Default,

			/// <summary>Talk mode over a creature: the release starts the conversation.</summary>
			Talk,

			/// <summary>Use mode over a thing: the release uses it.</summary>
			Use
		}

		public enum ActionKind
		{
			None,

			/// <summary>Look at the frontmost thing, or at the wall or floor - "You see nothing."
			/// when there is nothing.</summary>
			Look,

			/// <summary>A line of string block 1, MessageIndex.</summary>
			Message,

			/// <summary>A line of the conversation block (7), MessageIndex.</summary>
			ConversationMessage,

			/// <summary>The pickable thing comes onto the pointer at once, within the hand's reach
			/// ("That is too far away to take." otherwise).</summary>
			TakeToPointer,

			/// <summary>The pickable thing follows the pointer - the drag of the default state.</summary>
			BeginDrag,

			Talk,

			/// <summary>Talk mode over a thing that is no creature: what it answers is TalkToThing.</summary>
			TalkToThing,

			Use
		}

		/// <summary>What talking to a thing that is no creature does, see TalkToThing.</summary>
		public enum ThingTalkAnswer
		{
			/// <summary>"You cannot talk to that!" - conversation block, CannotTalkToThatLine.</summary>
			CannotTalk,

			/// <summary>The shrine asks for the mantra, as its use does.</summary>
			ChantMantra,

			/// <summary>"There is no reaction from the princess." - string block 1, NoReactionFromPrincessMessage.</summary>
			NoReactionFromPrincess,

			/// <summary>Nothing at all.</summary>
			Silent
		}

		private const int AlwaysTalksWhoami = 0x16;

		private const int RodrickWhoami = 0x8E;

		private const int TyballWhoami = 0xE7;

		private const int SilentWhoami = 0xFF;

		private const int GoalAttack = 5;

		private const int GoalWithdraw = 6;

		private const int GoalStand = 9;

		private const int GoalTalkAnyway = 10;

		private const int PlayerTarget = 1;

		/// <summary>
		/// TALKING TO A CREATURE: whether one with a conversation answers at all. READ
		/// 2026-10-01 further in TalkTo_ovr100_0 (per user: Oradinar, set to flee from the player by his
		/// own conversation, cannot be talked to
		/// in the original; in ours he could):
		///
		///   - whoami 0x16, Rodrick (0x8E) and Tyball (0xE7) always answer;
		///   - a creature whose goal is 5, 6 or 9 (attack, withdraw, making a stand) against the player
		///     (gtarg 1), or whose attitude is hostile, does not - unless it is an ally (byte 0x19 bit 6)
		///     or its goal is 10;
		///   - whoami 255 answers only with goal 10.
		///
		/// Refused, the original writes "You get no response." (block 7).
		/// </summary>
		public static bool CreatureAnswers(int piWhoami, int piGoal, int piGTarg, int piAttitude, bool pbAlly)
		{
			if (piWhoami == AlwaysTalksWhoami || piWhoami == RodrickWhoami || piWhoami == TyballWhoami)
				return true;

			bool lbAgainstPlayer = (piGoal == GoalAttack || piGoal == GoalWithdraw || piGoal == GoalStand)
				&& piGTarg == PlayerTarget;

			if ((lbAgainstPlayer || piAttitude == UWNpc.AttitudeHostile) && !pbAlly)
				return piGoal == GoalTalkAnyway;

			return piWhoami != SilentWhoami || piGoal == GoalTalkAnyway;
		}

		/// <summary>String block 1: "There is no reaction from the princess." - talking to Arial on her wall.</summary>
		public const int NoReactionFromPrincessMessage = 273;

		/// <summary>
		/// TALKING TO A THING (TalkTo_ovr100_0, read 2026-09-24 after the user found ours
		/// answering Arial with "You cannot talk to that!"). Before the creature check the
		/// routine takes two objects aside: the shrine (343) chants the mantra, and the special
		/// wall decoration (366) looks up the terrain type of its texture - the chained princess
		/// (TERRAIN.DAT type 8) answers "There is no reaction from the princess.", any other
		/// texture answers NOTHING. Everything else that is no creature gets "You cannot talk to
		/// that!". The other decoration id (367) is not taken aside.
		/// </summary>
		public static ThingTalkAnswer TalkToThing(int piObjectId, bool pbChainedPrincessTexture)
		{
			if (piObjectId == UWShrineRules.ShrineObjectId)
				return ThingTalkAnswer.ChantMantra;

			if (piObjectId == SpecialDecorationObjectId)
				return pbChainedPrincessTexture ? ThingTalkAnswer.NoReactionFromPrincess : ThingTalkAnswer.Silent;

			return ThingTalkAnswer.CannotTalk;
		}

		/// <summary>The wall decoration the talk takes aside (0x16E) - drains, stairs, banners, Arial.</summary>
		public const int SpecialDecorationObjectId = 366;

		public struct Decision
		{
			public ActionKind Action;

			public int MessageIndex;

			/// <summary>The gesture after this decision; None ends it.</summary>
			public GestureKind Gesture;

			public override string ToString()
			{
				return Action + (Action == ActionKind.Message || Action == ActionKind.ConversationMessage
					? " " + MessageIndex : "") + ", gesture " + Gesture;
			}
		}

		/// <summary>String block 1: "You cannot use that." - the use mode over geometry or nothing.</summary>
		public const int CannotUseThatMessage = 153;

		/// <summary>String block 1: "Nothing to get." - the get mode over geometry or nothing.</summary>
		public const int NothingToGetMessage = 156;

		/// <summary>String block 1: "You cannot pick that up." - the get mode over a thing or a creature.</summary>
		public const int CannotPickThatUpMessage = 97;

		/// <summary>String block 1: "You cannot talk to that." - the talk mode over geometry or nothing.</summary>
		public const int CannotTalkToThatMessage = 157;

		/// <summary>Conversation block, entry 1: "You cannot talk to that!" - the talk mode over a
		/// thing that is no creature (the exclamation mark is the original's).</summary>
		public const int CannotTalkToThatLine = 1;

		private static Decision fDecide(ActionKind peAction, GestureKind peGesture, int piMessage = 0)
		{
			return new Decision { Action = peAction, Gesture = peGesture, MessageIndex = piMessage };
		}

		/// <summary>
		/// The press of the right button. Outside the viewport it does nothing and the modes do
		/// not apply - the gesture stays open for a release after a drag, as in the default
		/// state, but there is nothing to look at.
		/// </summary>
		public static Decision OnPress(UWCommandMode peMode, TargetKind peTarget, bool pbInsideViewport)
		{
			if (!pbInsideViewport)
				return fDecide(ActionKind.None, GestureKind.Default);

			bool lbGeometry = peTarget == TargetKind.Nothing || peTarget == TargetKind.Geometry;

			switch (peMode)
			{
				case UWCommandMode.Talk:
					if (peTarget == TargetKind.Creature)
						return fDecide(ActionKind.None, GestureKind.Talk);

					return lbGeometry
						? fDecide(ActionKind.Message, GestureKind.None, CannotTalkToThatMessage)
						: fDecide(ActionKind.TalkToThing, GestureKind.None);

				case UWCommandMode.Get:
					if (peTarget == TargetKind.Pickable)
						return fDecide(ActionKind.TakeToPointer, GestureKind.None);

					return fDecide(ActionKind.Message, GestureKind.None,
						lbGeometry ? NothingToGetMessage : CannotPickThatUpMessage);

				case UWCommandMode.Look:
					return fDecide(ActionKind.Look, GestureKind.None);

				case UWCommandMode.Use:
					if (lbGeometry)
						return fDecide(ActionKind.Message, GestureKind.None, CannotUseThatMessage);

					if (peTarget == TargetKind.Creature)
						return fDecide(ActionKind.None, GestureKind.None);

					return fDecide(ActionKind.None, GestureKind.Use);

				default:
					return fDecide(ActionKind.Look, GestureKind.Default);
			}
		}

		/// <summary>The pointer crossed the drag threshold with the button still down. Only the
		/// default state's gesture drags, and only a pickable thing; everything else waits for
		/// the release.</summary>
		public static Decision OnDragThreshold(GestureKind peGesture, TargetKind peTarget)
		{
			if (peGesture == GestureKind.Default && peTarget == TargetKind.Pickable)
				return fDecide(ActionKind.BeginDrag, GestureKind.Default);

			return fDecide(ActionKind.None, peGesture);
		}

		/// <summary>
		/// The release. A remembered creature is talked to, a remembered thing is used, wherever
		/// the pointer is by now. In the default state the release uses only after a drag that
		/// took nothing along (a drag that did, or that was refused for reach, is complete in
		/// itself); a plain click does nothing more, the look already came on the press.
		/// </summary>
		public static Decision OnRelease(GestureKind peGesture, bool pbAfterDrag, bool pbDragConsumed)
		{
			switch (peGesture)
			{
				case GestureKind.Talk:
					return fDecide(ActionKind.Talk, GestureKind.None);

				case GestureKind.Use:
					return fDecide(ActionKind.Use, GestureKind.None);

				case GestureKind.Default:
					return fDecide(pbAfterDrag && !pbDragConsumed ? ActionKind.Use : ActionKind.None, GestureKind.None);

				default:
					return fDecide(ActionKind.None, GestureKind.None);
			}
		}

		/// <summary>
		/// WHICH BUTTON CARRIES THE USE OVER THE UI. The left one always; the right one in the
		/// use mode, where over the UI it replaces the look - but not in a conversation, where
		/// the function stays the usual one even with the mode lit.
		/// </summary>
		public static bool IsUseClick(bool pbRightButton, UWCommandMode peMode, bool pbConversationOpen)
		{
			if (!pbRightButton)
				return true;

			return peMode == UWCommandMode.Use && !pbConversationOpen;
		}

		/// <summary>
		/// A click on a command icon: the icon that is already lit switches the mode off,
		/// another one replaces it.
		///
		/// CONFIRMED against SetInteractionMode_seg024_24DC_13D5 on 2026-09-22 (labels 1460 to
		/// 148B, 88595-88620): the button number plus one is held against the current mode, and
		/// if they are equal the icon is unlit and the mode set to zero; otherwise the old one
		/// is unlit and the new one takes its place. The sixth button is the OPTIONS panel and
		/// returns before any of that (label 141E), which is why it is no mode here either.
		///
		/// FIGHT IS MODE 2 in the original and does belong in that switch - but everything it
		/// does beyond it is the weapon: it refuses while swimming (PLAYER.DAT 0xB8 bit 0,
		/// label 148B), sets bit 1 of 0x5F, runs the weapon animation and changes to the armed
		/// theme. The port keeps it in the caller (Interaction.ToggleCombatMode) with the same
		/// result - it clears the command mode and is refused while swimming.
		/// </summary>
		public static UWCommandMode NextMode(UWCommandMode peCurrent, UWCommandMode peClicked)
		{
			if (peClicked == UWCommandMode.Fight || peClicked == UWCommandMode.Options)
				return peCurrent;

			return peClicked == peCurrent ? UWCommandMode.None : peClicked;
		}

		// ------------------------------------------------- The areas of the panel

		/// <summary>One area of the original's table: both ends included, y counted from the
		/// bottom of the 200 line screen.</summary>
		public struct Area
		{
			public readonly int X0;
			public readonly int Y0;
			public readonly int X1;
			public readonly int Y1;

			public Area(int piX0, int piY0, int piX1, int piY1)
			{
				X0 = piX0;
				Y0 = piY0;
				X1 = piX1;
				Y1 = piY1;
			}

			public bool Contains(int piX, int piY)
			{
				return piX >= X0 && piX <= X1 && piY >= Y0 && piY <= Y1;
			}
		}

		// THE FIVE FIXED AREAS of the panel, in the order seg024_24DC_26F registers them
		// (85486-85577, read whole 2026-09-26): the four numbers go in as the routine's first
		// four arguments, which RegisterEvent_seg010_83 stores at record offsets 6, 8, 2 and 4 -
		// and the dispatcher compares exactly those as x0, y0, x1, y1. Until 2026-09-26 only
		// the compass was taken from them; the other four were drawn areas of our own.

		/// <summary>The column of the command icons, x 8 to 32, rows -7 to 115 from the top.
		/// </summary>
		public static readonly Area CommandIcons = new Area(8, 0x54, 0x20, 0xCE);

		/// <summary>The hollow of the rune shelf, casting: x 176 to 222, rows 138 to 154.
		/// </summary>
		public static readonly Area RuneShelf = new Area(0xB0, 0x2D, 0xDE, 0x3D);

		/// <summary>The three active spell icons: x 52 to 102, rows 136 to 152.</summary>
		public static readonly Area ActiveSpells = new Area(0x34, 0x2F, 0x66, 0x3F);

		/// <summary>The compass: x 122 to 152, rows 135 to 150 (UWHudCompass).</summary>
		public static readonly Area Compass = new Area(0x7A, 0x31, 0x98, 0x40);

		/// <summary>Both flasks and the chain between them: x 244 to 309, rows 119 to 155.
		/// </summary>
		public static readonly Area Flasks = new Area(0xF4, 0x2C, 0x135, 0x50);

		/// <summary>The three easy-movement arrows under the compass, left to right (turn left,
		/// step forward, turn right), registered with the numbers -1, 0 and +1 by the player's
		/// setup: rows 152 to 166, the middle one 155 to 168.</summary>
		public static readonly Area[] EasyArrows =
		{
			new Area(0x6B, 0x21, 0x7B, 0x2F),
			new Area(0x82, 0x1F, 0x92, 0x2C),
			new Area(0x9B, 0x21, 0xAA, 0x2F)
		};

		/// <summary>
		/// Which command icon a click in the column means, from the position inside it
		/// (SetInteractionMode_seg024_24DC_13D5): (y + 2) / 18 counted from the bottom - 0 use,
		/// 1 fight, 2 look, 3 get, 4 talk, 5 the options. Above the sixth the column answers
		/// nothing (None).
		/// </summary>
		public static UWCommandMode CommandIconAt(int piRelativeY)
		{
			int liIndex = (piRelativeY + 2) / 18;

			switch (liIndex)
			{
				case 0: return UWCommandMode.Use;
				case 1: return UWCommandMode.Fight;
				case 2: return UWCommandMode.Look;
				case 3: return UWCommandMode.Get;
				case 4: return UWCommandMode.Talk;
				case 5: return UWCommandMode.Options;
				default: return UWCommandMode.None;
			}
		}

		/// <summary>
		/// Which active spell a click means (LookAtActiveSpell_ovr119_2C9): slot 2 - x / 16
		/// inside the area, so the first spell is the icon on the right (x 84 to 99), the second
		/// 68 to 83, the third 52 to 67; the last three columns give -1, which no spell answers.
		/// A slot counts only while that many spells are active.
		/// </summary>
		public static int ActiveSpellSlotAt(int piRelativeX)
		{
			return 2 - (piRelativeX >> 4);
		}

		public enum FlaskPart
		{
			None,
			Vitality,
			Mana,
			Chain
		}

		/// <summary>
		/// What a click in the flask area means (PrintFlaskHealthManaMessage_seg024_10F): the
		/// columns 26 to 39 inside it are the chain, which turns the panel when above the
		/// fourteenth row from the bottom (x 270 to 283, rows 119 to 141) and does nothing below;
		/// elsewhere the bottom 31 rows name the vitality left of the chain and the mana right
		/// of it (rows 125 to 155), and above them nothing.
		/// </summary>
		public static FlaskPart FlaskAt(int piRelativeX, int piRelativeY)
		{
			if (piRelativeX > 0x19 && piRelativeX < 0x28)
				return piRelativeY > 0x0D ? FlaskPart.Chain : FlaskPart.None;

			if (piRelativeY > 0x1E)
				return FlaskPart.None;

			return piRelativeX > 0x1E ? FlaskPart.Mana : FlaskPart.Vitality;
		}

		// ------------------------------------------------- The inventory page

		/// <summary>The whole inventory page is ONE area (registered by the inventory setup of overlay 121 with mask 5,
		/// so in a conversation too): x 240 to 315, rows 10 to 117. Its handler goes by the panel
		/// mode - inventory, runes or stats page - and on the inventory side looks the spot up
		/// in the table below (InventorySpotAt).</summary>
		public static readonly Area InventoryPage = new Area(0xF0, 0x52, 0x13B, 0xBD);

		/// <summary>The places of the inventory page, named by where they lie on the screen:
		/// "Left" is the screen's left, the character's right.</summary>
		public enum InventorySpot
		{
			None,
			Helmet,
			Chest,
			Gloves,
			Legs,
			Boots,
			ShoulderLeft,
			ShoulderRight,
			HandLeft,
			HandRight,
			RingLeft,
			RingRight,
			Backpack0,
			Backpack1,
			Backpack2,
			Backpack3,
			Backpack4,
			Backpack5,
			Backpack6,
			Backpack7,
			ContainerIcon,
			ScrollLeft,
			ScrollRight
		}

		/// <summary>
		/// THE ORIGINAL'S TABLE OF THE INVENTORY PAGE (read 2026-09-26): 23 records of 14 bytes
		/// in the data segment from 0x172A - x0, y1, x1, y0, then where the picture goes and its
		/// size - searched by ovr121_18AC from the first record on, the first hit wins. Record 0
		/// is empty. Taken over in record order with y from the bottom, rows from the top in
		/// the comments (199 - y). Where two overlap (row 55 of the legs and the gloves, rows 52-53 of the
		/// hands and the rings) the earlier record wins, as there.
		/// </summary>
		private static readonly (Area Area, InventorySpot Spot)[] msInventorySpots =
		{
			(new Area(269, 128, 285, 144), InventorySpot.Legs),           // rows 55-71
			(new Area(269, 175, 286, 191), InventorySpot.Helmet),         // rows 8-24
			(new Area(263, 157, 291, 174), InventorySpot.Chest),          // rows 25-42
			(new Area(263, 144, 291, 156), InventorySpot.Gloves),         // rows 43-55
			(new Area(263, 119, 291, 128), InventorySpot.Boots),          // rows 71-80
			(new Area(244, 170, 261, 187), InventorySpot.ShoulderLeft),   // rows 12-29
			(new Area(293, 170, 310, 187), InventorySpot.ShoulderRight),
			(new Area(241, 146, 258, 165), InventorySpot.HandLeft),       // rows 34-53
			(new Area(295, 146, 312, 165), InventorySpot.HandRight),
			(new Area(241, 136, 268, 147), InventorySpot.RingLeft),       // rows 52-63
			(new Area(286, 130, 312, 147), InventorySpot.RingRight),      // rows 52-69
			(new Area(240, 101, 257, 118), InventorySpot.Backpack0),      // rows 81-98
			(new Area(259, 101, 276, 118), InventorySpot.Backpack1),
			(new Area(278, 101, 295, 118), InventorySpot.Backpack2),
			(new Area(297, 101, 314, 118), InventorySpot.Backpack3),
			(new Area(240, 83, 257, 100), InventorySpot.Backpack4),       // rows 99-116
			(new Area(259, 83, 276, 100), InventorySpot.Backpack5),
			(new Area(278, 83, 295, 100), InventorySpot.Backpack6),
			(new Area(297, 83, 314, 100), InventorySpot.Backpack7),
			(new Area(240, 118, 257, 135), InventorySpot.ContainerIcon),  // rows 64-81
			(new Area(295, 120, 304, 129), InventorySpot.ScrollLeft),     // rows 70-79
			(new Area(305, 120, 314, 129), InventorySpot.ScrollRight)
		};

		/// <summary>The spot of the inventory page under a point (x from the left, y from the
		/// bottom), None where no record answers.</summary>
		public static InventorySpot InventorySpotAt(int piX, int piY)
		{
			foreach ((Area lOArea, InventorySpot leSpot) in msInventorySpots)
			{
				if (lOArea.Contains(piX, piY))
					return leSpot;
			}

			return InventorySpot.None;
		}

		/// <summary>The backpack slot of a spot, 0 to 7 in rows from the top left, or -1.</summary>
		public static int BackpackSlotOf(InventorySpot peSpot)
		{
			return peSpot >= InventorySpot.Backpack0 && peSpot <= InventorySpot.Backpack7
				? peSpot - InventorySpot.Backpack0 : -1;
		}

		/// <summary>
		/// The degree word of "You are ... poisoned." that the vitality flask puts before its
		/// line while the player is poisoned (same routine): (poison - 1) / 3 over the five
		/// words of block 1 from "barely", so 1-3 barely up to 13-15 egregiously; -1 when not
		/// poisoned.
		/// </summary>
		public static int PoisonDegree(int piPoison)
		{
			return piPoison <= 0 ? -1 : (piPoison - 1) / 3;
		}
	}
}
