namespace UWDataImport.UWData
{
	/// <summary>
	/// The game's sounds, by number in SOUNDS.DAT (the reference: UWsoundeffects, the
	/// numbers taken from the call sites in the disassembly). Of the 24, these are known:
	///
	///    0  water (the splash: swimming, jumping in, something falling in)
	///  1/2  footsteps, alternating
	///    3  hit on the player
	///    4  hit on a critter
	///    5  the splash, and an object coming to rest after a fall
	///    7  the impact of the player's own missile or spell
	///    6  critter death (only if the flag is set in the critter table)
	///    9  bowstring
	///   10  miss, throw
	///   11  door
	///   15  landing after a fall, object lands
	///   16  spell
	///   17  chime (intro)
	///   18  rumble
	///   19  lockpick, switch
	///   20  portcullis
	///   21  magic item refused (still recharging)
	///   22  spell failed
	///
	/// Engine-free since cleanup step 6.6 (2026-10-24), out of UWSoundEffects, which
	/// UWAudioService absorbed; the numbers are data of the original, not of the engine.
	/// </summary>
	public static class UWSoundEffectId
	{
		/// <summary>
		/// THE WATER SOUND, and the only one the game has: SurfaceFootsteps_seg034_2F89_713 plays
		/// it in place of the footsteps while PLAYER.DAT byte 0xB8 bit 0, the swimming flag, is
		/// set - so it is the splashing of a swimmer, and that is what one hears when anything
		/// goes into the water.
		///
		/// It was called WaterEdge until 2026-09-21 and was played when the player STEPPED into
		/// water. The original does not do that (per user, who listened for it): walking in is
		/// silent, jumping in splashes, and a falling object splashes.
		/// </summary>
		public const int Water = 0;

		public const int FootstepA = 1;

		public const int FootstepB = 2;

		public const int HitPlayer = 3;

		public const int HitCritter = 4;

		/// <summary>
		/// THE SPLASH. The user picked it out by ear from the rendered effects (2026-09-21,
		/// with a sound dump tool), and it fits where UW.EXE plays it: the motion code takes it when an
		/// object comes to REST while falling, with a volume from its mass (seg030_2B26, labels
		/// D3E to D7F), while everything else that bumps gets effect 0x0F. Not to be confused with
		/// effect 0, which is the continuous rushing while one is in the water.
		/// </summary>
		public const int Splash = 5;

		public const int CritterDeath = 6;

		/// <summary>
		/// THE IMPACT of a missile or a spell, but only the player's own:
		/// SpawnImpactAnimo_seg022_2D2 plays effect 7 beside the picture it spawns, and only when
		/// CurrentAttacker is 1, which is the player. A creature's missile lands without it.
		/// Found 2026-09-21 while going through seg022.
		/// </summary>
		public const int SpellImpact = 7;

		public const int BowTwang = 9;

		public const int Miss = 0xA;

		public const int Door = 0xB;

		public const int Landing = 0xF;

		public const int Spell = 0x10;

		public const int Chime = 0x11;

		public const int Rumble = 0x12;

		public const int Lockpick = 0x13;

		public const int Portcullis = 0x14;

		/// <summary>An enchanted item used again too soon: UW.EXE CastSpellFromObject_seg040_1F04
		/// plays it instead of casting (seg040_352B_1F54).</summary>
		public const int MagicItemRefused = 0x15;

		public const int SpellFailure = 0x16;
	}
}
