using System;

namespace UWDataImport.UWData
{
	/// <summary>Where damage to the player comes from - one per site that lowers his hit points
	/// (cleanup step 4.2, 2026-10-17; the table of section 7.5 of Docs/CLEANUP-PLAN.md).</summary>
	public enum UWDamageSource
	{
		/// <summary>A creature's blow (UWCritter.fStrikePlayer), or its miss.</summary>
		CreatureBlow,

		/// <summary>A missile striking the player (UWMissiles): a creature's, a trap's or his
		/// own rebounding one.</summary>
		Missile,

		/// <summary>A blast on his tile (UWTileBlast): a fireball, Flame Wind, Sheet Lightning.</summary>
		Blast,

		/// <summary>Standing on lava (UWPlayerTerrain).</summary>
		Lava,

		/// <summary>Sinking while swimming (the swim check on the player tick).</summary>
		Drowning,

		/// <summary>A hard landing (the motion core's impact).</summary>
		Fall,

		/// <summary>A damage trap (UWTrapRules, whack_thing).</summary>
		Trap,

		/// <summary>Water or lava under whoever passed out drunk (UWSleepRules).</summary>
		SleepSurface,

		/// <summary>The damage trap in a poisoned potion's chain (UWItemUse).</summary>
		PotionTrap,

		/// <summary>The leeches' bite (UWItemUse).</summary>
		Leeches,

		/// <summary>The whole poison at once in a night (UWSleepRules).</summary>
		SleepPoison,

		/// <summary>A night on an empty stomach (UWSleepRules).</summary>
		Starvation,

		/// <summary>The poison's bout on the minute tick (UWPlayerVitals).</summary>
		PoisonTick,

		/// <summary>A worn cursed item on the status pass, Bronus' book, the cursed item taken
		/// into the hand (class 9).</summary>
		Curse,

		/// <summary>A backfired spell.</summary>
		Backfire,

		/// <summary>The Ethereal Void's drain (UWVoidEffects).</summary>
		VoidDrain,

		/// <summary>A level change that finds no room anywhere (UWLevelLoader).</summary>
		ArrivalNowhere,

		/// <summary>A blow of IUWWorld.Damage that came without a request (UWBlowRouting): plain
		/// typed damage, as the player end took every such blow before.</summary>
		Other
	}

	/// <summary>Who the combat music hears a damage from (damage_critter, UnderworldExhumed
	/// critter/AI.C:1458-1515, with who the attacker's index or a missile's last_hit): nobody (who
	/// 0, a trap's missile), a creature (who above 1: MUSIC_DANGER or MUSIC_COMBAT for the player)
	/// or the player himself (who 1: MUSIC_FOE_HURT or MUSIC_COMBAT; his own missile). 2026-10-19.</summary>
	public enum UWDamageMusic
	{
		None,

		Creature,

		Player
	}

	/// <summary>How a request takes the hit points - the four ways the sites had their own
	/// methods for until cleanup step 4.2 (2026-10-17): ApplyDamage, ApplyCurse and ApplyBackfire,
	/// DrainVitality, and the miss that only told the music.</summary>
	public enum UWDamageRule
	{
		/// <summary>damage_item (UnderworldExhumed combat/DAMAGE.C): the resistance of the type
		/// (check_res against the player's proof), recorded for F1 and counted for the dragons, and
		/// it can kill.</summary>
		Hit,

		/// <summary>backfire (SPELLS.C): the amount is a number of eight-sided dice; at three hit
		/// points or fewer nothing happens, otherwise at least three remain. No resistance;
		/// recorded, not counted.</summary>
		Backfire,

		/// <summary>The Void sets the hit points directly: no resistance, not recorded, not
		/// counted, never below zero.</summary>
		Drain,

		/// <summary>Nothing lands - a creature's miss, which still tells the combat music (do_attack
		/// hands the player damage_item with 0, COMBAT.C).</summary>
		Miss
	}

	/// <summary>
	/// ONE DAMAGE TO THE PLAYER, as a site hands it to UWDamagePipeline (cleanup step 4.2,
	/// 2026-10-17). Until then the sites lowered the hit points through four methods and did the
	/// rest themselves - the armour, the easy halving, the shake, the flash, the music - each in
	/// its own way (B6 of Docs/CLEANUP-PLAN.md). The factories below ARE the table of section 7.5:
	/// each site's type, armour, halving, resistance, flash and music in one place; only the sounds
	/// stay with the sites (they come before the armour, from the site's own numbers).
	///
	/// Formulas the table shows as different stay different: the blow and the missile take the
	/// armour and the halving, the curse and the leeches roll dice with their floor, the Void
	/// drains, everything else is plain damage of its type.
	/// </summary>
	public readonly struct UWDamageRequest
	{
		/// <summary>No flash.</summary>
		public const int NoFlash = -1;

		/// <summary>No body part: no armour to subtract.</summary>
		public const int NoBodyPart = -1;

		/// <summary>The proof the vitals' host reports at the moment of the damage.</summary>
		public const int HostProof = -1;

		/// <summary>The red flash of a creature's critical hit (UWCritterCombat.CriticalFlashColour,
		/// label A3D) and its length, as short as the curse's.</summary>
		public const int CriticalFlashColour = UWCritterCombat.CriticalFlashColour;

		public const float CriticalFlashSeconds = 0.1f;

		/// <summary>The flash of a backfire for the player (fill_FB(0xA8), SPELLS.C backfire); the
		/// reference uses 0x30 for uw2.</summary>
		public const int CurseFlashColour = 0xA8;

		public const float CurseFlashSeconds = 0.1f;

		/// <summary>Colour of the flash when the leeches bite - 0xA8 in uw1 (the reference:
		/// leech.use), the backfire's; a little longer than the curse's (UWItemUse until 2026-10-17).</summary>
		public const float LeechFlashSeconds = 0.2f;

		/// <summary>Drowning flashes blue (fill_FB(0xC6), PLAYTIME.C sink_sink_sink).</summary>
		public const int DrownFlashColour = 0xC6;

		public const float DrownFlashSeconds = 0.1f;

		/// <summary>backfire(ThePlayer, 2): the leeches bite with two eight-sided dice (USEITEMS.C
		/// UseMagic).</summary>
		public const int LeechDice = 2;

		/// <summary>Hit points, or under UWDamageRule.Backfire the number of dice; for a blow or a
		/// missile the damage BEFORE the armour.</summary>
		public readonly int Amount;

		/// <summary>A UWDamageTypes value: what the player's proof is tested against.</summary>
		public readonly int Type;

		public readonly UWDamageSource Source;

		public readonly UWDamageRule Rule;

		/// <summary>The body part hit (UWArmourProtection.PickBodyPart), NoBodyPart without one.</summary>
		public readonly int BodyPart;

		/// <summary>The armour of BodyPart comes off first (do_damage, COMBAT.C).</summary>
		public readonly bool SubtractArmour;

		/// <summary>The easy difficulty halves what the armour left (do_damage: player->easy).</summary>
		public readonly bool HalveOnEasy;

		/// <summary>The view shakes, from the damage after the armour and before the halving
		/// (do_damage's set_effect, C5 of 2026-10-12).</summary>
		public readonly bool Shakes;

		/// <summary>Who the combat music hears it from (damage_critter, critter/AI.C) - hit or not:
		/// nobody, a creature (MUSIC_DANGER or MUSIC_COMBAT) or the player himself (MUSIC_FOE_HURT or
		/// MUSIC_COMBAT; his own missile, since 2026-10-19).</summary>
		public readonly UWDamageMusic Music;

		/// <summary>The combat music hears of it at all.</summary>
		public bool TellsMusic => Music != UWDamageMusic.None;

		/// <summary>The palette colour the view window flashes when the damage lands, or NoFlash.</summary>
		public readonly int FlashColour;

		public readonly float FlashSeconds;

		/// <summary>The proof to test the type against, or HostProof for the host's at the moment.</summary>
		public readonly int Proof;

		private UWDamageRequest(int piAmount, int piType, UWDamageSource peSource, UWDamageRule peRule,
			int piBodyPart = NoBodyPart, bool pbSubtractArmour = false, bool pbHalveOnEasy = false, bool pbShakes = false,
			UWDamageMusic peMusic = UWDamageMusic.None, int piFlashColour = NoFlash, float pfFlashSeconds = 0f, int piProof = HostProof)
		{
			Amount = piAmount;
			Type = piType;
			Source = peSource;
			Rule = peRule;
			BodyPart = piBodyPart;
			SubtractArmour = pbSubtractArmour;
			HalveOnEasy = pbHalveOnEasy;
			Shakes = pbShakes;
			Music = peMusic;
			FlashColour = piFlashColour;
			FlashSeconds = pfFlashSeconds;
			Proof = piProof;
		}

		/// <summary>The same request tested against piProof instead of the host's.</summary>
		public UWDamageRequest WithProof(int piProof)
		{
			return new UWDamageRequest(Amount, Type, Source, Rule, BodyPart, SubtractArmour, HalveOnEasy, Shakes,
				Music, FlashColour, FlashSeconds, piProof);
		}

		// ------------------------------------------------- The sites (section 7.5)

		/// <summary>
		/// A creature's blow that hit (UWCritter.fStrikePlayer; COMBAT.C do_damage with type 4):
		/// piRolled is the damage after the charge and the flank, before the armour. The armour of
		/// the part, the easy halving, the shake, the combat music; the red flash on a critical.
		/// The hit sound stays at the site, from piRolled.
		/// </summary>
		public static UWDamageRequest CreatureBlow(int piRolled, int piBodyPart, bool pbCritical)
		{
			return new UWDamageRequest(piRolled, UWDamageTypes.Physical, UWDamageSource.CreatureBlow, UWDamageRule.Hit,
				piBodyPart, true, true, true, UWDamageMusic.Creature,
				pbCritical ? CriticalFlashColour : NoFlash, pbCritical ? CriticalFlashSeconds : 0f);
		}

		/// <summary>A creature's blow that missed: nothing lands, the combat music still hears of
		/// it (do_attack hands the player damage_item with 0, COMBAT.C; since 2026-10-12).</summary>
		public static UWDamageRequest CreatureMiss()
		{
			return new UWDamageRequest(0, UWDamageTypes.Physical, UWDamageSource.CreatureBlow, UWDamageRule.Miss,
				peMusic: UWDamageMusic.Creature);
		}

		/// <summary>
		/// EVERY MISSILE HIT ON THE PLAYER goes missile_newhit -> missile_thwack -> do_damage
		/// (UnderworldExhumed motion/OBJPHYS.C, combat/COMBAT.C): the armour of the part hit comes
		/// off (the player is Creature[63] there) and the easy difficulty halves what is left,
		/// whoever fired it and whatever its type. Until 2026-10-12 ours halved only a creature's
		/// arrow, bolt or stone - a mage's fireball or lightning bolt hit an easy player in full -
		/// and the player's own rebounding missile skipped armour and halving. The shake's level is
		/// taken before the easy halving (C5). piDamage is the missile's damage before the armour.
		/// The combat music hears of it by the missile's owner, piOwner (its last_hit byte:
		/// UWMissileHitRules.MusicFor - a creature's, the player's own, or none for a trap's; since
		/// 2026-10-19, a creature's alone until then). The two hit sounds stay at the site.
		/// (UWMissileHitRules.PlayerDamage until cleanup step 4.2, 2026-10-17.)
		/// EVERY MISSILE SHAKES THE VIEW (2026-10-19): missile_thwack sets fromwho to the missile's
		/// last_hit - 0 for a trap's, never -1 (UnderworldExhumed combat/COMBAT.C:761-780) - and
		/// do_damage skips the shake only for fromwho -1 (COMBAT.C:382-389; -1 is clear_fight_state's,
		/// COMBAT.C:627-637), so a trap's missile shakes as a creature's and the player's own do.
		/// Since 2026-10-12 (C5) ours shook only for a creature's or the player's own
		/// (UWMissileHitRules.ShakesPlayer), which a trap's missile launched in play passed as "the
		/// player's" and one read from the level data did not.
		/// </summary>
		public static UWDamageRequest Missile(int piDamage, int piType, int piBodyPart, int piOwner)
		{
			return new UWDamageRequest(piDamage, piType, UWDamageSource.Missile, UWDamageRule.Hit, piBodyPart, true, true,
				true, UWMissileHitRules.MusicFor(piOwner));
		}

		/// <summary>A blast on the player's tile: its dice already rolled, its type; no armour, no
		/// halving, no shake (UWTileBlast).</summary>
		public static UWDamageRequest Blast(int piDamage, int piType)
		{
			return new UWDamageRequest(piDamage, piType, UWDamageSource.Blast, UWDamageRule.Hit);
		}

		/// <summary>Lava under the feet: plain fire (damage_item(ThePlayer, ..., 1, DMG_FIRE),
		/// PLAYMOVE.C parse_effect), so Flameproof wards it off; the dragon skin boots keep the
		/// roll from coming at all (UWPlayerTerrainRules).</summary>
		public static UWDamageRequest Lava(int piDamage)
		{
			return new UWDamageRequest(piDamage, UWDamageTypes.PlainFire, UWDamageSource.Lava, UWDamageRule.Hit);
		}

		/// <summary>Drowning: untyped, with the blue flash (PLAYTIME.C sink_sink_sink: fill_FB(0xC6),
		/// then damage_item with type 0 - B5, decided by the original 2026-10-17).</summary>
		public static UWDamageRequest Drowning(int piDamage)
		{
			return new UWDamageRequest(piDamage, UWDamageTypes.None, UWDamageSource.Drowning, UWDamageRule.Hit,
				piFlashColour: DrownFlashColour, pfFlashSeconds: DrownFlashSeconds);
		}

		/// <summary>
		/// A hard landing: the value after the Acrobat check (the impact block of ApplyPlayerMotion).
		/// Typed as a blow, as ours always had it; the original hands damage_item type 0 there
		/// (PHYSICS.C), which is the same for the player - his proof never has the blow's bit 4
		/// (PLAYDATA.C damage_protection_flags).
		/// </summary>
		public static UWDamageRequest Fall(int piDamage)
		{
			return new UWDamageRequest(piDamage, UWDamageTypes.Physical, UWDamageSource.Fall, UWDamageRule.Hit);
		}

		/// <summary>
		/// A damage trap: type 4 on the player as on everything else (WORLDEV.C whack_thing hands
		/// damage_item the type 4 whoever the target is). Untyped until 2026-10-17 (B5, decided by
		/// the original); the same for the player, whose proof never has bit 4.
		/// </summary>
		public static UWDamageRequest Trap(int piDamage)
		{
			return new UWDamageRequest(piDamage, UWDamageTypes.Physical, UWDamageSource.Trap, UWDamageRule.Hit);
		}

		/// <summary>
		/// Water or lava under whoever passed out drunk: untyped (SKILLS.C drop_drunk_player hands
		/// damage_item 0xFF with type 0), so neither Flameproof nor the dragon skin boots help -
		/// B5, decided by the original 2026-10-17.
		/// </summary>
		public static UWDamageRequest SleepSurface(int piDamage)
		{
			return new UWDamageRequest(piDamage, UWDamageTypes.None, UWDamageSource.SleepSurface, UWDamageRule.Hit);
		}

		/// <summary>The damage trap in a poisoned potion's chain: untyped (UWItemUse).</summary>
		public static UWDamageRequest PotionTrap(int piDamage)
		{
			return new UWDamageRequest(piDamage, UWDamageTypes.None, UWDamageSource.PotionTrap, UWDamageRule.Hit);
		}

		/// <summary>The leeches bite as a backfire of two dice and flash when they bit (USEITEMS.C
		/// UseMagic).</summary>
		public static UWDamageRequest Leeches()
		{
			return new UWDamageRequest(LeechDice, UWDamageTypes.None, UWDamageSource.Leeches, UWDamageRule.Backfire,
				piFlashColour: CurseFlashColour, pfFlashSeconds: LeechFlashSeconds);
		}

		/// <summary>
		/// The whole poison at once in a night: poison damage, against the proof of piProof
		/// (SKILLS.C player_sleep: damage_item(..., DMG_POISON), and damage_item runs check_res -
		/// so Poison Resistance wards it off, as on the awake tick). The proof is the one from before
		/// the night: player_sleep clears the active spells but the resist byte keeps them until
		/// its FixPlayerEquips at the end. Until 2026-10-17 ours ignored the resistance here (B5,
		/// decided by the original).
		/// </summary>
		public static UWDamageRequest SleepPoison(int piDamage, int piProof)
		{
			return new UWDamageRequest(piDamage, UWDamageTypes.Poison, UWDamageSource.SleepPoison, UWDamageRule.Hit,
				piProof: piProof);
		}

		/// <summary>A night on an empty stomach: two untyped points (SKILLS.C player_sleep).</summary>
		public static UWDamageRequest Starvation(int piDamage)
		{
			return new UWDamageRequest(piDamage, UWDamageTypes.None, UWDamageSource.Starvation, UWDamageRule.Hit);
		}

		/// <summary>The poison's bout on the minute tick: poison damage (PLAYTIME.C duration_check),
		/// against the proof the tick was handed.</summary>
		public static UWDamageRequest PoisonTick(int piDamage, int piProof)
		{
			return new UWDamageRequest(piDamage, UWDamageTypes.Poison, UWDamageSource.PoisonTick, UWDamageRule.Hit,
				piProof: piProof);
		}

		/// <summary>A class 9 curse of piDice dice: a backfire (PLAYDATA.C player_affected_by), with
		/// its flash; no dice, nothing at all.</summary>
		public static UWDamageRequest Curse(int piDice)
		{
			return new UWDamageRequest(piDice, UWDamageTypes.None, UWDamageSource.Curse, UWDamageRule.Backfire,
				piFlashColour: CurseFlashColour, pfFlashSeconds: CurseFlashSeconds);
		}

		/// <summary>A backfired spell of piDice dice, with its flash (SPELLS.C backfire).</summary>
		public static UWDamageRequest Backfire(int piDice)
		{
			return new UWDamageRequest(piDice, UWDamageTypes.None, UWDamageSource.Backfire, UWDamageRule.Backfire,
				piFlashColour: CurseFlashColour, pfFlashSeconds: CurseFlashSeconds);
		}

		/// <summary>The Void takes piAmount hit points (UWVoidRules); its flash belongs to the
		/// effect, not to the drain.</summary>
		public static UWDamageRequest VoidDrain(int piAmount)
		{
			return new UWDamageRequest(piAmount, UWDamageTypes.None, UWDamageSource.VoidDrain, UWDamageRule.Drain);
		}

		/// <summary>No room anywhere after a level change: death (new_player_pos, UWArrivalRules).</summary>
		public static UWDamageRequest ArrivalNowhere(int piDamage)
		{
			return new UWDamageRequest(piDamage, UWDamageTypes.None, UWDamageSource.ArrivalNowhere, UWDamageRule.Hit);
		}

		/// <summary>Plain damage of a type, nothing else - a blow without a request (UWBlowRouting).</summary>
		public static UWDamageRequest Plain(int piDamage, int piType)
		{
			return new UWDamageRequest(piDamage, piType, UWDamageSource.Other, UWDamageRule.Hit);
		}

		public override string ToString()
		{
			return Source + " " + Amount + " type " + Type + " (" + Rule + ")";
		}
	}

	/// <summary>What became of a request, for the host and the checks.</summary>
	public readonly struct UWDamageOutcome
	{
		/// <summary>The request's amount (the dice under UWDamageRule.Backfire).</summary>
		public readonly int Requested;

		/// <summary>After the armour of the part (the amount without armour).</summary>
		public readonly int AfterArmour;

		/// <summary>After the easy halving (AfterArmour without halving).</summary>
		public readonly int AfterEasy;

		/// <summary>What came off the hit points (a backfire: what its dice rolled).</summary>
		public readonly int Applied;

		/// <summary>It landed: a hit always (even one the proof wards off - the flash and the music
		/// do not ask), a backfire when it struck, a drain when it took anything, a miss never.</summary>
		public readonly bool Landed;

		/// <summary>The proof warded it off whole (check_res is all or nothing).</summary>
		public readonly bool Resisted;

		/// <summary>The hit points crossed to zero with it.</summary>
		public readonly bool Died;

		/// <summary>The combat shake's level (0 to 3), -1 when it did not shake.</summary>
		public readonly int ShakeLevel;

		public UWDamageOutcome(int piRequested, int piAfterArmour, int piAfterEasy, int piApplied, bool pbLanded,
			bool pbResisted, bool pbDied, int piShakeLevel)
		{
			Requested = piRequested;
			AfterArmour = piAfterArmour;
			AfterEasy = piAfterEasy;
			Applied = piApplied;
			Landed = pbLanded;
			Resisted = pbResisted;
			Died = pbDied;
			ShakeLevel = piShakeLevel;
		}

		public override string ToString()
		{
			return "requested " + Requested + ", armour " + AfterArmour + ", easy " + AfterEasy + ", applied " + Applied
				+ (Landed ? ", landed" : "") + (Resisted ? ", resisted" : "") + (Died ? ", died" : "")
				+ (ShakeLevel >= 0 ? ", shake " + ShakeLevel : "");
		}
	}

	/// <summary>
	/// THE ONE ROAD OF DAMAGE TO THE PLAYER (cleanup step 4.2, 2026-10-17): every site that lowers
	/// his hit points hands a UWDamageRequest here - through IUWWorld.Damage (UWBlowRouting) where
	/// the site has a world, straight from the vitals' own tick and the item use otherwise. The
	/// armour and the proof come from the vitals' host (IUWVitalsHost.GetArmourAt,
	/// DamageTypeProof); what the player sees and hears of it is told once afterwards, by
	/// UWPlayerVitals.Damaged (the flash, the music), besides the combat shake (CombatShakeDue).
	///
	/// THE ORDER OF THE RANDOM DRAWS is the sites' own: the armour and the halving draw nothing,
	/// the proof draws only for a magic type the proof has a magic bit for (UWDamageTypes.Scale),
	/// a backfire rolls its dice before it looks at the hit points.
	/// </summary>
	public static class UWDamagePipeline
	{
		/// <summary>The combat shake's level is the damage divided by four, capped at three
		/// (COMBAT.C do_damage).</summary>
		private const int ShakeDivisor = 4;

		private const int MaxShakeLevel = 3;

		/// <summary>A backfire brings nobody below this, and at or below it does nothing (SPELLS.C).</summary>
		public const int BackfireFloor = 3;

		/// <summary>The backfire rolls eight-sided dice (rollem(sub, 8)).</summary>
		private const int BackfireDieSides = 8;

		public static UWDamageOutcome Apply(UWPlayerVitals pOVitals, in UWDamageRequest pORequest)
		{
			if (pOVitals == null)
				return default;

			UWDamageOutcome lOOutcome;

			switch (pORequest.Rule)
			{
				case UWDamageRule.Backfire:
					lOOutcome = fBackfire(pOVitals, pORequest);
					break;

				case UWDamageRule.Drain:
					lOOutcome = fDrain(pOVitals, pORequest);
					break;

				case UWDamageRule.Miss:
					lOOutcome = new UWDamageOutcome(0, 0, 0, 0, false, false, false, -1);
					break;

				default:
					lOOutcome = fHit(pOVitals, pORequest);
					break;
			}

			pOVitals.TellDamaged(pORequest, lOOutcome);

			return lOOutcome;
		}

		/// <summary>
		/// damage_item on the player: the armour of the part and the easy halving for a blow or a
		/// missile (do_damage), then the proof - ALL OR NOTHING, uw1 knows no reduction (check_res;
		/// the player has no row of his own in the resistance table, what he wards off is his
		/// class 3 spells and enchantments, Flameproof against lava and fireballs, Poison Resistance
		/// against the poison, magic protection by dice). Untyped damage passes whole.
		///
		/// A BLOW OR A MISSILE WITH NOTHING LEFT after the armour and the halving stops there: the
		/// missile site never called the damage then, and the blow's type 4 draws nothing - so the
		/// draws stay as they were.
		/// </summary>
		private static UWDamageOutcome fHit(UWPlayerVitals pOVitals, in UWDamageRequest pORequest)
		{
			IUWVitalsHost lIHost = pOVitals.Host;
			int liAfterArmour = pORequest.Amount;

			if (pORequest.SubtractArmour)
				liAfterArmour = UWCritterCombat.SubtractArmour(pORequest.Amount,
					lIHost != null ? lIHost.GetArmourAt(pORequest.BodyPart) : 0);

			int liAfterEasy = pORequest.HalveOnEasy
				? UWCritterCombat.HalveOnEasy(liAfterArmour, pOVitals.IsEasyDifficulty)
				: liAfterArmour;

			int liApplied = 0;
			bool lbResisted = false;
			bool lbDied = false;

			if (!pORequest.SubtractArmour || liAfterEasy > 0)
			{
				int liProof = pORequest.Proof != UWDamageRequest.HostProof ? pORequest.Proof
					: (lIHost != null ? lIHost.DamageTypeProof : 0);

				liApplied = UWDamageTypes.Scale(liProof, liAfterEasy, pORequest.Type);
				lbResisted = liAfterEasy > 0 && liApplied <= 0;

				if (liApplied > 0)
					lbDied = pOVitals.TakeHit(liApplied, pORequest.Type);
			}

			int liShakeLevel = -1;

			// THE SHAKE after the damage, from the value before the halving; only when the halving
			// left something - whether the proof then swallowed it does not matter (C5, 2026-10-12).
			if (pORequest.Shakes && liAfterEasy > 0)
			{
				liShakeLevel = Math.Min(Math.Max(0, liAfterArmour) / ShakeDivisor, MaxShakeLevel);
				pOVitals.TellCombatShake(liShakeLevel);
			}

			return new UWDamageOutcome(pORequest.Amount, liAfterArmour, liAfterEasy, liApplied, true, lbResisted, lbDied,
				liShakeLevel);
		}

		/// <summary>
		/// SPELLS.C backfire, which the curse (PLAYDATA.C player_affected_by, class 9), Bronus'
		/// book, the leeches and a failed spell all call: rollem(dice, 8) - the dice plus that many
		/// rolls of 0 to 7 - BEFORE the hit points are looked at, so the draws keep their order
		/// when nothing happens; at three or fewer nothing, otherwise at least three left. It cannot
		/// kill. A curse without dice does not even roll (ApplyCurse's guard until 2026-10-17).
		/// </summary>
		private static UWDamageOutcome fBackfire(UWPlayerVitals pOVitals, in UWDamageRequest pORequest)
		{
			if (pORequest.Source == UWDamageSource.Curse && pORequest.Amount <= 0)
				return new UWDamageOutcome(pORequest.Amount, 0, 0, 0, false, false, false, -1);

			int liDamage = Math.Max(0, UWRandom.RollDice(pORequest.Amount, BackfireDieSides));

			if (pOVitals.CurrentHP <= BackfireFloor)
				return new UWDamageOutcome(pORequest.Amount, liDamage, liDamage, 0, false, false, false, -1);

			pOVitals.TakeBackfire(liDamage, BackfireFloor);

			return new UWDamageOutcome(pORequest.Amount, liDamage, liDamage, liDamage, true, false, false, -1);
		}

		/// <summary>The Void's drain: straight off the hit points, never below zero, no record
		/// (UW.EXE sets the points directly there).</summary>
		private static UWDamageOutcome fDrain(UWPlayerVitals pOVitals, in UWDamageRequest pORequest)
		{
			if (pORequest.Amount <= 0)
				return new UWDamageOutcome(pORequest.Amount, 0, 0, 0, false, false, false, -1);

			bool lbDied = pOVitals.TakeDrain(pORequest.Amount);

			return new UWDamageOutcome(pORequest.Amount, pORequest.Amount, pORequest.Amount, pORequest.Amount, true, false,
				lbDied, -1);
		}
	}
}
