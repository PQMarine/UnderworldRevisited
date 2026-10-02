namespace UWDataImport.UWData
{
	public interface IPalette
	{
		byte[] GetARGB(int piIndex);

		byte[] GetBGRA(int piIndex);

		byte[] GetRGB(int piIndex);

		UWColor32 GetUWColor(int piIndex);
	}
}
