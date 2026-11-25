using UWDataImport.Presentation;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE COMPASS PIECE PER GAME (UW2 plan, "The modern interface for UW2: one interface, two hosts", stage U6 step 1,
	/// 2026-11-23): how the modern compass (UWModernCompass) lays the compass on its disc and what the disc offers.
	///
	/// THE FIRST GAME: the cross freed of its stone (IUWModernArt.Compass with pbFreed) on the stone disc grown from the
	/// frame's pedestal (UWCompassDiscBuilder.Build), the disc's front with the movement arrows (its click areas,
	/// CompassDiscArrowAt); the power gem is a piece of its own.
	///
	/// THE SECOND GAME: the frame has no disc and no arrows; its compass lies in the middle recess of the marble ledge
	/// under the view (UWClassicLayoutUw2.CompassRecess), and the power gem in the compass's middle (POWER.GR at 0x71,
	/// 0x30). The disc is that recess cut from the frame (UWCompassDiscBuilder.BuildUw2), the compass laid on it WHOLE -
	/// the face's own stone covers the cross baked into the recess, as the classic screen draws it - and the gem on top.
	/// Off the disc the cross is freed as the first game's.
	/// </summary>
	public sealed class UWCompassGeometry
	{
		public bool IsSecondGame { get; private set; }

		/// <summary>The disc's front carries the movement arrows (the first game's alone).</summary>
		public bool HasArrows { get; private set; }

		/// <summary>The power gem lies in the compass (the second game's frame).</summary>
		public bool HasGem { get; private set; }

		/// <summary>On the disc the compass is the freed cross (the first game, the disc grown without its cross) or the
		/// whole picture, face and needle with their stone (the second game, the recess with its cross baked in).</summary>
		public bool IsFreedOnDisc { get; private set; }

		/// <summary>Where the compass picture's top-left corner lies on the disc picture (its outline's room in).</summary>
		public void CrossOnDisc(UWTextures pOTextures, out int piX, out int piY)
		{
			if (!IsSecondGame)
			{
				piX = UWClassicLayout.Compass.Left - UWHudArt.CompassDiscX;
				piY = UWClassicLayout.Compass.Top - UWHudArt.CompassDiscY;

				return;
			}

			UWRectInt lOBox = UWCompassDiscBuilder.Uw2CompassBox(pOTextures);

			piX = lOBox.X - (UWClassicLayoutUw2.CompassRecess.Left - UWCompassDiscBuilder.Uw2DiscPad);
			piY = lOBox.Y - (UWClassicLayoutUw2.CompassRecess.Top - UWCompassDiscBuilder.Uw2DiscPad);
		}

		/// <summary>Where the power gem's picture's top-left corner lies on the compass picture (HasGem).</summary>
		public void GemOnCross(UWTextures pOTextures, out int piX, out int piY)
		{
			piX = 0;
			piY = 0;

			if (!HasGem)
				return;

			UWRectInt lOBox = UWCompassDiscBuilder.Uw2CompassBox(pOTextures);

			piX = UWClassicLayoutUw2.PowerGem.Left - lOBox.X;
			piY = UWClassicLayoutUw2.PowerGem.Top - lOBox.Y;
		}

		/// <summary>The measures for the data's game.</summary>
		public static UWCompassGeometry Of(UWTextures pOTextures)
		{
			return pOTextures != null && pOTextures.Game == UWGame.Uw2 ? Uw2 : Uw1;
		}

		public static readonly UWCompassGeometry Uw1 = new UWCompassGeometry
		{
			IsSecondGame = false,
			HasArrows = true,
			HasGem = false,
			IsFreedOnDisc = true
		};

		public static readonly UWCompassGeometry Uw2 = new UWCompassGeometry
		{
			IsSecondGame = true,
			HasArrows = false,
			HasGem = true,
			IsFreedOnDisc = false
		};
	}
}
