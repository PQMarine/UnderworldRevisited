namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STORY'S FIXED PLACES AND IDS, in the rule set (story.*; cleanup phase 10, wave 2,
	/// 2026-10-26): not balance but the anchors a total conversion repoints (Docs/CLEANUP-PLAN.md
	/// section 6) - the talking door's conversation, the conversation trap's partner, the emerald
	/// puzzle, the quest tiles of UWQuestPlaces, the bullfrog puzzle's game variables and the
	/// start tile. Their levels are levels.*. Each default is the const that held it; see UWRules.
	/// Tile coordinates are 0 to 63.
	/// </summary>
	public sealed class UWRulesStory
	{
		[UWRule(typeof(UWObjectMechanics), nameof(UWObjectMechanics.TalkingDoorConversationSlot),
			"The conversation a_do trap 42 starts - the talking door.", Minimum = 0)]
		public int TalkingDoorConversation = UWObjectMechanics.TalkingDoorConversationSlot;

		[UWRule(typeof(UWTrapRules), nameof(UWTrapRules.ConversationTrapWhoAmI),
			"a_do trap 50 talks to the first active creature with this conversation (the troll guard of level 7).",
			Minimum = 0)]
		public int ConversationTrapWhoAmI = UWTrapRules.ConversationTrapWhoAmI;

		public readonly EmeraldRules Emerald = new EmeraldRules();

		public readonly CupRules Cup = new CupRules();

		public readonly BullfrogRules Bullfrog = new BullfrogRules();

		public readonly TyballPrisonRules TyballPrison = new TyballPrisonRules();

		public readonly VolcanoRules Volcano = new VolcanoRules();

		public readonly BurialTriggerRules BurialTrigger = new BurialTriggerRules();

		public readonly VoidLandingRules VoidLanding = new VoidLandingRules();

		public readonly StartRules Start = new StartRules();

		/// <summary>story.emerald.*: the emerald puzzle (a_do trap 40) looks for this object
		/// offset tiles diagonally from the trap's tile.</summary>
		public sealed class EmeraldRules
		{
			[UWRule(typeof(UWObjectMechanics), nameof(UWObjectMechanics.EmeraldObjectId),
				"The object the emerald puzzle looks for.", Minimum = 0)]
			public int ObjectId = UWObjectMechanics.EmeraldObjectId;

			[UWRule(typeof(UWObjectMechanics), nameof(UWObjectMechanics.EmeraldPuzzleOffset),
				"The emerald puzzle looks this many tiles diagonally from its trap.", Minimum = 1)]
			public int Offset = UWObjectMechanics.EmeraldPuzzleOffset;
		}

		/// <summary>story.cup.*: where the Cup of Wonder lies on levels.cupOfWonder - the tile the
		/// shrine's direction and the mandolin's melody point to.</summary>
		public sealed class CupRules
		{
			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.CupTileX),
				"The Cup of Wonder's tile, X.", Minimum = 0)]
			public int TileX = UWQuestPlaces.CupTileX;

			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.CupTileY),
				"The Cup of Wonder's tile, Y.", Minimum = 0)]
			public int TileY = UWQuestPlaces.CupTileY;
		}

		/// <summary>story.tyballPrison.*: the tile whose move triggers go with Tybal when he dies.</summary>
		public sealed class TyballPrisonRules
		{
			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.TyballPrisonTileX),
				"The tile of Tybal's prison, X: its move triggers go with him.", Minimum = 0)]
			public int TileX = UWQuestPlaces.TyballPrisonTileX;

			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.TyballPrisonTileY),
				"The tile of Tybal's prison, Y.", Minimum = 0)]
			public int TileY = UWQuestPlaces.TyballPrisonTileY;
		}

		/// <summary>story.burialTrigger.*: the trigger that fires after Garamon's burial.</summary>
		public sealed class BurialTriggerRules
		{
			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.BurialTriggerTileX),
				"The tile of the trigger the burial fires, X.", Minimum = 0)]
			public int TileX = UWQuestPlaces.BurialTriggerTileX;

			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.BurialTriggerTileY),
				"The tile of the trigger the burial fires, Y.", Minimum = 0)]
			public int TileY = UWQuestPlaces.BurialTriggerTileY;
		}

		/// <summary>story.voidLanding.*: where the moongate puts the player down on levels.void.</summary>
		public sealed class VoidLandingRules
		{
			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.VoidTileX),
				"The tile the moongate puts the player down on in the Void, X.", Minimum = 0)]
			public int TileX = UWQuestPlaces.VoidTileX;

			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.VoidTileY),
				"The tile the moongate puts the player down on in the Void, Y.", Minimum = 0)]
			public int TileY = UWQuestPlaces.VoidTileY;
		}

		/// <summary>story.start.*: where a new character starts on the first level.</summary>
		public sealed class StartRules
		{
			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.StartTileX),
				"A new character's start tile on the first level, X.", Minimum = 0)]
			public int TileX = UWQuestPlaces.StartTileX;

			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.StartTileY),
				"A new character's start tile, Y.", Minimum = 0)]
			public int TileY = UWQuestPlaces.StartTileY;
		}

		/// <summary>story.bullfrog.*: the bullfrog puzzle of a_do trap 24 - its field's corner (the
		/// field is 8 by 8, as the trap's selection counts 0 to 7), the game variables holding the
		/// choice and the tries, and the tries a new game and the reset give.</summary>
		public sealed class BullfrogRules
		{
			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.BullfrogGridOrigin),
				"The bullfrog field's first tile on both axes.", Minimum = 0)]
			public int GridOrigin = UWQuestPlaces.BullfrogGridOrigin;

			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.BullfrogVarX),
				"The game variable of the bullfrog puzzle's X choice.", Minimum = 0)]
			public int VarX = UWQuestPlaces.BullfrogVarX;

			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.BullfrogVarY),
				"The game variable of its Y choice.", Minimum = 0)]
			public int VarY = UWQuestPlaces.BullfrogVarY;

			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.BullfrogVarRetries),
				"The game variable of its remaining tries.", Minimum = 0)]
			public int VarRetries = UWQuestPlaces.BullfrogVarRetries;

			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.BullfrogRetriesAtStart),
				"The tries a new game starts with.", Minimum = 0)]
			public int RetriesAtStart = UWQuestPlaces.BullfrogRetriesAtStart;

			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.BullfrogRetriesOnReset),
				"The tries the puzzle's reset gives.", Minimum = 0)]
			public int RetriesOnReset = UWQuestPlaces.BullfrogRetriesOnReset;
		}

		/// <summary>story.volcano.*: where the moongate appears and a talisman burns - within
		/// radius tiles (Manhattan distance) of centre/centre on levels.volcano.</summary>
		public sealed class VolcanoRules
		{
			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.VolcanoCentre),
				"The volcano's centre tile, the same on both axes.", Minimum = 0)]
			public int Centre = UWQuestPlaces.VolcanoCentre;

			[UWRule(typeof(UWQuestPlaces), nameof(UWQuestPlaces.VolcanoRadius),
				"A talisman burns within this many tiles of the centre (Manhattan distance).", Minimum = 0)]
			public int Radius = UWQuestPlaces.VolcanoRadius;
		}
	}
}
