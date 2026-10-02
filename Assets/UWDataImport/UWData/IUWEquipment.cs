namespace UWDataImport.UWData
{
	/// <summary>
	/// What the rules need to know about the player's equipment: the item in a body slot. The Unity
	/// inventory implements it (P2 of the engine separation, 2026-09-17), so rules such as
	/// UWArmourProtection no longer depend on it.
	/// </summary>
	public interface IUWEquipment
	{
		/// <summary>The item in that slot, or null.</summary>
		UWObject GetEquipped(UWArmorItemMap.BodySlot peSlot);

		/// <summary>
		/// Handedness, from bit 0 of PLAYER.DAT 0x64 (set = right-handed).
		///
		/// THE STATUS PASS NEEDS IT: PlayerStatusUpdate_ovr133_784 reads the weapon from
		/// inventory slot 8 minus that bit and the SHIELD from slot 7 plus it (labels 7EB and
		/// 887), so the two hands are not interchangeable. Without it the shield's armour and
		/// the weapon skill of the defence would come from the wrong hand.
		/// </summary>
		bool IsLeftHanded { get; }
	}
}
