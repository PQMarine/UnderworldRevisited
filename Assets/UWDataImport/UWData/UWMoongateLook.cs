namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MOONGATE'S COLOURS (do_rect for model 0x17, both games: uw1 and uw2 3d/DRAWOBJ.C, the colour record's flag
	/// 0x80 "colours animated by the clock"; per user 2026-11-22 with pictures of the second game's Void: ours was one
	/// flat colour and did not move). The record's three colours are offsets, 0, 2 and 4, and each register gets the
	/// gate's link field (link &amp; 0x1FF; a gate's link is 0x200 plus its colour) plus the clock step plus its offset.
	/// The clock step runs 0, 1, 2, 3, 3, 2, 1, 0, one step every 64 ticks of the 1/256 s game clock
	/// ((GAME_TIME &amp; 0x1FF) &gt;&gt; 6, the upper half mirrored). The gate's four faces are Gouraud quads, dark at the
	/// corners and light in the middle, on register 0.
	/// </summary>
	public static class UWMoongateLook
	{
		/// <summary>The moongate's model.</summary>
		public const int Model = 0x17;

		/// <summary>From this value on a moongate's link field is a colour (0x200 plus the colour).</summary>
		public const int ColourLinkBase = 0x200;

		/// <summary>The clock's step for colours animated by it: 0 to 3 and back, a step every 64 ticks. The large blackrock
		/// gem's body pulses with the same step (UWBlackrockGemLook).</summary>
		public static int ClockStep(long plGameTime)
		{
			int liStep = (int)((plGameTime & 0x1FF) >> 6);

			return (liStep & 4) != 0 ? 3 - (liStep & 3) : liStep;
		}

		/// <summary>True when the gate's link field carries its colour (UWEndgameSequence and the level data set it so).</summary>
		public static bool HasColourLink(UWObject pOGate)
		{
			return pOGate != null && pOGate.HasQuantity && pOGate.Quantity >= ColourLinkBase;
		}

		/// <summary>The palette colour of a face whose record colour (the offset) is piRecordColour, at this clock.</summary>
		public static int Colour(int piLink, int piRecordColour, long plGameTime)
		{
			return ((piLink & 0x1FF) + ClockStep(plGameTime) + piRecordColour) & 0xFF;
		}
	}
}
