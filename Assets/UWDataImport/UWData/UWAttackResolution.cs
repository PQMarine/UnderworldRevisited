namespace UWDataImport.UWData
{
	/// <summary>What a blow or a missile strikes - the three branches of frp_check and do_damage
	/// (UnderworldExhumed combat/COMBAT.C): a thing that is no creature, a creature, the player.</summary>
	public enum UWDefenderKind
	{
		/// <summary>A door, a chest, a barrel: no check, no armour (frp_check answers "hit" before
		/// any roll, do_damage subtracts armour only for creatures).</summary>
		Object,

		/// <summary>A creature: the check against its row's defence, the armour of the part from its
		/// row (a strong one's times 5 / 3).</summary>
		Creature,

		/// <summary>The player: the check less his protection of the part (cmbModTH); the armour of
		/// the part and the easy halving are the damage pipeline's (UWDamageRequest).</summary>
		Player
	}

	/// <summary>
	/// THE STRUCK BODY'S SIDE of one blow (cleanup step 4.9, 2026-10-18): what frp_check, do_damage
	/// and do_miss (UnderworldExhumed combat/COMBAT.C) ask of the defender. The creature (UWCritter),
	/// the player (UWPlayerCombatant over UWCharacter) and a thing (UWAttackResolution.ObjectDefender)
	/// answer it; the self-check uses UWFixedDefender. The part is picked (pickloc,
	/// UWArmourProtection.PickBodyPart) before the check, as resolve_attack does it (a creature's
	/// blow in UWAttackResolution.CreatureBlow since cleanup step 4.9b, 2026-10-18), and handed in.
	/// </summary>
	public interface IUWAttackDefender
	{
		UWDefenderKind DefenderKind { get; }

		/// <summary>The check value: a creature's row byte 0x12 (frp_check's cr->defence), the
		/// player's DefenceCheck (his row byte 0x12, PlayerStatusUpdate).</summary>
		int Defence { get; }

		/// <summary>cmbModTH[part]: the player's enchantment protection of the part, off the attack
		/// score; asked of the player only (frp_check: hitobj == 1).</summary>
		int ProtectionAt(int piPart);

		/// <summary>A creature's armour of the part: its row byte (part % 4), a byte of 0xFF falling
		/// back to byte 0, a strong one's times 5 / 3 (UWCritterCombat.GetCreatureArmour). Asked of a
		/// creature only; the player's armour is the damage pipeline's.</summary>
		int ArmourAt(int piPart);

		/// <summary>do_miss for the player as target: the piece in the slot of the part ((part + 1)
		/// &amp; 3) is there and is no leather. Asked of the player only.</summary>
		bool IsArmourMetalAt(int piPart);

		/// <summary>frp_check: a critical hit on the player wears the piece of the part
		/// (UWCritterCombat.GetCriticalEquipmentSlot) - between the critical's multiplier and the
		/// dice, as ours had it. Called for the player only.</summary>
		void OnCriticalHit(int piPart);
	}

	/// <summary>A defender whose answers are fixed: a thing (UWAttackResolution.ObjectDefender), and
	/// the self-check's stand-in for a creature or the player.</summary>
	public sealed class UWFixedDefender : IUWAttackDefender
	{
		private readonly UWDefenderKind meKind;
		private readonly int miDefence;
		private readonly int miArmour;
		private readonly int miProtection;
		private readonly bool mbArmourMetal;

		/// <summary>How often OnCriticalHit was called, and with which part last.</summary>
		public int CriticalHits;

		public int LastCriticalPart = UWAttackResolution.NoPart;

		public UWFixedDefender(UWDefenderKind peKind, int piDefence = 0, int piArmour = 0, int piProtection = 0,
			bool pbArmourMetal = false)
		{
			meKind = peKind;
			miDefence = piDefence;
			miArmour = piArmour;
			miProtection = piProtection;
			mbArmourMetal = pbArmourMetal;
		}

		public UWDefenderKind DefenderKind => meKind;

		public int Defence => miDefence;

		public int ProtectionAt(int piPart) => miProtection;

		public int ArmourAt(int piPart) => miArmour;

		public bool IsArmourMetalAt(int piPart) => mbArmourMetal;

		public void OnCriticalHit(int piPart)
		{
			CriticalHits++;
			LastCriticalPart = piPart;
		}
	}

	/// <summary>
	/// THE ATTACKER'S SIDE of one blow, as the original fills its attack globals before do_attack
	/// (UnderworldExhumed combat/COMBAT.C, cleanup step 4.9, 2026-10-18): askill (Score), damage
	/// (BaseDamage, before the critical and the dice), power (Charge), hitangle (Flank), fromwho
	/// (Index), and for do_miss the kinds of the attacker's own row - its weapon kind and, for a
	/// creature as target, its armour kind (the original's slip, kept). critter_attack's poison byte
	/// rides along for the player as target.
	/// </summary>
	public readonly struct UWAttackerStats
	{
		/// <summary>A creature's index in the master list, or UWCritterBrain.PlayerIndex.</summary>
		public readonly int Index;

		public readonly int Score;

		public readonly int BaseDamage;

		/// <summary>The charge value the dice are scaled by over 128 (a creature's table value, the
		/// player's mapped charge; both within a byte).</summary>
		public readonly int Charge;

		/// <summary>0 face to face to 4 from behind (compute_hitangle), into the check and the damage.</summary>
		public readonly int Flank;

		/// <summary>The attacker's row's weapon kind (1 metal) for a blocked blow's sound.</summary>
		public readonly int WeaponKind;

		/// <summary>The attacker's row's armour kind is metal - do_miss takes it for a creature as
		/// target.</summary>
		public readonly bool RowArmourMetal;

		/// <summary>critter_attack's poison: the row's byte 0x0F; 0 for none and for the player.</summary>
		public readonly int PoisonByte;

		public UWAttackerStats(int piIndex, int piScore, int piBaseDamage, int piCharge, int piFlank, int piWeaponKind,
			bool pbRowArmourMetal, int piPoisonByte = 0)
		{
			Index = piIndex;
			Score = piScore;
			BaseDamage = piBaseDamage;
			Charge = piCharge;
			Flank = piFlank;
			WeaponKind = piWeaponKind;
			RowArmourMetal = pbRowArmourMetal;
			PoisonByte = piPoisonByte;
		}

		public bool IsPlayer => Index == UWCritterBrain.PlayerIndex;

		/// <summary>A creature without table data: score 0 and damage 3, as the host had it.</summary>
		public const int NoRowDamage = 3;

		/// <summary>
		/// A creature's blow (critter_attack, COMBAT.C; NPCExecuteAttack labels 1629-1679): the
		/// table's to-hit and damage bytes of attack i, half the signed byte 0x11 and a fifth of byte
		/// 5; a strong individual adds 7 + RNG(6) to the score, then 4 + RNG(12) to the damage (the
		/// two draws in that order). Without a row: score 0, damage 3, no draw. Moved out of
		/// UWCritter.fGetAttackNumbers (cleanup step 4.9, 2026-10-18).
		/// </summary>
		public static UWAttackerStats Creature(UWObjectClassProperties.Critter pORow, bool pbHasRow, int piAttack, bool pbStrong,
			int piIndex, int piCharge, int piFlank)
		{
			int liScore = 0;
			int liDamage = NoRowDamage;

			if (pbHasRow)
			{
				liScore = UWCritterCombat.GetAttackScore(pORow, piAttack);
				liDamage = UWCritterCombat.GetAttackDamage(pORow, piAttack);

				if (pbStrong)
				{
					liScore += UWCritterCombat.GetStrongScoreBonus();
					liDamage += UWCritterCombat.GetStrongDamageBonus();
				}
			}

			return new UWAttackerStats(piIndex, liScore, liDamage, piCharge, piFlank, pORow.WeaponKind,
				pORow.ArmourKind == UWCritterCombat.MetalKind, pbHasRow ? pORow.PoisonDamage : 0);
		}

		public override string ToString()
		{
			return "attacker " + Index + " score " + Score + " damage " + BaseDamage + " charge " + Charge + " flank " + Flank;
		}
	}

	/// <summary>What became of one blow or missile hit (UWAttackResolution).</summary>
	public readonly struct UWAttackOutcome
	{
		/// <summary>The check: Success for a thing and for a missile, which roll none.</summary>
		public readonly UWSkillCheck.ResultEnum Result;

		public readonly UWDefenderKind DefenderKind;

		/// <summary>The part the caller picked, or UWAttackResolution.NoPart.</summary>
		public readonly int Part;

		/// <summary>The damage after the dice, the charge and the flank - before the armour (ddone at
		/// the hit sound); 0 on a miss.</summary>
		public readonly int Rolled;

		/// <summary>What lands: a creature's after its armour, a thing's as rolled, the player's as
		/// rolled (his armour and the halving are the pipeline's); 0 on a miss.</summary>
		public readonly int Damage;

		/// <summary>do_miss's 7 or 8 for a blocked blow, UWAttackResolution.NoSound otherwise.</summary>
		public readonly int BlockedSound;

		/// <summary>Who struck: a creature's index or UWCritterBrain.PlayerIndex.</summary>
		public readonly int Attacker;

		/// <summary>A missile's hit (missile_thwack), with the missile's damage type.</summary>
		public readonly bool IsMissile;

		public readonly int DamageType;

		/// <summary>For the player as defender: the request the damage pipeline takes - a blow's,
		/// a miss's or a missile's; default for any other defender.</summary>
		public readonly UWDamageRequest PlayerRequest;

		public UWAttackOutcome(UWSkillCheck.ResultEnum peResult, UWDefenderKind peKind, int piPart, int piRolled, int piDamage,
			int piBlockedSound, int piAttacker, bool pbMissile, int piDamageType, UWDamageRequest pORequest)
		{
			Result = peResult;
			DefenderKind = peKind;
			Part = piPart;
			Rolled = piRolled;
			Damage = piDamage;
			BlockedSound = piBlockedSound;
			Attacker = piAttacker;
			IsMissile = pbMissile;
			DamageType = piDamageType;
			PlayerRequest = pORequest;
		}

		public bool Hit => UWSkillCheck.IsSuccess(Result);

		public bool Critical => Result == UWSkillCheck.ResultEnum.CriticalSuccess;

		/// <summary>The hit sound's volume: four times the damage BEFORE the armour (do_damage plays
		/// effect 3 or 4 with ddone &lt;&lt; 2 before it subtracts the armour, so an absorbed blow
		/// is still heard).</summary>
		public int HitVolume => UWAttackResolution.HitVolume(Rolled);

		/// <summary>do_damage returns before the blood or the flash, and the player's eyes, when
		/// nothing is left after the armour.</summary>
		public bool LeavesTrace => Damage > 0;

		/// <summary>
		/// THE BLOW AS IT LANDS through IUWWorld.Damage: the player's request whole
		/// (UWBlow.ToPlayer), a missile's hit charged to its launcher (UWBlow.Missile), the player's
		/// blow (UWBlow.PlayerBlow) or a creature's (UWBlow.CreatureBlow) - with 0 on a miss, so the
		/// struck creature still records the attacker (do_attack hands damage_item 0).
		/// </summary>
		public UWBlow Blow
		{
			get
			{
				if (DefenderKind == UWDefenderKind.Player)
					return UWBlow.ToPlayer(PlayerRequest);

				if (IsMissile)
					return UWBlow.Missile(Damage, DamageType, Attacker);

				return Attacker == UWCritterBrain.PlayerIndex ? UWBlow.PlayerBlow(Damage) : UWBlow.CreatureBlow(Damage, Attacker);
			}
		}

		public override string ToString()
		{
			return Result + " part " + Part + " rolled " + Rolled + " damage " + Damage
				+ (BlockedSound != UWAttackResolution.NoSound ? " blocked " + BlockedSound : "");
		}
	}

	/// <summary>
	/// ONE BLOW, WHOEVER STRIKES WHOM (cleanup step 4.9, 2026-10-18; section 7.4 of
	/// Docs/CLEANUP-PLAN.md): what the original computes in common for a creature's blow
	/// (critter_attack -> do_attack), the player's melee (DoPlayerWeapon -> do_attack) and a missile
	/// on the player (missile_thwack -> do_damage), UnderworldExhumed combat/COMBAT.C:
	///
	///   do_attack      the same side is spared (SparesSameSide)
	///   frp_check      a thing is always hit, without a roll; else the score less the player's
	///                  protection of the part plus the flank against the defence (Check);
	///                  a critical multiplies the base 1 or 2 times and wears the player's piece
	///   do_damage      the dice over max(2, base), times the charge over 128, plus the flank;
	///                  the hit volume from that; a creature's armour of the part (Land)
	///   do_miss        a blocked blow sounds 7 for a metal weapon on metal, else 8 (BlockedSound)
	///   missile_thwack the missile's base through do_damage's dice at the hit, no check (Missile)
	///   critter_attack the poison after a hit on the player (PoisonNibble)
	///
	/// Until then the creature's three strike methods (UWCritter.fStrikePlayer, fStrikeCreature,
	/// fStrikeObject) and the player's UWPlayerAttack.ResolveMelee (over UWCombat.ComputeDamage)
	/// each ran this chain themselves, and the blocked sound was chosen in two places.
	///
	/// THE ORDER OF THE DRAWS follows the original's since cleanup step 4.9b (2026-10-18, by the
	/// user's decision): the part before the check for every blow (CreatureBlow; the player's blow
	/// picks it before ResolveMelee), the player's weapon worn between the check and the dice
	/// (PlayerBlow).
	///
	/// WHAT STAYS WITH THE CALLERS: finding the defender and its heights, the sounds, the
	/// pictures, the eyes and the wear of the player's weapon. The player's armour, the easy halving
	/// and the shake stay UWDamagePipeline's (the outcome hands it the request), the landing
	/// IUWWorld.Damage's (UWAttackOutcome.Blow). The numbers themselves stay UWCritterCombat's and
	/// UWCombat's.
	/// </summary>
	public static class UWAttackResolution
	{
		/// <summary>No part picked: a thing, or a creature's blow before its part is known.</summary>
		public const int NoPart = UWDamageRequest.NoBodyPart;

		/// <summary>No blocked sound: the blow hit.</summary>
		public const int NoSound = -1;

		/// <summary>Every thing that is no creature: no check, no armour, no wear.</summary>
		public static readonly IUWAttackDefender ObjectDefender = new UWFixedDefender(UWDefenderKind.Object);

		/// <summary>
		/// do_attack: a creature does not strike a creature of its own side - a creature attacker, a
		/// creature defender, the same ally bit. The blow is dropped: no damage_item, no sound. The
		/// player strikes anyone, and anyone strikes the player.
		/// </summary>
		public static bool SparesSameSide(bool pbAttackerIsPlayer, UWDefenderKind peDefender, bool pbAttackerAlly, bool pbDefenderAlly)
		{
			return !pbAttackerIsPlayer && peDefender == UWDefenderKind.Creature && pbAttackerAlly == pbDefenderAlly;
		}

		/// <summary>
		/// frp_check's roll: a thing is hit without one; otherwise the score less the player's
		/// protection of the part (cmbModTH, the player as target only) plus the flank against the
		/// defence plus RNG(31), banded by UWSkillCheck (UWCritterCombat.ResolveHit). One draw, none
		/// for a thing. A creature's protection is never asked, so its part may still be NoPart.
		/// </summary>
		public static UWSkillCheck.ResultEnum Check(in UWAttackerStats pOAttacker, IUWAttackDefender pIDefender, int piPart)
		{
			if (pIDefender == null || pIDefender.DefenderKind == UWDefenderKind.Object)
				return UWSkillCheck.ResultEnum.Success;

			int liProtection = pIDefender.DefenderKind == UWDefenderKind.Player ? pIDefender.ProtectionAt(piPart) : 0;

			return UWCritterCombat.ResolveHit(pOAttacker.Score, pOAttacker.Flank, liProtection, pIDefender.Defence);
		}

		/// <summary>
		/// What follows the check, in its order: a miss gives the blocked sound and nothing else (no
		/// draw); a hit on a critical multiplies the base by GetCriticalMultiplier (one draw) and,
		/// for the player as target, wears the piece of the part (the defender's OnCriticalHit, its
		/// own draws); then the dice (UWCritterCombat.RollDamage, minimum 2) times the charge plus the
		/// flank (ScaleByCharge); then a creature's armour of the part. A thing takes the roll whole,
		/// the player the request of a creature's blow (UWDamageRequest.CreatureBlow, the red flash on
		/// a critical) or of its miss (CreatureMiss).
		///
		/// Split from Check for the player's blow, whose weapon wears in frp_check between the two
		/// (PlayerBlow, cleanup step 4.9b, 2026-10-18). The creature's blow on another creature,
		/// which picked the part between them until that step, picks it before the check since
		/// then, as the original does (CreatureBlow).
		/// </summary>
		public static UWAttackOutcome Land(in UWAttackerStats pOAttacker, IUWAttackDefender pIDefender, int piPart,
			UWSkillCheck.ResultEnum peResult)
		{
			UWDefenderKind leKind = pIDefender != null ? pIDefender.DefenderKind : UWDefenderKind.Object;

			if (!UWSkillCheck.IsSuccess(peResult))
				return new UWAttackOutcome(peResult, leKind, piPart, 0, 0, BlockedSound(pOAttacker, pIDefender, piPart),
					pOAttacker.Index, false, UWDamageTypes.Physical,
					leKind == UWDefenderKind.Player ? UWDamageRequest.CreatureMiss() : default);

			int liBase = pOAttacker.BaseDamage;
			bool lbCritical = peResult == UWSkillCheck.ResultEnum.CriticalSuccess;

			// A critical hit multiplies the base value before rolling. The
			// factor is 1 or 2 - (48 + (RNG & 31)) divided by 32 (the critical branch of the
			// attack results; the reference rolled 0 to 29), so in half the cases
			// double damage and otherwise no bonus at all.
			//
			// BEFORE THE MINIMUM (do_damage raises the damage to 2 only after the multiplier,
			// UnderworldExhumed COMBAT.C, 2026-10-11; the Exhumed audit's row 31): until then ours
			// doubled the already raised value, so a base of 0 or 1 hit for 4.
			// (Moved from UWCombat.ComputeDamage, the player's blow, with cleanup step 4.9.)
			if (lbCritical)
			{
				liBase *= UWCritterCombat.GetCriticalMultiplier();

				if (leKind == UWDefenderKind.Player)
					pIDefender.OnCriticalHit(piPart);
			}

			// The FLANKING BONUS is added before the armour (AttackerAppliesFinalDamage_seg022_8A5):
			// ExecuteAttack computes it for every melee blow, the player's too; a
			// missile has none.
			int liRolled = UWCritterCombat.ScaleByCharge(UWCritterCombat.RollDamage(liBase), pOAttacker.Charge, pOAttacker.Flank);
			int liDamage = leKind == UWDefenderKind.Creature
				? UWCritterCombat.SubtractArmour(liRolled, pIDefender.ArmourAt(piPart))
				: liRolled;

			return new UWAttackOutcome(peResult, leKind, piPart, liRolled, liDamage, NoSound, pOAttacker.Index, false,
				UWDamageTypes.Physical,
				leKind == UWDefenderKind.Player ? UWDamageRequest.CreatureBlow(liRolled, piPart, lbCritical) : default);
		}

		/// <summary>do_attack after the target and the part are found: frp_check, then do_damage or
		/// do_miss (Check, then Land).</summary>
		public static UWAttackOutcome Resolve(in UWAttackerStats pOAttacker, IUWAttackDefender pIDefender, int piPart)
		{
			return Land(pOAttacker, pIDefender, piPart, Check(pOAttacker, pIDefender, piPart));
		}

		/// <summary>
		/// THE PLAYER'S BLOW with frp_check's wear of his weapon at its place (cleanup step 4.9b,
		/// 2026-10-18, by the user's decision to follow the original): the check, then the wear
		/// (WearsPlayersWeapon; pFWearWeapon rolls the dice it is handed and wears the weapon), then
		/// the critical's multiplier and do_damage's dice (Land) - a worn weapon's blow is a door's,
		/// which has no critical, or a bad miss, which has neither. frp_check (UnderworldExhumed
		/// combat/COMBAT.C:282) wears the weapon before do_attack reaches do_damage (COMBAT.C:505);
		/// until then ours wore it after the dice (Interaction, after UWPlayerAttack.ResolveMelee).
		/// </summary>
		public static UWAttackOutcome PlayerBlow(in UWAttackerStats pOAttacker, IUWAttackDefender pIDefender, int piPart,
			int piTargetObjectId, bool pbTargetWearsWeapon, System.Action<int, int> pFWearWeapon)
		{
			UWSkillCheck.ResultEnum leResult = Check(pOAttacker, pIDefender, piPart);
			UWDefenderKind leKind = pIDefender != null ? pIDefender.DefenderKind : UWDefenderKind.Object;

			if (pFWearWeapon != null
				&& WearsPlayersWeapon(leKind, piTargetObjectId, leResult, pbTargetWearsWeapon, out int liDiceCount, out int liDiceRange))
				pFWearWeapon(liDiceCount, liDiceRange);

			return Land(pOAttacker, pIDefender, piPart, leResult);
		}

		/// <summary>
		/// frp_check's wear of the player's weapon (UnderworldExhumed combat/COMBAT.C:292 and 316):
		/// a blow at a door wears it on the roll rand() * 12 / 0x8000 below twice the door's id
		/// &amp; 7 (UWEquipmentWear.WearsWeaponOnDoor: one draw for a door, none for another thing),
		/// by 2d4; a bad miss against a creature that does not spare weapons (row byte 0x0A bit 0,
		/// UWEquipmentWear.WearsWeaponOnCriticalFailure) by 2d3. Moved out of Interaction with
		/// cleanup step 4.9b (2026-10-18), the rule unchanged.
		/// </summary>
		public static bool WearsPlayersWeapon(UWDefenderKind peKind, int piTargetObjectId, UWSkillCheck.ResultEnum peResult,
			bool pbCreatureWearsIt, out int piDiceCount, out int piDiceRange)
		{
			piDiceCount = 0;
			piDiceRange = 0;

			if (peKind == UWDefenderKind.Object)
			{
				if (!UWEquipmentWear.WearsWeaponOnDoor(piTargetObjectId))
					return false;

				piDiceCount = UWEquipmentWear.DoorDiceCount;
				piDiceRange = UWEquipmentWear.DoorDiceRange;

				return true;
			}

			if (peKind != UWDefenderKind.Creature || peResult != UWSkillCheck.ResultEnum.CriticalFailure || !pbCreatureWearsIt)
				return false;

			piDiceCount = UWEquipmentWear.CriticalFailureDiceCount;
			piDiceRange = UWEquipmentWear.CriticalFailureDiceRange;

			return true;
		}

		/// <summary>
		/// A CREATURE'S BLOW ON A BODY - the player or another creature - with its draws in the
		/// original's order (cleanup step 4.9b, 2026-10-18, by the user's decision to follow the
		/// original): the ninth of the attacker's height the blow comes from (rand() % 9, handed to
		/// critter_attack as the swing, UnderworldExhumed critter/AI.C:1155; the probe
		/// UWCritterCombat.RollCritterBlowProbe), the strong creature's bonus to the score and the
		/// damage (critter_attack, combat/COMBAT.C:802; UWAttackerStats.Creature), the part from the
		/// probe's heights against the defender's (resolve_attack -> pickloc, COMBAT.C:260), and only
		/// then frp_check and do_damage or do_miss (do_attack, COMBAT.C:501-505; Resolve).
		///
		/// Until then the creature on the player drew the ninth, the part, then the strong bonus,
		/// and the creature on another creature the strong bonus and the check before the ninth and
		/// the part (UWCritter.fStrikePlayer and fStrikeCreature, reported with cleanup step 4.9).
		/// A thing (UWCritter.fStrikeObject) is not struck through here: ours draws neither the
		/// ninth nor a part for it.
		/// </summary>
		/// <param name="piOwnFeet">The attacker's z and COMOBJ height, for the blow's probe.</param>
		/// <param name="piDefenderFeet">The defender's z and top (its z plus its height), for pickloc.</param>
		public static UWAttackOutcome CreatureBlow(UWObjectClassProperties.Critter pORow, bool pbHasRow, int piAttack,
			bool pbStrong, int piIndex, int piCharge, int piFlank, int piOwnFeet, int piOwnHeight,
			IUWAttackDefender pIDefender, int piDefenderFeet, int piDefenderTop, out UWAttackerStats pOAttacker)
		{
			UWCritterCombat.RollCritterBlowProbe(piOwnFeet, piOwnHeight, out int liProbeBottom, out int liProbeTop);

			pOAttacker = UWAttackerStats.Creature(pORow, pbHasRow, piAttack, pbStrong, piIndex, piCharge, piFlank);

			int liPart = UWArmourProtection.PickBodyPart(piDefenderFeet, piDefenderTop, liProbeBottom, liProbeTop);

			return Resolve(pOAttacker, pIDefender, liPart);
		}

		/// <summary>
		/// do_miss's blocked blow (UWCritterCombat.GetBlockedSound): the attacker's row's weapon kind
		/// against, for the player as target, the piece he wears on the part, for a creature as target
		/// the ATTACKER's row's armour kind (the original's slip, kept). A thing is never missed.
		/// </summary>
		public static int BlockedSound(in UWAttackerStats pOAttacker, IUWAttackDefender pIDefender, int piPart)
		{
			UWDefenderKind leKind = pIDefender != null ? pIDefender.DefenderKind : UWDefenderKind.Object;

			if (leKind == UWDefenderKind.Object)
				return NoSound;

			bool lbArmourMetal = leKind == UWDefenderKind.Player ? pIDefender.IsArmourMetalAt(piPart) : pOAttacker.RowArmourMetal;

			return UWCritterCombat.GetBlockedSound(pOAttacker.WeaponKind, lbArmourMetal);
		}

		/// <summary>A flight that carries no damage of its own - the quake's boulder, a thing knocked
		/// loose, a throw of what is no missile: its hit lands 0 and rolls no dice (as the 0 these
		/// flights carried until cleanup step 4.9b, 2026-10-18).</summary>
		public const int NoMissileDamage = -1;

		/// <summary>
		/// missile_thwack -> do_damage: a missile's hit with no check, its damage ROLLED AT THE HIT
		/// from the base the launch computed (piBaseDamage: the missile table's damage, the player's
		/// ammunition scaled by his Missile skill) - do_damage's dice with missile_thwack's power 0x80
		/// and no angle (RollMissile). A creature takes it less the armour of the part (the same byte
		/// for the missile's part + 4); the player the missile's request (UWDamageRequest.Missile:
		/// the armour, the halving, the shake (every missile's) and the music by piOwner, the missile's
		/// last_hit - see UWMissileHitRules.MusicFor, 2026-10-19); a thing the damage whole.
		/// Every missile's hit since cleanup step 4.9b (2026-10-18): on the player, on a creature
		/// (UWMissiles.StrikeCreature, whose part - picked once, as missile_thwack picks it at
		/// UnderworldExhumed combat/COMBAT.C:772 - also places the blood) and on a thing. The part
		/// is picked before the dice, as pickloc comes before do_damage (COMBAT.C:772-779).
		/// </summary>
		public static UWAttackOutcome Missile(int piBaseDamage, int piDamageType, int piAttacker, IUWAttackDefender pIDefender,
			int piPart, int piOwner)
		{
			UWDefenderKind leKind = pIDefender != null ? pIDefender.DefenderKind : UWDefenderKind.Object;
			int liRolled = RollMissile(piBaseDamage);
			int liDamage = leKind == UWDefenderKind.Creature
				? UWCritterCombat.SubtractArmour(liRolled, pIDefender.ArmourAt(piPart))
				: liRolled;

			return new UWAttackOutcome(UWSkillCheck.ResultEnum.Success, leKind, piPart, liRolled, liDamage, NoSound, piAttacker,
				true, piDamageType,
				leKind == UWDefenderKind.Player
					? UWDamageRequest.Missile(liRolled, piDamageType, piPart, piOwner)
					: default);
		}

		/// <summary>
		/// A MISSILE'S DICE AT ITS HIT (cleanup step 4.9b, 2026-10-18, by the user's decision to
		/// follow the original): missile_newhit hands missile_thwack the table's damage
		/// (UnderworldExhumed motion/OBJPHYS.C:131), and do_damage rolls it (combat/COMBAT.C:346-356)
		/// with the power 0x80 missile_thwack sets - UWCritterCombat.RollMissileDamage. Until then
		/// every launch rolled it (the player's shot, throw and spell, a creature's missile, a
		/// trap's bolt) and the flight carried the result. NoMissileDamage rolls nothing and is 0.
		/// </summary>
		public static int RollMissile(int piBaseDamage)
		{
			return piBaseDamage < 0 ? 0 : UWCritterCombat.RollMissileDamage(piBaseDamage);
		}

		/// <summary>The hit sound's volume from the damage before the armour (do_damage: ddone &lt;&lt; 2).</summary>
		public static int HitVolume(int piRolled)
		{
			return piRolled << 2;
		}

		/// <summary>
		/// critter_attack's tail: after a HIT on the player (even one the armour swallowed) by an
		/// attacker with a poison byte above his present poison, and unless a Poison Resistance rules
		/// it out (UWCritterCombat.ShouldPoison), his poison becomes the byte's low nibble. -1 for
		/// none. Asked after the damage has landed, as ours always did.
		/// </summary>
		public static int PoisonNibble(in UWAttackerStats pOAttacker, in UWAttackOutcome pOOutcome, int piCurrentNibble,
			int piDamageTypeProof)
		{
			if (!pOOutcome.Hit || pOOutcome.IsMissile || pOOutcome.DefenderKind != UWDefenderKind.Player)
				return -1;

			// Past the armour, as the game has it (IUWCombatVariant; the first game asks no armour).
			if (!UWGameSession.Current.Game.Combat.PoisonGetsThrough(pOAttacker, pOOutcome))
				return -1;

			if (!UWCritterCombat.ShouldPoison(piCurrentNibble, pOAttacker.PoisonByte, piDamageTypeProof))
				return -1;

			return UWCritterCombat.GetPoisonNibble(pOAttacker.PoisonByte);
		}
	}
}
