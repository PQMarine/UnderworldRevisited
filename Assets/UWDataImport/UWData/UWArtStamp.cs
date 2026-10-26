using System;
using System.Collections.Generic;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// WHAT A BUILT PICTURE DEPENDS ON (cleanup step 5.4, 2026-10-19): the version of the colours
	/// it was made with (the colour help's, or the colour help's plus the modern backs' - see
	/// UWModernBacks.ArtVersion) and up to three numbers of its own (size, variant, a switch). Two
	/// stamps are equal when every part is; a picture whose stamp differs from the one asked for
	/// is stale and gets built anew.
	///
	/// It replaced the hand-kept version fields beside their size and switch fields of the modern
	/// scheme and three keys built as strings like "width/version" every frame (CLEANUP-PLAN 8.1):
	/// one value, compared in one place.
	/// </summary>
	public readonly struct UWArtStamp : IEquatable<UWArtStamp>
	{
		public readonly int Colours;

		public readonly int A;

		public readonly int B;

		public readonly int C;

		public UWArtStamp(int piColours, int piA = 0, int piB = 0, int piC = 0)
		{
			Colours = piColours;
			A = piA;
			B = piB;
			C = piC;
		}

		public bool Equals(UWArtStamp pOOther)
		{
			return Colours == pOOther.Colours && A == pOOther.A && B == pOOther.B && C == pOOther.C;
		}

		public override bool Equals(object pOOther)
		{
			return pOOther is UWArtStamp lOStamp && Equals(lOStamp);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (((((Colours * 397) ^ A) * 397) ^ B) * 397) ^ C;
			}
		}

		public static bool operator ==(UWArtStamp pOLeft, UWArtStamp pORight)
		{
			return pOLeft.Equals(pORight);
		}

		public static bool operator !=(UWArtStamp pOLeft, UWArtStamp pORight)
		{
			return !pOLeft.Equals(pORight);
		}

		public override string ToString()
		{
			return Colours + "/" + A + "/" + B + "/" + C;
		}
	}

	/// <summary>
	/// ONE BUILT PICTURE AND ITS STAMP (cleanup step 5.4, 2026-10-19): Set keeps a new picture and
	/// releases the one it replaces, so no caller destroys the old one by hand any more. The engine
	/// side says how a picture is released and whether one still exists (Unity's destroyed
	/// textures compare equal to null only through Unity's own operator) - UWVersionedTexture.
	/// </summary>
	public sealed class UWStampedSlot<TArt> where TArt : class
	{
		private readonly Action<TArt> mORelease;

		private readonly Func<TArt, bool> mOIsAlive;

		private bool mbHasStamp;

		public UWStampedSlot(Action<TArt> pORelease, Func<TArt, bool> pOIsAlive = null)
		{
			mORelease = pORelease;
			mOIsAlive = pOIsAlive;
		}

		public TArt Art { get; private set; }

		public UWArtStamp Stamp { get; private set; }

		/// <summary>The picture kept was built for exactly this stamp and still exists.</summary>
		public bool IsCurrent(UWArtStamp pOStamp)
		{
			return mbHasStamp && Stamp == pOStamp && fIsAlive(Art);
		}

		/// <summary>Keeps a picture built for a stamp; the one it replaces is released.</summary>
		public TArt Set(UWArtStamp pOStamp, TArt pOArt)
		{
			if (Art != null && !ReferenceEquals(Art, pOArt))
				mORelease?.Invoke(Art);

			Art = pOArt;
			Stamp = pOStamp;
			mbHasStamp = true;

			return pOArt;
		}

		/// <summary>Releases the picture; the next IsCurrent is false.</summary>
		public void Release()
		{
			if (Art != null)
				mORelease?.Invoke(Art);

			Art = null;
			mbHasStamp = false;
		}

		private bool fIsAlive(TArt pOArt)
		{
			return pOArt != null && (mOIsAlive == null || mOIsAlive(pOArt));
		}
	}

	/// <summary>
	/// BUILT PICTURES BY KEY, EACH WITH ITS STAMP (cleanup step 5.4, 2026-10-19): the modern
	/// parts that keep several pictures of one kind (leathers by size, strips by width, compass
	/// steps, papers) - TryGet answers only for the stamp asked for; Set releases what it
	/// replaces; ReleaseAll empties it.
	/// </summary>
	public sealed class UWStampedCache<TKey, TArt> where TArt : class
	{
		private readonly Dictionary<TKey, UWStampedSlot<TArt>> mOSlots = new Dictionary<TKey, UWStampedSlot<TArt>>();

		private readonly Action<TArt> mORelease;

		private readonly Func<TArt, bool> mOIsAlive;

		public UWStampedCache(Action<TArt> pORelease, Func<TArt, bool> pOIsAlive = null)
		{
			mORelease = pORelease;
			mOIsAlive = pOIsAlive;
		}

		public int Count => mOSlots.Count;

		public bool TryGet(TKey pOKey, UWArtStamp pOStamp, out TArt pOArt)
		{
			if (mOSlots.TryGetValue(pOKey, out UWStampedSlot<TArt> lOSlot) && lOSlot.IsCurrent(pOStamp))
			{
				pOArt = lOSlot.Art;

				return true;
			}

			pOArt = null;

			return false;
		}

		public TArt Set(TKey pOKey, UWArtStamp pOStamp, TArt pOArt)
		{
			if (!mOSlots.TryGetValue(pOKey, out UWStampedSlot<TArt> lOSlot))
			{
				lOSlot = new UWStampedSlot<TArt>(mORelease, mOIsAlive);
				mOSlots[pOKey] = lOSlot;
			}

			return lOSlot.Set(pOStamp, pOArt);
		}

		/// <summary>The picture for a key and stamp, built when there is none (or a stale one).</summary>
		public TArt Get(TKey pOKey, UWArtStamp pOStamp, Func<TArt> pOBuild)
		{
			return TryGet(pOKey, pOStamp, out TArt lOArt) ? lOArt : Set(pOKey, pOStamp, pOBuild());
		}

		public void ReleaseAll()
		{
			foreach (UWStampedSlot<TArt> lOSlot in mOSlots.Values)
				lOSlot.Release();

			mOSlots.Clear();
		}
	}
}
