namespace UWDataImport.UWData
{
	/// <summary>
	/// A rectangle in screen pixels, X and Y its lower left corner as the modern HUD counts them
	/// (from the bottom left) - the engine-free counterpart of UnityEngine.Rect for the HUD's
	/// layout rules (UWHudLayout, UWBagPlacement), with Rect's rules for the edges: Overlaps as
	/// Rect.Overlaps, touching edges do not overlap.
	/// </summary>
	public struct UWRect
	{
		public float X;

		public float Y;

		public float Width;

		public float Height;

		public UWRect(float pfX, float pfY, float pfWidth, float pfHeight)
		{
			X = pfX;
			Y = pfY;
			Width = pfWidth;
			Height = pfHeight;
		}

		public static readonly UWRect Zero = new UWRect(0f, 0f, 0f, 0f);

		public float XMin => X;

		public float XMax => X + Width;

		public float YMin => Y;

		public float YMax => Y + Height;

		public float CenterX => X + (Width / 2f);

		public float CenterY => Y + (Height / 2f);

		public bool Overlaps(UWRect pOOther)
		{
			return pOOther.XMax > XMin && pOOther.XMin < XMax && pOOther.YMax > YMin && pOOther.YMin < YMax;
		}
	}

	/// <summary>A rectangle of whole pixels in a picture, rows counted from the top as the
	/// files store them (UWPicture).</summary>
	public struct UWRectInt
	{
		public int X;

		public int Y;

		public int Width;

		public int Height;

		public UWRectInt(int piX, int piY, int piWidth, int piHeight)
		{
			X = piX;
			Y = piY;
			Width = piWidth;
			Height = piHeight;
		}
	}
}
