using System;
using System.Globalization;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MODERN HUD'S LAYOUT RULES, engine-free (moved out of UWModernLayout 2026-10-05, per
	/// user: reusable for UW2 or another engine). The parts of a HUD, by number, each either left
	/// at its default place or moved, and each with its own size in percent:
	///
	///   - a moved part is kept as an ANCHOR - the nearest corner, edge or the centre of the
	///     screen, per axis by thirds - and its distance from it in original pixels at the UI
	///     size, so the layout keeps its sense at another resolution or aspect;
	///   - Place puts it back there, kept on the screen and on whole pixels;
	///   - the whole layout is one line of text ("element:anchorX:anchorY:offsetX:offsetY:percent;"
	///     per part that was changed, an anchor X below 0 for a part only sized).
	///
	/// Screen pixels count from the bottom left. The engine side keeps the instance, the screen's
	/// size and the UI size, and where the parts were drawn (UWModernLayout).
	/// </summary>
	public sealed class UWHudLayout
	{
		public const int MinPercent = 50;

		public const int MaxPercent = 200;

		public sealed class Placement
		{
			public bool Placed;

			public UWVector2 Anchor;

			/// <summary>From the screen's anchor point to the part's own, original pixels at the UI size.</summary>
			public UWVector2 Offset;

			public int Percent = 100;
		}

		private readonly Placement[] mOPlacements;

		public UWHudLayout(int piCount)
		{
			mOPlacements = new Placement[piCount];

			for (int liAt = 0; liAt < piCount; liAt++)
				mOPlacements[liAt] = new Placement();
		}

		public int Count => mOPlacements.Length;

		public Placement Get(int piElement)
		{
			return mOPlacements[piElement];
		}

		/// <summary>Where the part goes: its default rect (screen pixels from the bottom left,
		/// already at its own size) unless it was moved; kept on the screen.</summary>
		public UWRect Place(int piElement, UWRect pODefault, float pfScreenWidth, float pfScreenHeight, float pfPixelScale)
		{
			Placement lOPlacement = mOPlacements[piElement];

			if (!lOPlacement.Placed)
				return pODefault;

			float lfAnchorX = (lOPlacement.Anchor.X * pfScreenWidth) + (lOPlacement.Offset.X * pfPixelScale);
			float lfAnchorY = (lOPlacement.Anchor.Y * pfScreenHeight) + (lOPlacement.Offset.Y * pfPixelScale);
			float lfX = lfAnchorX - (lOPlacement.Anchor.X * pODefault.Width);
			float lfY = lfAnchorY - (lOPlacement.Anchor.Y * pODefault.Height);

			lfX = fClamp(lfX, 0f, Math.Max(0f, pfScreenWidth - pODefault.Width));
			lfY = fClamp(lfY, 0f, Math.Max(0f, pfScreenHeight - pODefault.Height));

			return new UWRect((float)Math.Round(lfX), (float)Math.Round(lfY), pODefault.Width, pODefault.Height);
		}

		/// <summary>The part moved to this rect: its anchor by thirds, its distance from it.</summary>
		public void Move(int piElement, UWRect pORect, float pfScreenWidth, float pfScreenHeight, float pfPixelScale)
		{
			Placement lOPlacement = mOPlacements[piElement];
			float lfAnchorX = fThird(pORect.CenterX, pfScreenWidth);
			float lfAnchorY = fThird(pORect.CenterY, pfScreenHeight);
			float lfOwnX = pORect.X + (lfAnchorX * pORect.Width);
			float lfOwnY = pORect.Y + (lfAnchorY * pORect.Height);
			float lfScale = Math.Max(0.01f, pfPixelScale);

			lOPlacement.Placed = true;
			lOPlacement.Anchor = new UWVector2(lfAnchorX, lfAnchorY);
			lOPlacement.Offset = new UWVector2((lfOwnX - (lfAnchorX * pfScreenWidth)) / lfScale, (lfOwnY - (lfAnchorY * pfScreenHeight)) / lfScale);
		}

		/// <summary>A new size: the part stays at its anchor point (where it was drawn when it had
		/// none yet) and grows away from it.</summary>
		public void SetPercent(int piElement, int piPercent, UWRect pOCurrent, float pfScreenWidth, float pfScreenHeight, float pfPixelScale)
		{
			Placement lOPlacement = mOPlacements[piElement];

			if (!lOPlacement.Placed)
				Move(piElement, pOCurrent, pfScreenWidth, pfScreenHeight, pfPixelScale);

			lOPlacement.Percent = Math.Min(Math.Max(piPercent, MinPercent), MaxPercent);
		}

		public void Reset(int piElement)
		{
			mOPlacements[piElement] = new Placement();
		}

		public void ResetAll()
		{
			for (int liAt = 0; liAt < mOPlacements.Length; liAt++)
				mOPlacements[liAt] = new Placement();
		}

		private static float fThird(float pfAt, float pfLength)
		{
			if (pfAt < pfLength / 3f)
				return 0f;

			return pfAt > pfLength * 2f / 3f ? 1f : 0.5f;
		}

		private static float fClamp(float pfValue, float pfMin, float pfMax)
		{
			if (pfValue < pfMin)
				return pfMin;

			return pfValue > pfMax ? pfMax : pfValue;
		}

		// ------------------------------------------------- As text

		/// <summary>A layout of piCount parts from its text; what does not parse is left at the
		/// default.</summary>
		public static UWHudLayout Parse(string psText, int piCount)
		{
			UWHudLayout lOLayout = new UWHudLayout(piCount);

			foreach (string lsEntry in (psText ?? string.Empty).Split(';'))
			{
				string[] lsParts = lsEntry.Split(':');

				if (lsParts.Length != 6 || !int.TryParse(lsParts[0], out int liElement) || liElement < 0 || liElement >= piCount)
					continue;

				float[] lfValues = new float[4];
				bool lbValid = true;

				for (int liAt = 0; liAt < 4; liAt++)
					lbValid &= float.TryParse(lsParts[liAt + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out lfValues[liAt]);

				if (!lbValid || !int.TryParse(lsParts[5], out int liPercent))
					continue;

				Placement lOPlacement = lOLayout.mOPlacements[liElement];

				lOPlacement.Placed = lfValues[0] >= 0f;
				lOPlacement.Anchor = new UWVector2(fClamp(lfValues[0], 0f, 1f), fClamp(lfValues[1], 0f, 1f));
				lOPlacement.Offset = new UWVector2(lfValues[2], lfValues[3]);
				lOPlacement.Percent = Math.Min(Math.Max(liPercent, MinPercent), MaxPercent);
			}

			return lOLayout;
		}

		public override string ToString()
		{
			StringBuilder lOText = new StringBuilder();

			for (int liAt = 0; liAt < mOPlacements.Length; liAt++)
			{
				Placement lOPlacement = mOPlacements[liAt];

				if (!lOPlacement.Placed && lOPlacement.Percent == 100)
					continue;

				lOText.Append(liAt).Append(':')
					.Append((lOPlacement.Placed ? lOPlacement.Anchor.X : -1f).ToString(CultureInfo.InvariantCulture)).Append(':')
					.Append(lOPlacement.Anchor.Y.ToString(CultureInfo.InvariantCulture)).Append(':')
					.Append(lOPlacement.Offset.X.ToString("0.##", CultureInfo.InvariantCulture)).Append(':')
					.Append(lOPlacement.Offset.Y.ToString("0.##", CultureInfo.InvariantCulture)).Append(':')
					.Append(lOPlacement.Percent).Append(';');
			}

			return lOText.ToString();
		}
	}
}
