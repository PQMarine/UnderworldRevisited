namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIRST GAME'S MAGIC STEPS (phase 0 step 2, 2026-10-28): what UWSpellCasting.CastRunes did
	/// at these steps until then, moved here unchanged (player_cast and release_missile of UW.EXE).
	/// </summary>
	public sealed class UWMagicVariantUw1 : IUWMagicVariant
	{
		/// <summary>The first game lets a paralysed player cast.</summary>
		public bool MayCast(IUWSpellHost pIHost)
		{
			return true;
		}

		/// <summary>The first game knows no circle limit by place; level 9's no-magic is the world's.</summary>
		public bool MayCastHere(IUWSpellHost pIHost, int piCircle)
		{
			return true;
		}

		/// <summary>A projectile pays when it is released (release_missile keeps the cost and
		/// subtracts it only once the missile found room; the rule's home is UWSpellCasting).</summary>
		public bool PaysOnRelease(UWRunicMagic.Spell pOSpell)
		{
			return pOSpell.MajorClass == UWSpellCasting.ProjectileMajorClass;
		}

		/// <summary>Too soon: the sound alone.</summary>
		public void TooSoon(IUWSpellHost pIHost)
		{
			pIHost.World.Presentation.PlaySoundAtPlayer(UWSpellCasting.TooSoonSound);
		}
	}
}
