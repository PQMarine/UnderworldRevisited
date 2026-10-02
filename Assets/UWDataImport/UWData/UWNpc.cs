namespace UWDataImport.UWData
{
	public class UWNpc : UWObject
	{
		/// <summary>The goal values the game logic tells apart: 5 attacks, 10 wants to talk.</summary>
		public const int GoalAttack = 5;

		public const int GoalWantToTalk = 10;

		/// <summary>Attitude 0 is hostile, 3 friendly - and the highest value.</summary>
		public const int AttitudeHostile = 0;

		public const int AttitudeFriendly = 3;

		public const int MaxAttitude = 3;

		/// <summary>Hit points are one byte.</summary>
		public const int MaxHitPoints = 255;

		public byte NPCHeading;

		public bool NPCUsed;

		public byte NPC_HP;

		/// <summary>
		/// The hit points as stored in the savegame - a whole byte at 0x08 of the
		/// record (reference: uwObject.npc_hp).
		///
		/// Since 2026-09-11 NPC_HP carries the same value (before that double, a mistake).
		/// This field is the one tracked at runtime (UWWorldSync) and the one
		/// written back; NPC_HP stays the value from loading.
		/// </summary>
		public byte HitPoints;

		/// <summary>
		/// The nineteen extra bytes of a mobile object, exactly as they were in the file.
		///
		/// We only read part of them. When writing back (see UWLevelWriter)
		/// these bytes serve as the base and only what we know is overwritten - so
		/// everything else, such as movement state and target coordinates, stays untouched.
		/// </summary>
		public byte[] RawNpcBytes;

		public byte NPCGoal;

		public byte NPCGTarg;

		public byte NPCLevel;

		public bool NPCTalkedTo;

		/// <summary>Whether the creature's belongings have already been rolled (bit 12 of the word at
		/// 0x0D in the object record, reference: uwObject.LootSpawnedFlag). The original only rolls them when
		/// someone looks inside - when trading or on death - and remembers that here so
		/// it does not happen twice.</summary>
		public bool NPCLootSpawned;

		public byte NPCAttitude;

		/// <summary>Whether the creature was only created during play (bit 8 of the word at 0x0D,
		/// reference: uwObject.SpawnedCritter_0XD_Bit8). The original deletes such creatures
		/// as soon as the player leaves the level - summoned helpers and trap monsters
		/// therefore do not survive a level change.</summary>
		public bool NPCSpawned;

		/// <summary>Whether the creature is exempt from attitude changes of its race (bit 7 of
		/// byte 0x0A in the object record, reference: uwObject.UnkBit_0XA_Bit7). The reference calls
		/// the bit unknown; it is read there in the same places where a whole
		/// race changes its attitude (set_race_attitude, the balancing on level change).
		/// What else it means is open.</summary>
		public bool NPCAttitudeLocked;

		/// <summary>Bit 9 of the same word at 0x0D. The reference checks it before a
		/// creature regains health on level change (UnkBit_0XD_Bit9); what else it
		/// stands for is unknown.</summary>
		public bool NPCNoHealing;

		/// <summary>The CURRENT tile of the creature (word at 0x16, uw-formats calls it
		/// npc_xhome/npc_yhome). In every file of the original equal to the tile in whose
		/// list it hangs; the home is stored in Quality and Owner (2026-09-11).</summary>
		public byte NPCXHome;

		public byte NPCYHome;

		/// <summary>Bits 0-6 of byte 0x19 AS LOADED. Misnamed: the hunger bit is bit 7 (read by
		/// UWConversationSession from RawNpcBytes[17]), bits 0-5 are the creature AI's flags and
		/// bit 6 the ally bit. Nothing writes this field back since 2026-09-20 (UWLevelWriter
		/// keeps the raw byte); UWCritterRecord is the live view of the byte.</summary>
		public byte NPCHunger;

		/// <summary>Whether the creature is on the player's side (bit 6 of byte 0x19 in the
		/// object record, reference: uwObject.IsAlly). A conversation sets it when the script
		/// leaves an attitude above 3 ("follow me"), and in the next conversation
		/// the attitude then reads as 6.</summary>
		public bool NPCIsAlly;

		public byte NPCwhoami;

		public byte Animstate;

		public byte Animframe;

		public UWNpc(ushort piItem_id)
			: base(piItem_id)
		{
		}
	}
}
