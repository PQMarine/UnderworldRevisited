namespace UWDataImport.UWData
{
	/// <summary>How the scheme's input acts on the world (UWUiSchemePolicy.WorldInput).</summary>
	public enum UWWorldInputKind
	{
		/// <summary>The right button's gesture in the view window - press, drag, release - as the
		/// original's (UWClassicWorldGesture); the same button is the weapon in combat mode.</summary>
		RightButtonGesture,

		/// <summary>The keys E (use), R (draw), Q (look) and the free pointer on the world
		/// (UWModernWorldInput); the right button switches the pointer instead.</summary>
		KeysAndFreePointer
	}

	/// <summary>Where a thrown or shot missile and a spell projectile take their aim from
	/// (UWUiSchemePolicy.AimSource).</summary>
	public enum UWAimSource
	{
		/// <summary>The pointer's place in the view window, as the original aims
		/// (InitPlayerProjectilesValues_seg025_D; UWPlayerThrow.GetPointerInView).</summary>
		PointerInView,

		/// <summary>Along a ray: the crosshair's, or the free pointer's on the world.</summary>
		AimRay
	}

	/// <summary>What the kind of a blow - overhead bash, slash, thrust - is read from when the
	/// weapon button goes down (IUWUiScheme.AttackKindSource).</summary>
	public enum UWAttackKindSource
	{
		/// <summary>The pointer's third of the view window, and its column (the original's ninths).</summary>
		PointerThirdsInView,

		/// <summary>The view's pitch: looking up a bash, down a thrust, straight a slash.</summary>
		ViewPitch,

		/// <summary>The pointer's third of the whole screen (the view fills it).</summary>
		PointerThirdsOnScreen
	}

	/// <summary>
	/// THE DECISIONS A SCHEME MAKES THAT NEED NO ENGINE (cleanup 5.19, 2026-10-23): one table per
	/// interface scheme, Classic and Modern, read through IUWUiScheme.Policy. Until then the game
	/// logic tested the scheme itself (UWControlScheme.IsModern) at every one of these places and
	/// branched; now each place asks the one question it has, and the table answers as that scheme
	/// answered before - no value here changes what the player sees. The comments at the places
	/// keep the user's observations and dates; the fields name the question.
	///
	/// A third front end (decision 26 of the cleanup plan: Unreal, SDL) chooses one of these tables
	/// or writes its own; the self-check pins both.
	/// </summary>
	public sealed class UWUiSchemePolicy
	{
		// ------------------------------------------------- The prompts of the message scroll

		/// <summary>The running line "Chant the mantra: ..." stands in the message scroll; else the
		/// scheme's own box shows the prompt and the typed text (UWPromptController.fSetMantraLine).</summary>
		public bool MantraLineInScroll { get; private set; }

		/// <summary>A typed input and the yes/no question hide the system pointer while they stand,
		/// as the original's input line; else the box shows the pointer itself
		/// (UWPromptController.fHoldForTypedInput, AskYesNo).</summary>
		public bool PromptHidesPointer { get; private set; }

		/// <summary>Any mouse button ends the mantra as Enter does, as in the original; else the box
		/// takes the mouse for its OK and Cancel (UWPromptController.UpdateMantra).</summary>
		public bool MouseButtonEndsMantra { get; private set; }

		/// <summary>A cancelled mantra writes the original's dash and chants the empty text ("That
		/// is not a mantra."); else cancelling leaves no trace (UWPromptController.fFinishMantra).</summary>
		public bool CancelledMantraChantsDash { get; private set; }

		/// <summary>The gamepad's A and B answer the yes/no question, the pad's pointer standing
		/// aside meanwhile; else the box is clicked with the pointer (UWPromptController.UpdateYesNo,
		/// UWGamepadPointer).</summary>
		public bool PadButtonsAnswerYesNo { get; private set; }

		/// <summary>The right button confirms "Yes" and the left turns the answer into "No", as the
		/// original; else the box answers (UWPromptController.UpdateYesNo).</summary>
		public bool MouseButtonsAnswerYesNo { get; private set; }

		// ------------------------------------------------- The conversation

		/// <summary>The number keys that choose an answer: 1 to 4 on the original's scroll, 1 to 9
		/// in the modern conversation (UWConversationView.ChoiceKeys).</summary>
		public int ConversationChoiceKeys { get; private set; }

		/// <summary>The history waits with [MORE] after a full parchment and at every "\m"; else it
		/// scrolls and nothing waits for a click (UWConversationView.Policy).</summary>
		public bool ConversationPausesForMore { get; private set; }

		/// <summary>The right button chooses an answer line as the left does; else the left only -
		/// the right one is the bags' (UWConversationController.fReadChoice).</summary>
		public bool ConversationRightClickChooses { get; private set; }

		/// <summary>The conversation is drawn on the classic frame's canvas
		/// (UWClassicConversationRenderer); else the modern conversation draws it (UWModernConversation).</summary>
		public bool ConversationOnFrame { get; private set; }

		/// <summary>A typed answer (babl_ask) stands on the original's scroll at the bottom, which
		/// cannot move: the letter grid goes to the top and keeps above the scroll (UWLetterGrid).</summary>
		public bool AnswerTypedOnScroll { get; private set; }

		// ------------------------------------------------- The world

		/// <summary>How the scheme's input acts on the world (Interaction.Update).</summary>
		public UWWorldInputKind WorldInput { get; private set; }

		/// <summary>The right button charges and releases the weapon; else the left one, or the
		/// gamepad's right trigger (UWPlayerAttackInput.AttackButton).</summary>
		public bool RightButtonAttacks { get; private set; }

		/// <summary>A drag from the world puts a pickable thing on the pointer
		/// (UWClassicWorldGesture, UWItemDrag.TryBeginWorldDrag); else the pickup sits in the use
		/// (UWModernWorldInput).</summary>
		public bool WorldDragPicksUp { get; private set; }

		/// <summary>The inventory is the frame's paperdoll and backpack, whose slots take the clicks
		/// and the drags (UWItemDrag); else the bags do, and the frame's slots are not on screen.</summary>
		public bool InventoryOnFrame { get; private set; }

		/// <summary>A gift (the silver tree's seed) goes into the bags first, as what the scheme
		/// picks up does; else onto the pointer as in the original (UWSilverTree, UWInventoryModel.GiveToPlayer).</summary>
		public bool PickupGoesToBags { get; private set; }

		/// <summary>A locked door is opened with the matching key from the backpack by the use
		/// itself (UWWorldUse.fTryUseDoor, UWDoorRules.Use); else the key is applied by hand.</summary>
		public bool KeyUsedAutomatically { get; private set; }

		/// <summary>A look finds a trap only in the look icon's command mode, as the original; else
		/// every look searches, since the scheme has no look icon (UWLookService.fTryDetectTrap).</summary>
		public bool TrapSearchNeedsLookMode { get; private set; }

		/// <summary>Where a missile and a spell projectile take their aim from (UWPlayerMissiles,
		/// UWPendingSpell).</summary>
		public UWAimSource AimSource { get; private set; }

		/// <summary>A blow hits what stands in the hit area in front of the character, as the
		/// original; else what the ray to the aim hits (UWPlayerMelee).</summary>
		public bool MeleeByHitArea { get; private set; }

		/// <summary>A projectile cast from the runes or a wand flies at once towards the crosshair
		/// while the pointer is locked; else it waits for the click that aims it (UWPendingSpell.AfterModernCast).</summary>
		public bool ProjectileFliesWhenPointerLocked { get; private set; }

		/// <summary>Escape ends the Roaming Sight, since the scheme has no spell icon to click;
		/// else only the icon or the spell's end does (UWRoamingSight).</summary>
		public bool RoamingSightEndsOnEscape { get; private set; }

		/// <summary>The pointer's thirds with the left button steer the Roaming Sight, as the
		/// original; else the keys and the stick alone (UWRoamingSight).</summary>
		public bool PointerThirdsSteerSight { get; private set; }

		// ------------------------------------------------- The screen

		/// <summary>The original's screen - frame, panels, scroll - shows only under the big map,
		/// which lives on the classic frame; else always (UWGameUI, UWHudMessageLog, UWClassicWide).</summary>
		public bool ClassicScreenOnlyUnderMap { get; private set; }

		/// <summary>The gargoyle eyes show in the modern heading strip; else at the top edge of the
		/// classic frame (UWEyesDisplay).</summary>
		public bool EyesInHeadingStrip { get; private set; }

		/// <summary>A flash or a blackout (sleep, a hit) fills the whole screen, since the view does;
		/// else the view window of the classic frame (UWHudPictures.fFlashWindow).</summary>
		public bool FlashFillsScreen { get; private set; }

		/// <summary>A cutscene's picture lies in the middle in a leather frame at the UI's size; else
		/// in the classic view window (UWIntroPlayer).</summary>
		public bool CutsceneInLeatherFrame { get; private set; }

		/// <summary>The help lies over the picture - in the modern character panel's Help tab, or
		/// beside the Classic Wide frame; else beside the classic frame, which slides left for it
		/// (UWHelpWindow).</summary>
		public bool HelpOverPicture { get; private set; }

		private UWUiSchemePolicy()
		{
		}

		/// <summary>The original's screen and controls: the classic scheme's answers.</summary>
		public static readonly UWUiSchemePolicy Classic = new UWUiSchemePolicy
		{
			MantraLineInScroll = true,
			PromptHidesPointer = true,
			MouseButtonEndsMantra = true,
			CancelledMantraChantsDash = true,
			PadButtonsAnswerYesNo = true,
			MouseButtonsAnswerYesNo = true,
			ConversationChoiceKeys = UWConversationView.ClassicChoiceKeys,
			ConversationPausesForMore = true,
			ConversationRightClickChooses = true,
			ConversationOnFrame = true,
			AnswerTypedOnScroll = true,
			WorldInput = UWWorldInputKind.RightButtonGesture,
			RightButtonAttacks = true,
			WorldDragPicksUp = true,
			InventoryOnFrame = true,
			PickupGoesToBags = false,
			KeyUsedAutomatically = false,
			TrapSearchNeedsLookMode = true,
			AimSource = UWAimSource.PointerInView,
			MeleeByHitArea = true,
			ProjectileFliesWhenPointerLocked = false,
			RoamingSightEndsOnEscape = false,
			PointerThirdsSteerSight = true,
			ClassicScreenOnlyUnderMap = false,
			EyesInHeadingStrip = false,
			FlashFillsScreen = false,
			CutsceneInLeatherFrame = false,
			HelpOverPicture = false
		};

		/// <summary>The full-screen view with the modern HUD, bags and keys: the modern scheme's
		/// answers - the presets Classic Wide and Classic Modular are modern too.</summary>
		public static readonly UWUiSchemePolicy Modern = new UWUiSchemePolicy
		{
			MantraLineInScroll = false,
			PromptHidesPointer = false,
			MouseButtonEndsMantra = false,
			CancelledMantraChantsDash = false,
			PadButtonsAnswerYesNo = false,
			MouseButtonsAnswerYesNo = false,
			ConversationChoiceKeys = UWConversationView.ModernChoiceKeys,
			ConversationPausesForMore = false,
			ConversationRightClickChooses = false,
			ConversationOnFrame = false,
			AnswerTypedOnScroll = false,
			WorldInput = UWWorldInputKind.KeysAndFreePointer,
			RightButtonAttacks = false,
			WorldDragPicksUp = false,
			InventoryOnFrame = false,
			PickupGoesToBags = true,
			KeyUsedAutomatically = true,
			TrapSearchNeedsLookMode = false,
			AimSource = UWAimSource.AimRay,
			MeleeByHitArea = false,
			ProjectileFliesWhenPointerLocked = true,
			RoamingSightEndsOnEscape = true,
			PointerThirdsSteerSight = false,
			ClassicScreenOnlyUnderMap = true,
			EyesInHeadingStrip = true,
			FlashFillsScreen = true,
			CutsceneInLeatherFrame = true,
			HelpOverPicture = true
		};
	}
}
