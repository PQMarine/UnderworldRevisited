namespace UWDataImport.UWData
{
	public struct WeaponCoordinate
	{
		public int Index { get; set; }

		public int X { get; set; }

		public int Y { get; set; }

		public override string ToString()
		{
			return $"X:{X}, Y:{Y}";
		}
	}
}
