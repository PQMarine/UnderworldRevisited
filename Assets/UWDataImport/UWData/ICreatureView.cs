namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PICTURE OF ONE CREATURE (cleanup step 4.11, 2026-10-18): what its engine-free actor
	/// (UWCreatureActor) asks of the Unity side that shows it - the body that glides to the record's
	/// spot, the animator, the sounds and effects at the body, the hit points' component and the
	/// trace. The one implementation is UWCritter; the self-check has a logging fake.
	///
	/// WHY: until this step UWCritter was the creature's host and its picture in one class of about
	/// 2,700 lines, so none of the creature's decisions around the mind - the blow's outcome, the
	/// doors, the casts, the reactions to spells, the record's moves - could run without a scene
	/// (section 7.4 of Docs/CLEANUP-PLAN.md). The view decides nothing: every member shows or
	/// reports what the actor decided.
	/// </summary>
	public interface ICreatureView
	{
		/// <summary>This creature as the target of a blow, with the body that takes it (its
		/// UWDamageable) - what UWWorld.TargetOf made at every damage site of UWCritter.</summary>
		UWDamageTarget DamageTarget { get; }

		/// <summary>This creature as the engine-free searches and the special deaths see it (its
		/// level object, record, the tile its picture stands on, dead or not, index).</summary>
		UWCreatureRef Ref { get; }

		/// <summary>The death is committed: the body counts as destroyed from now on and raises its
		/// Destroyed once (UWDamageable.MarkDestroyed).</summary>
		void MarkDestroyed();

		/// <summary>The body follows the record after a step: the picture glides from where it is to
		/// the record's spot over the interval that has just elapsed (deviation 17).</summary>
		void PlaceBody(int piInterval);

		/// <summary>The body at the record's spot at once, picture included (a teleport, the sleep
		/// ambush, the night's time pass).</summary>
		void SetBodyNow();

		/// <summary>The picture of one update: the Motion's animation slot and frame (and the walk
		/// that still glides, see UWCritter.ShowMotion).</summary>
		void ShowMotion(UWCritterBrain.Motion pOMotion);

		/// <summary>LoadSoundAtCoordinate at the body with the original's sound number (the brain's
		/// footsteps and the death sound).</summary>
		void PlaySound(int piSound);

		/// <summary>The miss of a blow that met nothing or the world: sound 0x0A at the body.</summary>
		void PlayMissSound();

		/// <summary>The sound of a blow on this creature as the defender: on a hit sound 4 at the
		/// body with the outcome's volume, on a miss the blocked sound of the outcome.</summary>
		void PlayStruckSound(in UWAttackOutcome pOOutcome);

		/// <summary>The blood or the flash of a damaging hit at this creature's part (a missile's
		/// part at the missile's own height piMissileZ, zpos).</summary>
		void ShowHitEffect(int piPart, int piDamage, int piMissileZ);

		/// <summary>A land creature ended its step in deep water: the water sound and the one splash
		/// at the body.</summary>
		void ShowDrowning();

		/// <summary>Frame 3 of the death animation, called once by the actor's death machine: the body
		/// snaps to its logical spot, and the remains, the loot and the removal follow
		/// (UWCritterRemains.CompleteDeath; the body is simply destroyed without one).</summary>
		void CompleteDeath();

		/// <summary>A line of the creature's trace (the format filled only while tracing is on).</summary>
		void Trace(string psFormat, params object[] pOArguments);

		/// <summary>An event of the creature: the last one shows in the F1 overlay, all of them in
		/// the trace.</summary>
		void Event(string psFormat, params object[] pOArguments);
	}
}
