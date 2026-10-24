namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE THE PAPER DOLL'S PIECES LIE on the inventory page, in one engine-free table (cleanup
	/// 5.1, 2026-10-19). Until then the armour places were written four times - the classic frame
	/// (UWGameUI), the modern character panel (UWModernPanel), the modern original page
	/// (UWModernInventoryPage) and, for the body, UWHudArt.PaperdollBodyX/Y - and the six icon slots
	/// three times (UWHudInventory.BuildEquipmentSlots, UWModernPanel, UWModernInventoryPage).
	///
	/// Coordinates are MAIN.BYT pixels of the 320 x 200 screen, rows from the top, the top-left
	/// corner of the piece. UWHudArt's PaperdollBodyX/Y read it through UWClassicLayout.Paperdoll
	/// (cleanup 5.3, 2026-10-19); the self-check holds the two equal.
	/// </summary>
	public static class UWPaperdollLayout
	{
		// ------------------------------------------------- The body and the armour pictures

		/// <summary>The body picture (BODIES.BYT) - the armour pictures are drawn over it.</summary>
		public const int BodyX = 260;

		public const int BodyY = 11;

		public const int HelmetX = 267;

		/// <summary>One pixel higher than the 11 it had since the baseline: the headgear sat one
		/// pixel too low against the original (per user, 2026-09-23, measured on an enlarged
		/// screenshot).</summary>
		public const int HelmetY = 10;

		public const int GlovesX = 261;

		public const int GlovesY = 42;

		public const int ChestX = 262;

		public const int ChestY = 22;

		/// <summary>The leggings' place; a man's plate leggings sit lower still
		/// (UWPaperdollRules.GetSlotLook, Drop).</summary>
		public const int LegsX = 268;

		public const int LegsY = 24;

		public const int BootsX = 266;

		public const int BootsY = 66;

		// ------------------------------------------------- The six slots with an item icon

		/// <summary>
		/// A paper doll slot that shows the worn thing's own icon (rings and hands): its top-left
		/// corner and its size. Deconstructs to (Slot, X, Y, Size), as the tuples it replaces.
		/// </summary>
		public readonly struct IconSlot
		{
			public readonly UWArmorItemMap.BodySlot Slot;

			public readonly float X;

			public readonly float Y;

			public readonly float Size;

			public IconSlot(UWArmorItemMap.BodySlot peSlot, float pfX, float pfY, float pfSize)
			{
				Slot = peSlot;
				X = pfX;
				Y = pfY;
				Size = pfSize;
			}

			public void Deconstruct(out UWArmorItemMap.BodySlot peSlot, out float pfX, out float pfY, out float pfSize)
			{
				peSlot = Slot;
				pfX = X;
				pfY = Y;
				pfSize = Size;
			}
		}

		/// <summary>
		/// The ring and hand slots, in the order of UWHudInventory.ArmorHitZoneSlots after its five
		/// armour slots. Positions and sizes set per user in the prefab editor and taken over -
		/// rings much smaller (4x4) than the four hand slots (16x16), since ring-shaped items are
		/// visually much smaller. (RightHandSlot is drawn on the left, see
		/// UWHudInventory.BuildEquipmentSlots.)
		/// </summary>
		public static readonly IconSlot[] IconSlots =
		{
			new IconSlot(UWArmorItemMap.BodySlot.LeftRing, 291.03f, 56.9f, 4f),
			new IconSlot(UWArmorItemMap.BodySlot.RightRing, 260f, 56.9f, 4f),
			new IconSlot(UWArmorItemMap.BodySlot.RightHandSlot, 242f, 34.82566f, 16f),
			new IconSlot(UWArmorItemMap.BodySlot.LeftHandSlot, 296f, 34.826f, 16f),
			new IconSlot(UWArmorItemMap.BodySlot.LeftOffHandSlot, 294f, 12.792999f, 16f),
			new IconSlot(UWArmorItemMap.BodySlot.RightOffHandSlot, 244.9f, 12.793f, 16f),
		};

		/// <summary>Ring icons are themselves 16x16 despite the tiny 4x4 hit zone: at their
		/// natural size from OBJECTS.GR, squashed onto the hit zone per stretch fill, the ring icon
		/// did not sit right (CONFIRMED per user test). Centred instead of stretched.</summary>
		public const float RingIconSize = 16f;

		/// <summary>The ring icon's small Y offset (up) - taken over 1:1 from the result the user
		/// found in the prefab editor.</summary>
		public const float RingIconYOffset = 0.8f;
	}
}
