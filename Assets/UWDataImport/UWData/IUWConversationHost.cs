namespace UWDataImport.UWData
{
	/// <summary>
	/// What a running conversation (UWConversationSession, UWConversationTrade) needs from the
	/// game around it: the player's numbers, the world, the inventory and a way to describe an
	/// item. The rules of the conversation live behind this interface, engine-free; the Unity
	/// side implements it by reading UWCharacter, UWLevelLoader, Interaction and the screen
	/// (UWConversationHost). Same pattern as UWConversationVM.IHost, one level up.
	///
	/// Health, mana and hit points come as whole numbers: the conversation globals are
	/// integers anyway, and the host rounds the way the display does.
	/// </summary>
	public interface IUWConversationHost
	{
		// ------------------------------------------------- The player

		/// <summary>False when no character is around (a test run without a game).</summary>
		bool HasPlayer { get; }

		int PlayerHunger { get; }

		int PlayerHitPoints { get; }

		int PlayerMaxHitPoints { get; }

		int PlayerMana { get; }

		int PlayerAttack { get; }

		int PlayerStrength { get; }

		int PlayerDexterity { get; }

		int PlayerLevel { get; }

		/// <summary>The game clock in the original's units (PLAYER.DAT 0xCE).</summary>
		int PlayerClock { get; }

		int PlayerPoison { get; }

		bool PlayerWeaponDrawn { get; }

		int GetPlayerSkill(UWPlayerData.Skill peSkill);

		/// <summary>Skills by the save game number (0 attack, 1 defence, then the eighteen
		/// named ones), as x_skills addresses them.</summary>
		int GetPlayerSkillByNumber(int piSkillNumber);

		void SetPlayerSkillByNumber(int piSkillNumber, int piValue);

		bool TryIncreasePlayerSkill(int piSkillNumber);

		/// <summary>Hunger, hit points, mana and poison as the conversation returns them at
		/// its end.</summary>
		void ApplyConversationValues(int piHunger, int piHitPoints, int piMana, int piPoison);

		void ChangeExperience(int piAmount, int piDungeonLevel);

		// ------------------------------------------------- The world

		/// <summary>The level the conversation takes place on, or null without a world.</summary>
		UWLevel CurrentLevel { get; }

		/// <summary>The dungeon level, 1-based - what the conversations call dungeon_level.</summary>
		int CurrentLevelNumber { get; }

		/// <summary>Opens, closes or toggles the door on a tile (gronk_door, see
		/// UWConversationSession.GronkDoorOpen and the two after it). False without a door.
		/// </summary>
		bool SetDoorState(int piTileX, int piTileY, int piMode);

		/// <summary>Puts an item onto a tile of the level (place_object).</summary>
		bool SpawnObjectInTile(UWObject pOItem, int piTileX, int piTileY);

		/// <summary>Drops an item at the player's feet, scattered over his tile - what does not
		/// fit anywhere.</summary>
		void DropAtPlayer(UWObject pOItem);

		/// <summary>Puts an item down around the player (PlaceObjectAtNPC_seg026_12E5 with the
		/// player object and radius 5) - what the player still has laid out when the conversation
		/// ends (end_barter_ovr095_3C2).</summary>
		void DropAroundPlayer(UWObject pOItem);

		/// <summary>Puts an item down around the partner (PlaceObjectAtNPC_seg026_12E5 with
		/// radius 5) - bought goods the player left on the partner's side when the conversation
		/// ends (end_barter_ovr095_3C2).</summary>
		void DropAtNpc(UWObject pOItem, UWNpc pONpc);

		/// <summary>The partner disappears from the world, display included (remove_talker).</summary>
		bool RemoveObjectFromWorld(UWObject pOObject);

		/// <summary>The creature turns on the player - attitude has already been set to
		/// hostile; this makes its body attack.</summary>
		void AngerNpc(UWNpc pONpc);

		// ------------------------------------------------- Inventory and text

		/// <summary>The player's inventory, or null without one.</summary>
		UWInventoryModel Inventory { get; }

		/// <summary>The full look description of an item ("You see ..."), or null.</summary>
		string DescribeItem(UWObject pOItem, int piDetail = UWItemDescriptions.DetailFromLoreCheck);

		/// <summary>Shows a look text for a moment in the answer scroll without writing it
		/// into the conversation. False if there is no such place; the session then prints
		/// it as a line.</summary>
		bool ShowLookText(string psText);
	}
}
