using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ANIMATION LIST OF A LEVEL as the original keeps it in LEV.ARK, block 8 + level
	/// (the "animation overlay" of uw-formats.txt): up to 64 entries of six bytes - the object's
	/// number in bits 6-15 of the first word, the frames left as a signed word (-1 for ever),
	/// then the tile. The original (EFFECT.C's animlist) animates ONLY the objects in this list:
	/// add_animobj puts a freshly spawned class-7 object in (a trap's creation, a spell's picture,
	/// a blow's blood, the planted silver tree), toast_animobj takes an expired one out, and
	/// Anim_Load / Anim_Save read and write the block with the level. The count is not stored;
	/// the list ends at the first entry with object number 0, and the bytes behind it are
	/// whatever was there before (Anim_Save clears only the number of the entry after the last).
	///
	/// Found 2026-10-11: a silver tree planted here and saved stood still in the original, one
	/// planted there and saved animated here and there - our save copied the block unchanged.
	/// Since 2026-10-17 the renderer honours the list too (GetFate): a blood splash saved within a
	/// second of a blow animated for ever here, while the original played it out once and took it
	/// away (per user, SAVE4 L1 46/55).
	/// </summary>
	public sealed class UWAnimationOverlay
	{
		public const int BlockLength = 384;

		public const int MaxEntries = 64;

		public const int EntryLength = 6;

		/// <summary>The length that never runs out.</summary>
		public const int Forever = -1;

		/// <summary>animclassd flag 0x20 (ANIMF_REMOVE): toast_animobj takes the object off the
		/// map when its length runs out; without it the object stays, no longer animated.</summary>
		public const int RemoveAtEndFlag = 0x20;

		/// <summary>What becomes of a class-7 object of the level as loaded.</summary>
		public enum Fate
		{
			/// <summary>Listed with length -1, or not the level's own object: animates for ever.</summary>
			Forever,

			/// <summary>Listed with frames left: plays them, then goes (RemoveAtEndFlag).</summary>
			ExpiresAndGoes,

			/// <summary>Listed with frames left, a class without RemoveAtEndFlag: plays them, then
			/// stands still.</summary>
			ExpiresAndStops,

			/// <summary>Not listed: update_animobj never steps it, so it shows its owner field's
			/// picture and nothing more.</summary>
			StandsStill
		}

		/// <summary>
		/// The fate of a class-7 object (EFFECT.C update_animobj, toast_animobj): only listed objects
		/// animate, a finite length runs out. pbFromLevelData false - a view-only effect picture
		/// with its own clock (UWLevelLoader.SpawnEffectAt) - and an unloaded list leave it
		/// animating as before. piClassFlags is the class's animclassd flags byte.
		/// </summary>
		public Fate GetFate(UWObject pOObject, bool pbFromLevelData, int piClassFlags, out int piLengthTicks)
		{
			piLengthTicks = Forever;

			if (!IsLoaded || !pbFromLevelData || pOObject == null)
				return Fate.Forever;

			Entry lOEntry = Find(pOObject);

			if (lOEntry == null)
				return Fate.StandsStill;

			if (lOEntry.Length < 0)
				return Fate.Forever;

			piLengthTicks = lOEntry.Length;

			return (piClassFlags & RemoveAtEndFlag) != 0 ? Fate.ExpiresAndGoes : Fate.ExpiresAndStops;
		}

		public sealed class Entry
		{
			/// <summary>The object, when the number could be resolved; null for a number that
			/// points at no object of the master list (kept as read).</summary>
			public UWObject Object;

			/// <summary>The object's number as read from the file.</summary>
			public int ObjectIndex;

			/// <summary>Frames left, or Forever.</summary>
			public int Length;

			public int TileX;

			public int TileY;
		}

		public readonly List<Entry> Entries = new List<Entry>();

		/// <summary>The block as read; the bytes behind the last entry are written back as they were.</summary>
		private readonly byte[] myRaw = new byte[BlockLength];

		public bool IsLoaded { get; private set; }

		/// <summary>Reads the block at the offset; a short block gives an empty, unloaded list.</summary>
		public static UWAnimationOverlay Load(byte[] pyData, int piOffset, List<UWObject> pOMasterlist)
		{
			UWAnimationOverlay lOOverlay = new UWAnimationOverlay();

			if (pyData == null || piOffset <= 0 || piOffset + BlockLength > pyData.Length)
				return lOOverlay;

			Array.Copy(pyData, piOffset, lOOverlay.myRaw, 0, BlockLength);
			lOOverlay.IsLoaded = true;

			for (int liAt = 0; liAt < MaxEntries; liAt++)
			{
				int liBase = liAt * EntryLength;
				int liIndex = UWBinary.ReadU16(lOOverlay.myRaw, liBase) >> 6;

				if (liIndex == 0)
					break;

				lOOverlay.Entries.Add(new Entry
				{
					Object = pOMasterlist != null && liIndex < pOMasterlist.Count ? pOMasterlist[liIndex] : null,
					ObjectIndex = liIndex,
					Length = (short)UWBinary.ReadU16(lOOverlay.myRaw, liBase + 2),
					TileX = lOOverlay.myRaw[liBase + 4],
					TileY = lOOverlay.myRaw[liBase + 5]
				});
			}

			return lOOverlay;
		}

		/// <summary>Starts an animation on an object placed in a tile, as add_animobj does; an
		/// object already listed keeps one entry. A full list takes nothing (the original gives
		/// -1 then and the caller leaves the object unanimated).</summary>
		public bool Add(UWObject pOObject, int piLength, int piTileX, int piTileY)
		{
			if (pOObject == null)
				return false;

			Remove(pOObject);

			if (Entries.Count >= MaxEntries)
				return false;

			Entries.Add(new Entry { Object = pOObject, Length = piLength, TileX = piTileX, TileY = piTileY });

			return true;
		}

		public bool Remove(UWObject pOObject)
		{
			return pOObject != null && Entries.RemoveAll(pOEntry => pOEntry.Object == pOObject) > 0;
		}

		/// <summary>The entry of an object, or null.</summary>
		public Entry Find(UWObject pOObject)
		{
			return pOObject != null ? Entries.Find(pOEntry => pOEntry.Object == pOObject) : null;
		}

		/// <summary>
		/// The block as Anim_Save writes it: the entries in order with the numbers the level
		/// writer gave their objects (pFIndexOf; an entry whose object has no number any more is
		/// dropped, one without an object keeps the number it was read with), the number of the
		/// entry after the last cleared, everything else as it was read.
		/// </summary>
		public byte[] Serialize(Func<UWObject, int> pFIndexOf)
		{
			byte[] lyOut = (byte[])myRaw.Clone();
			int liAt = 0;

			foreach (Entry lOEntry in Entries)
			{
				if (liAt >= MaxEntries)
					break;

				int liIndex = lOEntry.Object != null
					? (pFIndexOf != null ? pFIndexOf(lOEntry.Object) : -1)
					: lOEntry.ObjectIndex;

				if (liIndex <= 0 || liIndex >= 1024)
					continue;

				int liBase = liAt * EntryLength;

				UWBinary.Write16(lyOut, liBase, (liIndex << 6) | (lyOut[liBase] & 0x3F));
				UWBinary.Write16(lyOut, liBase + 2, lOEntry.Length & 0xFFFF);
				lyOut[liBase + 4] = (byte)lOEntry.TileX;
				lyOut[liBase + 5] = (byte)lOEntry.TileY;
				liAt++;
			}

			if (liAt < MaxEntries)
			{
				int liBase = liAt * EntryLength;

				UWBinary.Write16(lyOut, liBase, lyOut[liBase] & 0x3F);
			}

			return lyOut;
		}
	}
}
