namespace UWDataImport.UWData
{
	/// <summary>Where a blow comes from - one per kind of damage site that goes through
	/// IUWWorld.Damage (cleanup step 3.3, 2026-10-17). It names the site for the log and the checks;
	/// damage to the player is one source, Player, whose request names its site (cleanup step 4.2).</summary>
	public enum UWBlowSource
	{
		/// <summary>The player's melee blow (Interaction.fApplyMeleeHit).</summary>
		PlayerBlow,

		/// <summary>A creature's blow on another creature, a door or an object in the way.</summary>
		CreatureBlow,

		/// <summary>A missile's hit (UWMissiles): an arrow, a bolt, a stone, a spell missile.</summary>
		Missile,

		/// <summary>Everything on a tile takes a blow of its own (UWTileBlast): a fireball's or a
		/// lightning bolt's explosion, Flame Wind, Sheet Lightning.</summary>
		Blast,

		/// <summary>A class 7 spell on a clicked creature (UWTargetSpell): Smite Undead, Poison.</summary>
		TargetSpell,

		/// <summary>A damage trap (UWTrapRules) on the player, a creature or a thing in
		/// flight.</summary>
		Trap,

		/// <summary>The player's Rune of Warding on a creature stepping on it.</summary>
		Ward,

		/// <summary>The lava under a creature.</summary>
		Lava,

		/// <summary>Damage to the player described by its UWDamageRequest (cleanup step 4.2,
		/// 2026-10-17): UWBlow.ToPlayer. It strikes the player only. The sleep's water and lava,
		/// the curse and the backfire were blow sources of their own until then.</summary>
		Player
	}

	/// <summary>
	/// ONE BLOW, as every damage site hands it to IUWWorld.Damage (cleanup step 3.3, 2026-10-17;
	/// section 7.2 of Docs/CLEANUP-PLAN.md). Until then each site called UWDamageable.ApplyDamage or
	/// UWCharacter.ApplyDamage itself and told the creature its attacker in one of three ways - the
	/// missile and the blast through AnnounceAttacker, the creature's blow by setting the pending
	/// attacker around the call, the trap, the ward and the lava through ApplyDamageWithoutAttacker
	/// (section 4.3: "Damage to objects and creatures").
	///
	/// THE AMOUNT IS THE SITE'S OWN: every site still computes its damage as before - the dice of a
	/// blast, the charge and the flank of a blow - and passes the result. The blow carries only what
	/// the landing needs: the amount, the damage type and the attacker. A blow to the player carries
	/// the site's UWDamageRequest instead (ToPlayer; the armour and the halving are the damage
	/// pipeline's since cleanup step 4.2, 2026-10-17); the creatures' formulas are unified later
	/// (UWAttackResolution).
	///
	/// THE ATTACKER TRAVELS WITH THE BLOW (cleanup step 4.8, 2026-10-18). DamageNPC (54563-54802;
	/// damage_critter, UnderworldExhumed AI.C) stores the attacker in byte 0x12 - one for the
	/// player, and FOR A PROJECTILE THE PROJECTILE'S OWN BYTE 0x12, which is its launcher. Only then
	/// can the damage reaction do its work: it ignores a hit that came from neither the player nor
	/// an ally of either side, and it makes the attacker its goal target (see
	/// UWCritterBrain.fDamageReaction). The kin alarm, the experience and the combat music follow
	/// from the same attacker. Until 2026-10-18 the struck creature learned it in one of three ways
	/// (UWBlowAttribution: untold, announced before the blow and left pending for the next damage
	/// event, or for this blow only) through a pending attacker kept in UWCritter; every creature's
	/// damage event took the announcement, so the three always came down to the attacker named
	/// here, which UWDamageable.ApplyDamage now hands to the creature with the blow itself.
	/// </summary>
	public readonly struct UWBlow
	{
		/// <summary>The attacker of damage nobody dealt: damage_item with no attacker (from 0,
		/// UnderworldExhumed AI.C damage_critter: who = 0).</summary>
		public const int Nobody = 0;

		/// <summary>No attacker given to a blast: nobody's, as Nobody (see Blast).</summary>
		public const int NoBlastAttacker = -1;

		/// <summary>Hit points; for a blow to the player its request's amount.</summary>
		public readonly int Amount;

		/// <summary>A UWDamageTypes value.</summary>
		public readonly int DamageType;

		/// <summary>Whom a struck creature holds responsible (byte 0x12): a creature's index,
		/// UWCritterBrain.PlayerIndex, or Nobody.</summary>
		public readonly int Attacker;

		public readonly UWBlowSource Source;

		/// <summary>What the player's end makes of the blow (UWBlowSource.Player); default for any
		/// other - see ForPlayer.</summary>
		public readonly UWDamageRequest PlayerRequest;

		private UWBlow(int piAmount, int piDamageType, int piAttacker, UWBlowSource peSource, UWDamageRequest pORequest = default)
		{
			Amount = piAmount;
			DamageType = piDamageType;
			Attacker = piAttacker;
			Source = peSource;
			PlayerRequest = pORequest;
		}

		/// <summary>The blow strikes the player only (ToPlayer).</summary>
		public bool StrikesPlayerOnly => Source == UWBlowSource.Player;

		/// <summary>
		/// DAMAGE TO THE PLAYER through the world (cleanup step 4.2, 2026-10-17): the site's request
		/// - its type, armour, halving, shake, flash and music, the table of section 7.5 - goes
		/// whole to the player's end, which hands it to UWDamagePipeline. Nobody's: the player keeps
		/// no attacker.
		/// </summary>
		public static UWBlow ToPlayer(in UWDamageRequest pORequest)
		{
			return new UWBlow(pORequest.Amount, pORequest.Type, Nobody, UWBlowSource.Player, pORequest);
		}

		/// <summary>The request the player's end hands to the pipeline: the blow's own for
		/// ToPlayer; anything else plain damage of its type, as the player's end took every blow
		/// until cleanup step 4.2.</summary>
		public UWDamageRequest ForPlayer()
		{
			return Source == UWBlowSource.Player ? PlayerRequest : UWDamageRequest.Plain(Amount, DamageType);
		}

		/// <summary>The player's melee blow. The player's own strike is bash damage, just like a
		/// creature's (reference: combat.AttackerAppliesFinalDamage with four), charged to the
		/// player. A miss is this blow with 0 (cleanup step 4.8, 2026-10-18): DamageObject with
		/// zero, so the creature still records the player and reacts (per user on the original,
		/// 2026-09-12); until then UWCritter.OnMissedByPlayer told the mind past the body.</summary>
		public static UWBlow PlayerBlow(int piDamage)
		{
			return new UWBlow(piDamage, UWDamageTypes.Physical, UWCritterBrain.PlayerIndex, UWBlowSource.PlayerBlow);
		}

		/// <summary>A creature's blow: DamageObject with type 4, charged to the attacking
		/// creature's index - on a creature, a door it bashes or an object in the way (an object
		/// keeps no attacker).</summary>
		public static UWBlow CreatureBlow(int piDamage, int piAttacker)
		{
			return new UWBlow(piDamage, UWDamageTypes.Physical, piAttacker, UWBlowSource.CreatureBlow);
		}

		/// <summary>A missile's hit, charged to its launcher (the player for his own and, as
		/// UWMissiles has always done, for a missile without a launching creature).</summary>
		public static UWBlow Missile(int piDamage, int piDamageType, int piAttacker)
		{
			return new UWBlow(piDamage, piDamageType, piAttacker, UWBlowSource.Missile);
		}

		/// <summary>
		/// One thing's blow of a blast, CHARGED TO THE LAUNCHER (cleanup step 4.10, 2026-10-18):
		/// a creature's index, the player's, or nobody (NoBlastAttacker or Nobody - a trap's
		/// fireball). The original hands the blast its launcher all the way down: the detonating
		/// missile's last_hit (land_obj, UnderworldExhumed OBJPHYS.C) or the area spell's caster
		/// (gronk_area's src, SPELLS.C) goes to mts_doanim (EFFECT.C) and damage_square (SPELLS.C),
		/// which calls damage_item with Obj_IntTMem(src) - none for 0 - and damage_critter (AI.C)
		/// stores that creature's own index, even when it is the struck creature itself. So a mage
		/// caught in its own fireball is hit by the mage: no kin alarm, no combat music, no
		/// experience for the player on a kill, and the damage reaction ignores it unless allies
		/// are involved (critter_mv); a blast nobody launched records no attacker at all.
		///
		/// Until 2026-10-18 a blast on its own caster or with no attacker was charged to the
		/// player, as the untold blast's damage event had done by default until cleanup step 4.8
		/// (found in step 4.8): the kin of a mage caught in its own fireball turned on the player,
		/// and its death paid him experience.
		/// </summary>
		public static UWBlow Blast(int piDamage, int piDamageType, int piAttacker)
		{
			return new UWBlow(piDamage, piDamageType, piAttacker > 0 ? piAttacker : Nobody, UWBlowSource.Blast);
		}

		/// <summary>A class 7 spell's damage on its target (Smite Undead, Poison), charged to the
		/// player, as its untold damage event was until cleanup step 4.8 (L-review: "no announce,
		/// verify against the original").</summary>
		public static UWBlow TargetSpell(int piDamage, int piDamageType)
		{
			return new UWBlow(piDamage, piDamageType, UWCritterBrain.PlayerIndex, UWBlowSource.TargetSpell);
		}

		/// <summary>
		/// DAMAGE THAT NOBODY DEALT: damage_item with no attacker (from 0, UnderworldExhumed
		/// AI.C damage_critter: who = 0) - the lava under the creature, a damage trap (whack_thing,
		/// WORLDEV.C) and the player's Rune of Warding (TRAP_WARD, TRIGGER.C, which hands the damage
		/// to whack_thing). The tally grows and the creature may die, but no attacker is recorded,
		/// no kin alarm raised, no experience paid and no combat music started. Until 2026-10-15 the
		/// trap and the ward fell back to the player as the attacker, so a monster stepping on the
		/// rune turned on him with its kin and a kill paid experience (audit round 2, A7 / KR D3).
		///
		/// A damage trap's blow: type 4 on a creature or a thing in flight (DamageObject,
		/// as UWTrapRules has it), untyped on the player - the site passes the type.
		/// </summary>
		public static UWBlow Trap(int piDamage, int piDamageType)
		{
			return new UWBlow(piDamage, piDamageType, Nobody, UWBlowSource.Trap);
		}

		/// <summary>The Rune of Warding on a creature: type 4, nobody's (see Trap).</summary>
		public static UWBlow Ward(int piDamage)
		{
			return new UWBlow(piDamage, UWDamageTypes.Physical, Nobody, UWBlowSource.Ward);
		}

		/// <summary>The lava under a creature: plain fire, nobody's (see Trap).</summary>
		public static UWBlow Lava(int piDamage)
		{
			return new UWBlow(piDamage, UWDamageTypes.PlainFire, Nobody, UWBlowSource.Lava);
		}

		public override string ToString()
		{
			return Source + " " + Amount + " type " + DamageType + " by " + Attacker;
		}
	}

	/// <summary>What a blow strikes.</summary>
	public enum UWDamageTargetKind
	{
		Player,

		/// <summary>A creature, by its index in the level's master list.</summary>
		Creature,

		/// <summary>A door, a chest, a barrel - an object with hit points of its own.</summary>
		Object,

		/// <summary>A thing in flight, by its flight index (UWMotionHost): the damage trap's strike
		/// on a thrown object that set it off.</summary>
		ThingInFlight
	}

	/// <summary>
	/// THE TARGET OF A BLOW (cleanup step 3.3, 2026-10-17). Engine-free it is a kind and an index or
	/// an object's data; a site that already holds the struck body hands it along in Body, so the
	/// world need not look it up again (and finds the very body the site found - a frame post
	/// belongs to its door, a creature's collider to its creature). Body is the engine's own and
	/// opaque here: the Unity world puts the UWDamageable there.
	/// </summary>
	public readonly struct UWDamageTarget
	{
		public static readonly UWDamageTarget ThePlayer = new UWDamageTarget(UWDamageTargetKind.Player, UWCritterBrain.PlayerIndex,
			null, null);

		public readonly UWDamageTargetKind Kind;

		/// <summary>A creature's index in the master list, a thing in flight's flight index; the
		/// player's index for the player; 0 for an object.</summary>
		public readonly int Index;

		/// <summary>The struck object's data, when known.</summary>
		public readonly UWObject Object;

		/// <summary>The engine's own body of the target, when the site holds it; null otherwise.</summary>
		public readonly object Body;

		private UWDamageTarget(UWDamageTargetKind peKind, int piIndex, UWObject pOObject, object pOBody)
		{
			Kind = peKind;
			Index = piIndex;
			Object = pOObject;
			Body = pOBody;
		}

		public static UWDamageTarget Creature(int piIndex, object pOBody = null)
		{
			return new UWDamageTarget(UWDamageTargetKind.Creature, piIndex, null, pOBody);
		}

		public static UWDamageTarget ObjectOf(UWObject pOObject, object pOBody = null)
		{
			return new UWDamageTarget(UWDamageTargetKind.Object, 0, pOObject, pOBody);
		}

		public static UWDamageTarget InFlight(int piFlightIndex)
		{
			return new UWDamageTarget(UWDamageTargetKind.ThingInFlight, piFlightIndex, null, null);
		}

		public override string ToString()
		{
			return Kind == UWDamageTargetKind.Object ? "object" + (Object != null ? " " + Object.ID : "") : Kind + " " + Index;
		}
	}

	/// <summary>
	/// What a world offers the landing of a blow (UWBlowRouting): the bodies' own operations, each
	/// doing exactly what the sites did before - nothing decides here. The Unity world implements it
	/// over UWDamageable, UWCritter, UWCharacter and UWMissiles; the self-check's FakeWorld logs it.
	/// </summary>
	public interface IUWBlowBodies
	{
		/// <summary>The creature is there and not destroyed - every creature site asked this before
		/// its blow (or ApplyDamageWithoutAttacker did).</summary>
		bool CreatureTakesBlows(UWDamageTarget pOTarget);

		/// <summary>The hit points of a creature or an object take the damage (UWDamageable.
		/// ApplyDamage: resistances, quality class, a door's spike, the events); a creature takes
		/// the attacker with it (byte 0x12, cleanup step 4.8, 2026-10-18 - a pending attacker set
		/// beforehand until then), an object keeps none.</summary>
		void DamageBody(UWDamageTarget pOTarget, int piDamage, int piDamageType, int piAttacker);

		/// <summary>The player takes the request through UWDamagePipeline (cleanup step 4.2,
		/// 2026-10-17; three methods for plain damage, the curse and the backfire until then).</summary>
		void DamagePlayer(UWDamageRequest pORequest);

		/// <summary>The damage trap's strike on a thing in flight (UWMissiles.StrikeThing).</summary>
		void StrikeThingInFlight(int piFlightIndex, int piDamage, int piDamageType);
	}

	/// <summary>
	/// WHERE A BLOW LANDS, the one routine behind IUWWorld.Damage (cleanup step 3.3, 2026-10-17):
	/// the player, a creature with the blow's attacker, an object, a thing in flight.
	/// Engine-free so the self-check runs it in a fake world.
	/// </summary>
	public static class UWBlowRouting
	{
		public static void Land(IUWBlowBodies pIBodies, UWDamageTarget pOTarget, UWBlow pOBlow)
		{
			if (pIBodies == null)
				return;

			switch (pOTarget.Kind)
			{
				case UWDamageTargetKind.Player:
					pIBodies.DamagePlayer(pOBlow.ForPlayer());

					return;

				case UWDamageTargetKind.Creature:
					// A request strikes the player only.
					if (pOBlow.StrikesPlayerOnly || !pIBodies.CreatureTakesBlows(pOTarget))
						return;

					pIBodies.DamageBody(pOTarget, pOBlow.Amount, pOBlow.DamageType, pOBlow.Attacker);

					return;

				case UWDamageTargetKind.Object:
					if (!pOBlow.StrikesPlayerOnly)
						pIBodies.DamageBody(pOTarget, pOBlow.Amount, pOBlow.DamageType, pOBlow.Attacker);

					return;

				case UWDamageTargetKind.ThingInFlight:
					if (!pOBlow.StrikesPlayerOnly)
						pIBodies.StrikeThingInFlight(pOTarget.Index, pOBlow.Amount, pOBlow.DamageType);

					return;
			}
		}
	}
}
