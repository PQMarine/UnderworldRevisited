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
	///
	/// THE WORLD (cleanup step 3.6, 2026-10-17): the level, its number, the doors gronk_door moves,
	/// the objects place_object puts down, the drops at the end of a barter, remove_talker and the
	/// player's inventory are IUWWorld's; this interface keeps the player's numbers (they are the
	/// conversation's own: rounded as the display rounds) and the two text services. Until then
	/// it had a world block of its own, forwarding to the world and the level loader.
	/// </summary>
	public interface IUWConversationHost
	{
		/// <summary>The world the conversation takes place in; null without one (a test run
		/// without a game).</summary>
		IUWWorld World { get; }

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

		// ------------------------------------------------- Text

		/// <summary>The full look description of an item ("You see ..."), or null.</summary>
		string DescribeItem(UWObject pOItem, int piDetail = UWItemDescriptions.DetailFromLoreCheck);

		/// <summary>Shows a look text for a moment in the answer scroll without writing it
		/// into the conversation. False if there is no such place; the session then prints
		/// it as a line.</summary>
		bool ShowLookText(string psText);
	}
}
