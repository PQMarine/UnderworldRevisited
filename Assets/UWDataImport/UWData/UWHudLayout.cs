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
	///     per part that was changed, an anchor X of -1 for a part only sized);
	///   - a DOCKED part (a panel at a screen edge with its handle) moved along its edge keeps the
	///     handle's height alone, anchored by thirds like a moved part, and stays docked - anchor X -2
	///     in the text (per user, 2026-11-08: the collapsed panel's handle dragged up or down in the
	///     layout editor); a version before reads that as "only sized" and keeps the default height.
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

			/// <summary>Docked, its handle moved along the edge: Anchor.Y and Offset.Y say where.</summary>
			public bool Docked;
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

			lfX = UWMath.Clamp(lfX, 0f, Math.Max(0f, pfScreenWidth - pODefault.Width));
			lfY = UWMath.Clamp(lfY, 0f, Math.Max(0f, pfScreenHeight - pODefault.Height));

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
			lOPlacement.Docked = false;
			lOPlacement.Anchor = new UWVector2(lfAnchorX, lfAnchorY);
			lOPlacement.Offset = new UWVector2((lfOwnX - (lfAnchorX * pfScreenWidth)) / lfScale, (lfOwnY - (lfAnchorY * pfScreenHeight)) / lfScale);
		}

		/// <summary>A docked part's handle moved along its edge to this rect: its height by thirds, the
		/// part stays docked (no longer placed freely).</summary>
		public void MoveDock(int piElement, UWRect pOHandle, float pfScreenHeight, float pfPixelScale)
		{
			Placement lOPlacement = mOPlacements[piElement];
			float lfAnchorY = fThird(pOHandle.CenterY, pfScreenHeight);
			float lfOwnY = pOHandle.Y + (lfAnchorY * pOHandle.Height);

			lOPlacement.Placed = false;
			lOPlacement.Docked = true;
			lOPlacement.Anchor = new UWVector2(0f, lfAnchorY);
			lOPlacement.Offset = new UWVector2(0f, (lfOwnY - (lfAnchorY * pfScreenHeight)) / Math.Max(0.01f, pfPixelScale));
		}

		/// <summary>Where a docked part's handle of this height lies (its bottom, screen pixels from the
		/// bottom), kept between pfMinY and pfMaxY; false when its handle was never moved.</summary>
		public bool TryPlaceDock(int piElement, float pfHandleHeight, float pfScreenHeight, float pfPixelScale, float pfMinY, float pfMaxY,
			out float pfBottom)
		{
			Placement lOPlacement = mOPlacements[piElement];

			pfBottom = 0f;

			if (!lOPlacement.Docked || lOPlacement.Placed)
				return false;

			float lfAnchorY = (lOPlacement.Anchor.Y * pfScreenHeight) + (lOPlacement.Offset.Y * pfPixelScale);

			pfBottom = (float)Math.Round(UWMath.Clamp(lfAnchorY - (lOPlacement.Anchor.Y * pfHandleHeight), pfMinY, Math.Max(pfMinY, pfMaxY)));

			return true;
		}

		/// <summary>A new size: the part stays at its anchor point (where it was drawn when it had
		/// none yet) and grows away from it.</summary>
		public void SetPercent(int piElement, int piPercent, UWRect pOCurrent, float pfScreenWidth, float pfScreenHeight, float pfPixelScale)
		{
			Placement lOPlacement = mOPlacements[piElement];

			// A docked part whose handle was moved keeps its edge and its height.
			if (!lOPlacement.Placed && !lOPlacement.Docked)
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
				lOPlacement.Docked = lfValues[0] < -1.5f;
				lOPlacement.Anchor = new UWVector2(UWMath.Clamp(lfValues[0], 0f, 1f), UWMath.Clamp(lfValues[1], 0f, 1f));
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

				if (!lOPlacement.Placed && !lOPlacement.Docked && lOPlacement.Percent == 100)
					continue;

				lOText.Append(liAt).Append(':')
					.Append((lOPlacement.Placed ? lOPlacement.Anchor.X : lOPlacement.Docked ? -2f : -1f).ToString(CultureInfo.InvariantCulture)).Append(':')
					.Append(lOPlacement.Anchor.Y.ToString(CultureInfo.InvariantCulture)).Append(':')
					.Append(lOPlacement.Offset.X.ToString("0.##", CultureInfo.InvariantCulture)).Append(':')
					.Append(lOPlacement.Offset.Y.ToString("0.##", CultureInfo.InvariantCulture)).Append(':')
					.Append(lOPlacement.Percent).Append(';');
			}

			return lOText.ToString();
		}
	}
}
