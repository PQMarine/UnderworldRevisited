using System.Collections.Generic;

namespace UWDataImport.UWData
{
	public class UWCursors
	{
		public enum CursorEnum
		{
			Center,
			Forward,
			Backward,
			StrafeLeft,
			StrafeRight,
			TurnLeft,
			TurnRight,
			SlightLeft,
			SlightRight,
			Aim,
			Enchant,
			Use,
			Feather,
			Erase,
			FeatherWrite
		}

		private List<UWTexture> mCursorImages;

		public UWCursors(List<UWTexture> pCursorImages)
		{
			mCursorImages = pCursorImages;
		}

		public UWTexture GetCursor(CursorEnum pCursor)
		{
			return mCursorImages[(int)pCursor];
		}
	}
}
