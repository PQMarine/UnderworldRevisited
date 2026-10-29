namespace UWDataImport.UWData
{
	/// <summary>
	/// A snapshot of an object the brain looks at: its goal target, its last attacker, the
	/// creature it bumped into. Everything in the original's units - eighths of a tile, zpos,
	/// tiles, facing eighths, nibbles.
	/// </summary>
	public struct CritterTarget
	{
		/// <summary>False when the index names nothing (freed, off the level).</summary>
		public bool Exists;

		public int Index;

		/// <summary>Index 1, item id 0x7F.</summary>
		public bool IsPlayer;

		/// <summary>Word 0 bits 6-8 == 1: a mobile creature (the player included).</summary>
		public bool IsCreature;

		public int ItemId;

		/// <summary>Position in eighths: tile * 8 + fine.</summary>
		public int X;

		public int Y;

		public int ZPos;

		public int TileX;

		public int TileY;

		/// <summary>The COMOBJ object height, added to zpos for the sight line (spec 3.1).</summary>
		public int ObjectHeight;

		/// <summary>Word 2 bits 7-9.</summary>
		public int FacingEighth;

		/// <summary>Table byte 0x1D low nibble of its kind; for the player the runtime value
		/// (UWPlayerVitals.Quietness).</summary>
		public int Loudness;

		/// <summary>Table byte 0x1D high nibble; for the player 15 - Sneak / 5 minus equipment.</summary>
		public int Visibility;

		/// <summary>Byte 8; 0 means dead (the target validity guard, 54108).</summary>
		public int HitPoints;

		public int Goal;

		/// <summary>Byte 0x19 bit 6.</summary>
		public bool IsAlly;

		/// <summary>Table byte 9 of its kind.</summary>
		public int Kind;

		/// <summary>The player only: PlayerData 0x5F bit 1 (ReactToPlayerPresence, 51383).</summary>
		public bool WeaponDrawn;

		/// <summary>ZPos &gt;&gt; 3, what GetDistancesToGTarg_seg007_326B stores as the target's
		/// floor height.</summary>
		public int FloorLevel => ZPos >> 3;
	}

	/// <summary>
	/// What the physical step of an update left behind - the per-update globals of the
	/// original (spec 2.4, 7.3). The motion core fills it (UWCreatureMotion.Step, called by the
	/// host's RunMotion inside the update); a creature that did not move reports an empty
	/// result.
	/// </summary>
	public struct StepResult
	{
		/// <summary>RelatedToMotorCollision_2452: a collision callback fired.</summary>
		public bool Collided;

		/// <summary>dseg_5c99_246D: an object was hit, HitObjectIndex says which
		/// (CollisionObject_dseg_5c99_2442).</summary>
		public bool HitObject;

		public int HitObjectIndex;

		/// <summary>Word 0 bits 0-8 of the hit object: 0x7F the player, class 0x14 index &lt; 8 a
		/// closed door, 0x148..0x14F an open door, low three bits 7 a portcullis.</summary>
		public int HitObjectItemId;

		/// <summary>Word 0 bits 6-8 == 1 of the hit object.</summary>
		public bool HitObjectIsCreature;

		/// <summary>RelatedToColliding_2473: a closed door was among the collision records.</summary>
		public bool HitClosedDoor;

		/// <summary>HasCurrObjHeadingChanged_2449: the physics changed byte 9 (wall deflection or
		/// a push). The brain then keeps the deflected heading and never sets the blocked bit.</summary>
		public bool HeadingDeflected;

		/// <summary>IsNPCActive cleared by the callback with result flag 0x1000: the step left
		/// the creature stuck (interval 1, 41408).</summary>
		public bool Stuck;

		/// <summary>A land creature ended the step in deep water without an object under its
		/// feet - the tile state masked with 0xF8 equals 0x10 (seg006_1477_476, 41417-41421):
		/// splash, death animation 0x0C at frame 3, interval 1. Fliers and swimmers never set
		/// it; their callbacks have no water branch.</summary>
		public bool Drowned;

		/// <summary>dseg_5c99_2462: a flier touched the ceiling.</summary>
		public bool TouchedCeiling;

		/// <summary>The step left the tile: the record holds the new one, OldTileX/Y the old,
		/// and the host relinks the object's tile list (ApplyProjectileMotion's relink).</summary>
		public bool TileChanged;

		public int OldTileX;

		public int OldTileY;

		/// <summary>The lava roll of the write-back came up (1 in 5 on a moving update on lava):
		/// one point of plain fire, type 8 - the host applies it to the body.</summary>
		public bool LavaBurn;

		/// <summary>The impact of the step above 0x100, in units of 0x100 (ApplyProjectileMotion's
		/// DamageObject with type 0). For a creature the original overwrites the hit points with
		/// the params' value right after, so nothing lasts; reported for the trace.</summary>
		public int ImpactDamage;
	}

	/// <summary>
	/// What the creature brain (UWCritterBrain) asks of the world around it, for one
	/// creature. Everything in the original's integer units: positions in eighths of a tile,
	/// heights in zpos (7 bits, 8 per floor level), tiles 0..63, facing eighths 0..7 and fine
	/// headings 0..255 (0 = +Y, clockwise, 64 = +X), squared distances in eighths^2 or tiles^2,
	/// time in PIT ticks of about 3.9 ms.
	///
	/// The host is the Unity side (the successor of UWCritter); the self-check builds a fake
	/// one with a small tile map, a scripted RNG and a target position. The physics is the
	/// host's: it applies the Motion the brain returns (one displacement per update along the
	/// fine heading, its clearance probes as the substitute for the original's collision
	/// scan) and reports the outcome as a StepResult. The contract is written out in
	/// Docs/AI/brain-and-host.md.
	///
	/// SHRUNK TO THE QUESTIONS ABOUT THE WORLD AROUND THIS CREATURE AND THE EVENTS OUT OF ITS MIND
	/// (cleanup step 4.13, 2026-10-19; section 7.4 of Docs/CLEANUP-PLAN.md): the random source, the
	/// critter clock, the kin alarm and the path slots are the level's and the session's, not one
	/// creature's - the brain gets them as ICritterServices in its constructor; its own object
	/// height and the ammunition table's velocities are constants of its kind and the data, handed
	/// to the constructor as values; the goal trace is the brain's GoalChanged event. 32 members
	/// until then, 25 since.
	/// </summary>
	public interface ICritterHost
	{
		// ------------------------------------------------- The level

		/// <summary>The tile's floor in height levels (the tile word's floor nibble, what
		/// zpos &gt;&gt; 3 of a creature standing on it reads). Off the map: 0.</summary>
		int FloorLevelAt(int piTileX, int piTileY);

		/// <summary>The tile's no-magic bit (UWTile.NoMagicAllowed; anti_magic_p, the original's
		/// mind asks nothing more - UWEndgameRules.IsAntiMagicTile). The Void is the cast's refusal,
		/// not the mind's (UWCreatureSpellHost, cleanup step 4.10).</summary>
		bool IsMagicBlockedAt(int piTileX, int piTileY);

		/// <summary>The dungeon level, 1-based (PlayerLevel) - acceptable_danger's level 6
		/// exception (UWCritterRules.GetPathRangeBudget). Since 2026-10-15.</summary>
		int DungeonLevel { get; }

		/// <summary>The orb rule (NPC_Goal5_Attack 48879-48905, TryToDoMagicAttack,
		/// NPCStartMagicAttack): true on level 7 while the orb stands (PlayerData 0x60 bit 5 clear,
		/// UWTybalOrbRules). The MAGES, kind 0x13, then keep stand-off 1 and never cast - not
		/// Tybal himself, who is kind 0x19.</summary>
		bool TybalOrbStands { get; }

		// ------------------------------------------------- Other objects

		/// <summary>A snapshot of the object with this index (1 = the player); Exists false
		/// when there is none. The brain asks for its goal target, its attacker and the
		/// creature it collided with; the kin alarm needs no enumeration, it reads Alarm.</summary>
		CritterTarget GetObject(int piIndex);

		/// <summary>The tile the view is drawn from - the original's "PlayerTileX/Y", which
		/// PositionCamera_seg031_396 takes from the CAMERA every frame: the player's head
		/// normally, the camera trap's spot or Roaming Sight's eye while those show. The distance
		/// cull keeps a creature awake near it as near the player (UWCritterRules.IsFarFromBoth).</summary>
		UWTilePos ViewTile { get; }

		/// <summary>
		/// TestBetweenPoints_seg006_1477_1BD1 (44792-45426): a line walk in eighths from one
		/// point to the other with the height interpolated, blocked by the wall bits of the
		/// tiles crossed and where the interpolated height (&gt;&gt; 3) falls below the next
		/// tile's floor. Doors are objects and do not block. The heights are zpos plus the
		/// object heights, as the callers add them.
		/// </summary>
		bool HasLineOfSight(int piX0, int piY0, int piZ0, int piX1, int piY1, int piZ1);

		// ------------------------------------------------- Movement (the physics substitute)

		/// <summary>The straight-line test seg006_1477_1938 (44397): can this creature walk
		/// the straight tile line from its tile to the destination tile? Its own passability
		/// (walker, flier, swimmer, doors it could open) is the host's to know.</summary>
		bool IsStraightLineClear(int piToTileX, int piToTileY);

		/// <summary>
		/// The path search flood_path (PathFindBetweenTiles; UWTileRoute.FindPath with this
		/// creature's triple test) from the creature's tile and height (piFromX, piFromY,
		/// piFromHeight) to the destination tile at piToHeight: the squares, start first and the
		/// destination last, and the jump flag of each
		/// (UWTileRoute.Jumps) into the two lists, cleared first. piRangeBudget is the original's
		/// range (UWCritterRules.GetPathRangeBudget), spent as costs on drops of more than one level
		/// and on lava. False: no path. The brain stores the path in a slot (PathSlots) and follows
		/// it (since 2026-10-15; until then the host searched the next tile anew every half second).
		/// </summary>
		bool TryFindPath(int piFromX, int piFromY, int piFromHeight, int piToTileX, int piToTileY, int piToHeight, int piRangeBudget,
			System.Collections.Generic.List<UWTilePos> pOSquares, System.Collections.Generic.List<bool> pOJumps);

		/// <summary>
		/// The physics of this update (NPCInitialProcessing 52702-52990: NeedsToMove, the params,
		/// the pre-state, the step on the core, the write-back), called by the brain after the
		/// distance cull and the path bookkeeping and before the step's verdict - the order of
		/// the original. The host runs UWCreatureMotion.Step on its record, relinks the tile
		/// list, applies the lava burn and moves the body to the record's new position; the
		/// result is what the step left behind.
		/// </summary>
		StepResult RunMotion();

		/// <summary>Goal 3's teleport (48458-48471): put the record on this tile at its centre
		/// (eighth 4/4) and floor (the tile's height times eight, a flier too), relink it, the body
		/// there. The one write of the position (cleanup step 4.8, 2026-10-18; the brain wrote the
		/// record a second time after the call until then). The host's walkability check
		/// (deviation 37) may refuse; the record then stays as it was.</summary>
		bool Teleport(int piTileX, int piTileY);

		// ------------------------------------------------- Doors (NPCTryToOpenDoor, 47474)

		/// <summary>ObjectUse on the door: returns true when the door is open afterwards.</summary>
		bool UseDoor(int piDoorIndex);

		/// <summary>UnlockDoor_seg040_352B_1D3B with minus the skill: a lockpick attempt.</summary>
		void PickDoor(int piDoorIndex, int piSkill);

		/// <summary>DamageObject on the door with this damage of type 4.</summary>
		void BashDoor(int piDoorIndex, int piDamage);

		// ------------------------------------------------- Events out of the brain

		/// <summary>A melee swing is under way: animation 1..3 (piAttack 0 bash, 1 slash, 2
		/// thrust) passed its frame 0, the update after the decision. A swing the decision
		/// rolled but NPC_Goto's walk overwrote in the same update (spec 6.5) never gets here.</summary>
		void OnSwingStarted(int piAttack);

		/// <summary>The blow at frame 4 (NPCExecuteAttack_seg022_15DE): the host runs the reach
		/// scan, the skill check, the damage and the aftermath of spec 8.3. piCharge is the
		/// charge table VALUE (50..255, over 128), piSwingType RNG(9), piFlank the flanking
		/// bonus from the two facings.</summary>
		void OnBlow(int piTargetIndex, int piAttack, int piCharge, int piSwingType, int piFlank);

		/// <summary>NPCMissileLaunch_seg025_262 at frame 4 of animation 5: object 0x10 + ammo
		/// index at the launcher, heading byte 9, the pitch of GetPitchToGTarg.</summary>
		void OnMissileLaunched(int piAmmoIndex, int piPitch);

		/// <summary>SpellTrapWandCast_seg038_27 at frame 4 of animation 0x0D with the spell of
		/// the table (index into the runic spell table) and the pitch of GetPitchToGTarg(0x1E, 0).</summary>
		void OnSpellCast(int piSpellIndex, int piPitch);

		/// <summary>Goal 10: the player looked back within reach, TalkTo.</summary>
		void OnTalk();

		/// <summary>LoadSoundAtCoordinate at the creature: footsteps 1, 2, 0x17, 5, 0x0E, 0x0D;
		/// the death sound 6.</summary>
		void PlaySound(int piSound);

		/// <summary>ChangeThemeMusic: theme 6 at swing frame 0 against the player unless the
		/// current theme is 5..7 (53136-53160), theme 5 or 6 after a blow by the player
		/// (DamageNPC). The host also resets its combat music timer.</summary>
		void PlayMusic(int piTheme);

		/// <summary>The drowning of spec 7.3 (41417-41460): the host places the ONE splash
		/// (class-7 object with offset 6, so 0x1C6) at the creature and plays the water sound.
		/// The death removal that follows must leave nothing behind - see the measurement of
		/// 2026-09-20 in Docs/AI/creature-ai.md section 7.</summary>
		void OnDrowned();

		/// <summary>Death began (ProcessDeath_seg007_1798_3577): animation 0x0C frame 0. With
		/// pbByPlayer the host awards the experience (AwardKillEXP_seg022_1725).</summary>
		void OnDeathStarted(bool pbByPlayer);

		/// <summary>SpecialDeathCases_ovr107_149F(obj, 0) for a creature with a whoami: true
		/// vetoes the death (the creature keeps living with 0 hp until the script decides).</summary>
		bool VetoDeath();

		/// <summary>Frame 3 of animation 0x0C: SpecialDeathCases(obj, 1), unlink, loot, remains,
		/// inventory, free the object. The brain's Update returns Removed afterwards.</summary>
		void OnDeathFinished();
	}

	/// <summary>
	/// WHAT EVERY CREATURE'S MIND ON THE LEVEL DRAWS ON (cleanup step 4.13, 2026-10-19): the random
	/// source, the critter clock, the kin alarm and the sixteen path slots - one set per level and
	/// session, none of it one creature's, so it left ICritterHost. The game's is the session
	/// (UWGameSession, which owns all four); the self-check's brain fake scripts the rolls with a
	/// queue (an IUWRandom) and sets the clock.
	/// </summary>
	public interface ICritterServices
	{
		/// <summary>The original's RNG_seg005_DE7 followed by a division by n: Next(n) is a uniform
		/// 0..n-1, "1 in n" is Next(n) == 0. The brain draws in the original's order and never for a
		/// range of 0 or 1.</summary>
		IUWRandom Random { get; }

		/// <summary>The game clock in PIT ticks (UWCritterClock.Clock) - the kin alarm window.</summary>
		long Clock { get; }

		/// <summary>The kin alarm globals of this level (written by UWCritterBrain.OnDamaged).</summary>
		UWCritterAlarm Alarm { get; }

		/// <summary>The level's sixteen stored path slots (UWGameSession.CritterPaths).</summary>
		UWCritterPathSlots PathSlots { get; }

		/// <summary>The rule set the minds decide by (UWGameSession.Rules; cleanup phase 10 wave 4,
		/// 2026-10-26: UWRules.Current until then).</summary>
		UWRules Rules { get; }

		/// <summary>The game being played: its creature steps (IUWCritterVariant, UW2 plan phase 0 step 2).</summary>
		IUWGameProfile Game { get; }
	}
}
