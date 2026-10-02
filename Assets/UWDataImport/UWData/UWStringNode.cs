namespace UWDataImport.UWData
{
	public class UWStringNode
	{
		public int CharSymbol;

		public int ParentNode;

		public int LeftChild;

		public int RightChild;

		public UWStringNode(uint plStringData)
		{
			CharSymbol = (sbyte)(plStringData & 0xFF);
			ParentNode = (byte)((plStringData & 0xFF00) >> 8);
			LeftChild = (byte)((plStringData & 0xFF0000) >> 16);
			RightChild = (byte)((plStringData & 0xFF000000u) >> 24);
		}
	}
}
