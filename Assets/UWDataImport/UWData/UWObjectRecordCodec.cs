namespace UWDataImport.UWData
{
	/// <summary>
	/// THE EIGHT-BYTE OBJECT RECORD, read and written in one place (cleanup phase 2, step
	/// "Codecs", 2026-10-12). Until then UWLevel.load_object_list and UWPlayerData.fParseRecord
	/// each parsed the four words, UWLevelWriter and UWPlayerData.fWriteInventory each built
	/// them, and the writers wrote THREE flag bits plus a separate enchanted bool into bit 12
	/// while the readers read four (B13). The layout is the original's struct Object
	/// (object.h), whose macros are cited at the fields:
	///
	///   word 0  bits 0-8 item id, 9-12 flags, 13 doordir, 14 invisible, 15 is_quant
	///   word 1  bits 0-6 zpos, 7-9 heading, 10-12 ypos, 13-15 xpos
	///   word 2  bits 0-5 quality, 6-15 next (the chain link)
	///   word 3  bits 0-5 owner, 6-15 quantity or special link
	///
	/// THE FLAGS ARE FOUR BITS (ID_FLAGS 0x1E00; OBJ_FLAGS reads four, SET_FLAGS writes four)
	/// and the enchanted bit is the top one of them (ID_ENCHANT 0x1000, bit 3 of the field).
	/// UWObject.Flags carries the four bits and IsEnchanted is a view on bit 3 (decision 4 of
	/// the plan). For consistent data the bytes do not change; what changes is a writer that
	/// used to re-add a stale bit 12 after a four-bit value had been written over it - the
	/// chain-removal trap counts down through SET_FLAGS (trigger_obj_del, TRIGGER.C), and a
	/// count that crossed 8 came back as 15 after a save.
	///
	/// The same record is the first eight bytes of a mobile object (UWNpcRecordCodec holds the
	/// nineteen after them) and of an inventory record in PLAYER.DAT.
	/// </summary>
	public static class UWObjectRecordCodec
	{
		public const int RecordSize = 8;

		// ------------------------------------------------- Word 0: id and flags

		/// <summary>ID_ITEM: bits 0-8.</summary>
		public const int IdMask = 0x1FF;

		/// <summary>ID_FLAGS 0x1E00: bits 9-12, four bits; FlagsMask applies to the field.</summary>
		public const int FlagsShift = 9;

		public const int FlagsMask = 0xF;

		/// <summary>ID_ENCHANT 0x1000, bit 12 of the word - bit 3 of the four-bit field.</summary>
		public const int EnchantedFlag = 0x8;

		/// <summary>ID_DOORDIR: bit 13.</summary>
		public const int DoorDirectionBit = 0x2000;

		/// <summary>ID_INVIS: bit 14, not drawn (the port's IsHidden).</summary>
		public const int InvisibleBit = 0x4000;

		/// <summary>ID_ISQUANT: bit 15, the last field is a quantity or a special property, not
		/// an object link (the port's HasQuantity).</summary>
		public const int IsQuantityBit = 0x8000;

		// ------------------------------------------------- Word 1: position

		/// <summary>POS_Z: bits 0-6.</summary>
		public const int ZPosMask = 0x7F;

		/// <summary>POS_HEADING 0x380: bits 7-9, eighths of a turn.</summary>
		public const int HeadingShift = 7;

		public const int HeadingMask = 0x7;

		/// <summary>POS_YFINE and POS_XFINE: bits 10-12 and 13-15, eighths of a tile.</summary>
		public const int YPosShift = 10;

		public const int XPosShift = 13;

		public const int FinePositionMask = 0x7;

		// ------------------------------------------------- Word 2: quality and chain link

		public const int QualityMask = 0x3F;

		/// <summary>The chain link ("next"): bits 6-15, an object number.</summary>
		public const int LinkShift = 6;

		public const int LinkMask = 0x3FF;

		// ------------------------------------------------- Word 3: owner and quantity

		public const int OwnerMask = 0x3F;

		/// <summary>Bits 6-15: a quantity (with IsQuantityBit and a value below SpecialLinkBit),
		/// a special property (with IsQuantityBit and SpecialLinkBit set) or the first object
		/// of the contents (without IsQuantityBit).</summary>
		public const int QuantityShift = 6;

		public const int QuantityMask = 0x3FF;

		/// <summary>LINK_SPECIAL 0x200 within the quantity field.</summary>
		public const int SpecialLinkBit = 0x200;

		// ------------------------------------------------- Reading

		/// <summary>The item id, for choosing the object's class before Read fills it.</summary>
		public static int ReadId(byte[] pyData, int piAt)
		{
			return UWBinary.Read16(pyData, piAt) & IdMask;
		}

		/// <summary>Fills every field of the record except the id (the constructor's).</summary>
		public static void Read(byte[] pyData, int piAt, UWObject pOObject)
		{
			int liWord0 = UWBinary.Read16(pyData, piAt);
			int liWord1 = UWBinary.Read16(pyData, piAt + 2);
			int liWord2 = UWBinary.Read16(pyData, piAt + 4);
			int liWord3 = UWBinary.Read16(pyData, piAt + 6);

			pOObject.Flags = (ushort)((liWord0 >> FlagsShift) & FlagsMask);
			pOObject.DoorDirection = (liWord0 & DoorDirectionBit) != 0;
			pOObject.IsHidden = (liWord0 & InvisibleBit) != 0;
			pOObject.HasQuantity = (liWord0 & IsQuantityBit) != 0;
			pOObject.ZPos = liWord1 & ZPosMask;
			pOObject.Heading = (ushort)((liWord1 >> HeadingShift) & HeadingMask);
			pOObject.YPos = (ushort)((liWord1 >> YPosShift) & FinePositionMask);
			pOObject.XPos = (ushort)((liWord1 >> XPosShift) & FinePositionMask);
			pOObject.Quality = (ushort)(liWord2 & QualityMask);
			pOObject.Link = (ushort)((liWord2 >> LinkShift) & LinkMask);
			pOObject.Owner = (ushort)(liWord3 & OwnerMask);
			pOObject.Quantity = (ushort)((liWord3 >> QuantityShift) & QuantityMask);
		}

		/// <summary>Word 2's link, for walking the chains in the bytes themselves.</summary>
		public static int ReadLink(byte[] pyData, int piAt)
		{
			return (UWBinary.Read16(pyData, piAt + 4) >> LinkShift) & LinkMask;
		}

		/// <summary>Word 3's quantity or special link.</summary>
		public static int ReadQuantityOrLink(byte[] pyData, int piAt)
		{
			return (UWBinary.Read16(pyData, piAt + 6) >> QuantityShift) & QuantityMask;
		}

		public static bool ReadIsQuantity(byte[] pyData, int piAt)
		{
			return (UWBinary.Read16(pyData, piAt) & IsQuantityBit) != 0;
		}

		// ------------------------------------------------- Writing

		/// <summary>The record as the object holds it: its own id, link and quantity.</summary>
		public static void Write(byte[] pyData, int piAt, UWObject pOObject)
		{
			Write(pyData, piAt, pOObject, pOObject.ID, pOObject.Link, pOObject.Quantity);
		}

		/// <summary>
		/// The record with the three fields a writer decides itself: the id (an open sack at
		/// the top of the inventory is written closed), the chain link (recomputed from the
		/// order of the lists) and the last field (a quantity, or the new number of the first
		/// contained object).
		/// </summary>
		public static void Write(byte[] pyData, int piAt, UWObject pOObject, int piId, int piLink, int piQuantityOrLink)
		{
			int liWord0 = (piId & IdMask)
				| ((pOObject.Flags & FlagsMask) << FlagsShift)
				| (pOObject.DoorDirection ? DoorDirectionBit : 0)
				| (pOObject.IsHidden ? InvisibleBit : 0)
				| (pOObject.HasQuantity ? IsQuantityBit : 0);

			int liWord1 = (pOObject.ZPos & ZPosMask)
				| ((pOObject.Heading & HeadingMask) << HeadingShift)
				| ((pOObject.YPos & FinePositionMask) << YPosShift)
				| ((pOObject.XPos & FinePositionMask) << XPosShift);

			int liWord2 = (pOObject.Quality & QualityMask) | ((piLink & LinkMask) << LinkShift);

			int liWord3 = (pOObject.Owner & OwnerMask) | ((piQuantityOrLink & QuantityMask) << QuantityShift);

			UWBinary.Write16(pyData, piAt, liWord0);
			UWBinary.Write16(pyData, piAt + 2, liWord1);
			UWBinary.Write16(pyData, piAt + 4, liWord2);
			UWBinary.Write16(pyData, piAt + 6, liWord3);
		}

		/// <summary>A released slot loses its item id and keeps the rest - that is how the
		/// original frees (Obj_Free leaves the record, the next CreateObj fills it; the
		/// reference's ReleaseFreeObject sets item_id to zero).</summary>
		public static void ClearId(byte[] pyData, int piAt)
		{
			UWBinary.Write16(pyData, piAt, UWBinary.Read16(pyData, piAt) & ~IdMask);
		}
	}
}
