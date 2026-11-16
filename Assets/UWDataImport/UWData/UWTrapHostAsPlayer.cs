namespace UWDataImport.UWData
{
	/// <summary>
	/// A firing's host with the PLAYER as the one who set it off, whoever moved (G3 part H4): check_pplate sets a plate off
	/// with UseTrigger(ThePlayer, ...) whether the player, a creature or a thing pressed it. Everything else is the host's.
	/// </summary>
	public sealed class UWTrapHostAsPlayer : IUWTrapHost
	{
		private readonly IUWTrapHost mIInner;

		private readonly UWTrapFiring mOFiring;

		public UWTrapHostAsPlayer(IUWTrapHost pIInner)
			: this(pIInner, UWTrapFiring.ByPlayer)
		{
		}

		/// <summary>With another setter: nobody (a thing's firing, pbByThing) for the hacks that act as by no one -
		/// switch_flip_hack's checkTrap(0L, ...).</summary>
		public UWTrapHostAsPlayer(IUWTrapHost pIInner, UWTrapFiring pOFiring)
		{
			mIInner = pIInner;
			mOFiring = pOFiring;
		}

		/// <summary>The square of the plate whose firing this is (check_pplate's TriggerChainTileData), which the q*bert
		/// hack reads; null for other firings.</summary>
		public UWTile PlateTile { get; set; }

		public IUWWorld World => mIInner.World;

		public UWTrapFiring Firing => mOFiring;

		public bool AlwaysFireCreateObjectTraps => mIInner.AlwaysFireCreateObjectTraps;

		public void AdvanceLever(UWObject pOSwitchObject) => mIInner.AdvanceLever(pOSwitchObject);

		public void ToggleSwitchVisual(UWObject pOSwitchObject) => mIInner.ToggleSwitchVisual(pOSwitchObject);

		public void RebuildGeometry() => mIInner.RebuildGeometry();

		public void OnObjectHeightChanged(UWObject pOObject, int piOldZPos) => mIInner.OnObjectHeightChanged(pOObject, piOldZPos);

		public void MoveCreaturesWithFloor(int piTileX, int piTileY, int piOldZPos, int piNewZPos)
			=> mIInner.MoveCreaturesWithFloor(piTileX, piTileY, piOldZPos, piNewZPos);

		public void SpawnPendingObjectsInTile(int piTileX, int piTileY) => mIInner.SpawnPendingObjectsInTile(piTileX, piTileY);

		// The player is struck as the one who set it off: the creature or thing that pressed the plate is not.
		public void StrikeTriggeringThing(int piDamage)
		{
		}

		public void DamageTriggeringCreature(int piDamage)
		{
		}

		public void ApplyWardDamage(int piDamage, int piWardMessage, int piFirstDirectionMessage)
		{
		}

		public bool FireArrowTrap(UWObject pOTrap, int piTileX, int piTileY) => mIInner.FireArrowTrap(pOTrap, piTileX, piTileY);

		public bool FireSpellTrap(UWObject pOTrap, int piTileX, int piTileY, int piMajorClass, int piMinorClass)
			=> mIInner.FireSpellTrap(pOTrap, piTileX, piTileY, piMajorClass, piMinorClass);

		public bool FireCameraTrap(UWObject pOTrap, int piTileX, int piTileY) => mIInner.FireCameraTrap(pOTrap, piTileX, piTileY);

		public int AngerRaceNearPlayer(int piOwner, int piTilesBack, int piTilesForward)
			=> mIInner.AngerRaceNearPlayer(piOwner, piTilesBack, piTilesForward);

		public bool BeginConversation(int piSlot) => mIInner.BeginConversation(piSlot);

		public bool TalkTo(UWObject pONpcData) => mIInner.TalkTo(pONpcData);

		public bool PlayVictory() => mIInner.PlayVictory();
	}
}
