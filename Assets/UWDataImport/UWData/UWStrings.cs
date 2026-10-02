using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	public class UWStrings
	{
		private List<UWStringNode> mOStringNodes;

		private List<UWStringBlock> mOStringBlocks;

		private Dictionary<int, UWStringBlock> mOBlockDict;

		public Dictionary<int, UWStringBlock> Blocks => mOBlockDict;

		public UWStrings(byte[] pyStringData)
		{
			mOStringNodes = new List<UWStringNode>();
			mOStringBlocks = new List<UWStringBlock>();
			uint num = BitConverter.ToUInt16(pyStringData, 0);
			int num2 = 2;
			for (int i = 0; i < num; i++)
			{
				mOStringNodes.Add(new UWStringNode(BitConverter.ToUInt32(pyStringData, num2)));
				num2 += 4;
			}
			ushort num3 = BitConverter.ToUInt16(pyStringData, num2);
			num2 += 2;
			for (int j = 0; j < num3; j++)
			{
				byte[] array = new byte[6];
				Array.Copy(pyStringData, num2, array, 0, 6);
				mOStringBlocks.Add(new UWStringBlock(array, ref pyStringData, mOStringNodes));
				num2 += 6;
			}
			mOStringBlocks.Sort();
			mOStringBlocks.Reverse();
			mOBlockDict = new Dictionary<int, UWStringBlock>();
			foreach (UWStringBlock mOStringBlock in mOStringBlocks)
			{
				mOBlockDict.Add(mOStringBlock.BlockNumber, mOStringBlock);
			}
		}
	}
}
