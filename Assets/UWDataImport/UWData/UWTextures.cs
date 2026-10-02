using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UWDataImport.UWData
{
	public class UWTextures
	{
		public enum TextureFile
		{
			Floor16,
			Floor32,
			Wall16,
			Wall64
		}

		private readonly string msUWDataPath;

		private byte[] myRawData;

		private readonly UWPalettes mOPalettes;

		private readonly Dictionary<TextureFile, List<UWTexture>> mOTextures = new Dictionary<TextureFile, List<UWTexture>>();

		private Dictionary<UWTexture.TextureTypes, List<UWTexture>> mOImageFiles;

		private Dictionary<UWTexture.TextureTypes, UWTexture> mOBitmapFiles;

		public Dictionary<TextureFile, List<UWTexture>> Textures => mOTextures;

		public Dictionary<UWTexture.TextureTypes, List<UWTexture>> ImageFiles => mOImageFiles;

		public Dictionary<UWTexture.TextureTypes, UWTexture> BitmapFiles => mOBitmapFiles;

		public UWTextures(string psUWDataPath, UWPalettes pOPalettes)
		{
			mOPalettes = pOPalettes;
			msUWDataPath = psUWDataPath;
			fImportTextures();
			fImportImages();
			fImportBitmaps();
		}

		private void fImportTextures()
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(msUWDataPath);
			FileInfo[] files = directoryInfo.GetFiles("*.TR");
			files = files.OrderBy((FileInfo n) => n.Name).ToArray();
			for (int num = 0; num < files.Length; num++)
			{
				myRawData = File.ReadAllBytes(files[num].FullName);
				UWTexture.TextureTypes peTextureType = UWTexture.TextureTypes.WALL;
				if (files[num].Name.StartsWith("F", StringComparison.InvariantCultureIgnoreCase))
				{
					peTextureType = UWTexture.TextureTypes.FLOOR;
				}
				byte b = myRawData[1];
				byte[] array = new byte[2];
				Array.Copy(myRawData, 2, array, 0, 2);
				ushort num2 = BitConverter.ToUInt16(array, 0);
				uint[] array2 = new uint[num2];
				for (int num3 = 0; num3 < num2; num3++)
				{
					array = new byte[4];
					Array.Copy(myRawData, num3 * 4 + 4, array, 0, 4);
					array2[num3] = BitConverter.ToUInt32(array, 0);
				}
				List<UWTexture> list = new List<UWTexture>();
				for (int num4 = 0; num4 < num2; num4++)
				{
					byte[] array3 = new byte[b * b];
					Array.Copy(myRawData, array2[num4], array3, 0L, b * b);
					list.Add(new UWTexture(files[num].Name, num4, b, array3, mOPalettes, peTextureType));
				}
				mOTextures.Add((TextureFile)num, list);
			}
		}

		private void fImportImages()
		{
			mOImageFiles = new Dictionary<UWTexture.TextureTypes, List<UWTexture>>();
			DirectoryInfo directoryInfo = new DirectoryInfo(msUWDataPath);
			FileInfo[] files = directoryInfo.GetFiles("*.gr", SearchOption.TopDirectoryOnly);
			files = files.OrderBy((FileInfo n) => n.Name).ToArray();
			for (int num = 0; num < files.Length; num++)
			{
				if (files[num].Name.Equals("PANELS.GR", StringComparison.InvariantCultureIgnoreCase))
				{
					fImportPanels(files[num].FullName);
					continue;
				}

				{
					byte[] pyImagefileData = File.ReadAllBytes(files[num].FullName);
					UWTexture.UWImageFileFormatEnum peImageFileFormat = (UWTexture.UWImageFileFormatEnum)pyImagefileData[0];
					short num2 = BitConverter.ToInt16(pyImagefileData, 1);
					List<UWTexture> list = new List<UWTexture>();
					int piMainPaletteIndex = 0;
					UWTexture.TextureTypes textureTypes;
					switch (files[num].Name.ToUpper())
					{
					case "3DWIN.GR":
						textureTypes = UWTexture.TextureTypes.THRREDWIN;
						break;
					case "ANIMO.GR":
						textureTypes = UWTexture.TextureTypes.ANIMO;
						break;
					case "ARMOR_F.GR":
						textureTypes = UWTexture.TextureTypes.ARMOR_F;
						break;
					case "ARMOR_M.GR":
						textureTypes = UWTexture.TextureTypes.ARMOR_M;
						break;
					case "BODIES.GR":
						textureTypes = UWTexture.TextureTypes.BODIES;
						break;
					case "BUTTONS.GR":
						textureTypes = UWTexture.TextureTypes.BUTTONS;
						break;
					case "CHAINS.GR":
						textureTypes = UWTexture.TextureTypes.CHAINS;
						break;
					case "CHARHEAD.GR":
						textureTypes = UWTexture.TextureTypes.CHARHEAD;
						break;
					case "CHRBTNS.GR":
						textureTypes = UWTexture.TextureTypes.CHRBTNS;
						piMainPaletteIndex = 3;
						break;
					case "COMPASS.GR":
						textureTypes = UWTexture.TextureTypes.COMPASS;
						break;
					case "CONVERSE.GR":
						textureTypes = UWTexture.TextureTypes.CONVERSE;
						break;
					case "CURSORS.GR":
						textureTypes = UWTexture.TextureTypes.CURSORS;
						break;
					case "DOORS.GR":
						textureTypes = UWTexture.TextureTypes.DOORS;
						break;
					case "DRAGONS.GR":
						textureTypes = UWTexture.TextureTypes.DRAGONS;
						break;
					case "EYES.GR":
						textureTypes = UWTexture.TextureTypes.EYES;
						break;
					case "FLASKS.GR":
						textureTypes = UWTexture.TextureTypes.FLASKS;
						break;
					case "GENHEAD.GR":
						textureTypes = UWTexture.TextureTypes.GENHEAD;
						break;
					case "HEADS.GR":
						textureTypes = UWTexture.TextureTypes.HEADS;
						break;
					case "INV.GR":
						textureTypes = UWTexture.TextureTypes.INV;
						break;
					case "LFTI.GR":
						textureTypes = UWTexture.TextureTypes.LFTI;
						break;
					case "OBJECTS.GR":
						textureTypes = UWTexture.TextureTypes.OBJECTS;
						break;
					case "OPBTN.GR":
						textureTypes = UWTexture.TextureTypes.OPBTN;
						piMainPaletteIndex = 2;
						break;
					case "OPTB.GR":
						textureTypes = UWTexture.TextureTypes.OPTB;
						break;
					case "OPTBTNS.GR":
						textureTypes = UWTexture.TextureTypes.OPTBTNS;
						break;
					case "PANELS.GR":
						textureTypes = UWTexture.TextureTypes.PANELS;
						break;
					case "POWER.GR":
						textureTypes = UWTexture.TextureTypes.POWER;
						break;
					case "QUESTION.GR":
						textureTypes = UWTexture.TextureTypes.QUESTION;
						break;
					case "SCRLEDGE.GR":
						textureTypes = UWTexture.TextureTypes.SCRLEDGE;
						break;
					case "SPELLS.GR":
						textureTypes = UWTexture.TextureTypes.SPELLS;
						break;
					case "TMFLAT.GR":
						textureTypes = UWTexture.TextureTypes.TMFLAT;
						break;
					case "TMOBJ.GR":
						textureTypes = UWTexture.TextureTypes.TMOBJ;
						break;
					case "VIEWS.GR":
						textureTypes = UWTexture.TextureTypes.VIEWS;
						break;
					case "WEAPONS.GR":
						textureTypes = UWTexture.TextureTypes.WEAPONS;
						break;
					default:
						throw new Exception($"Unknown image file. {files[num].Name}");
					}
					int num3 = 3;
					for (int num4 = 0; num4 < num2; num4++)
					{
						list.Add(new UWTexture(peImageFileFormat, ref pyImagefileData, BitConverter.ToInt32(pyImagefileData, num3), mOPalettes, piMainPaletteIndex, textureTypes, num4, files[num].Name));
						num3 += 4;
					}
					if (mOImageFiles.ContainsKey(textureTypes))
					{
						mOImageFiles[textureTypes].AddRange(list);
					}
					else
					{
						mOImageFiles.Add(textureTypes, list);
					}
				}
			}
		}

		private void fImportBitmaps()
		{
			mOBitmapFiles = new Dictionary<UWTexture.TextureTypes, UWTexture>();
			DirectoryInfo directoryInfo = new DirectoryInfo(msUWDataPath);
			FileInfo[] files = directoryInfo.GetFiles("*.byt", SearchOption.TopDirectoryOnly);
			files = files.OrderBy((FileInfo n) => n.Name).ToArray();
			for (int num = 0; num < files.Length; num++)
			{
				int piMainPaletteIndex;
				UWTexture.TextureTypes textureTypes;
				switch (files[num].Name.ToUpper())
				{
				case "BLNKMAP.BYT":
					piMainPaletteIndex = 1;
					textureTypes = UWTexture.TextureTypes.BLNKMAP;
					break;
				case "CHARGEN.BYT":
					piMainPaletteIndex = 3;
					textureTypes = UWTexture.TextureTypes.CHARGEN;
					break;
				case "CONV.BYT":
					piMainPaletteIndex = 0;
					textureTypes = UWTexture.TextureTypes.CONV;
					break;
				case "MAIN.BYT":
					piMainPaletteIndex = 0;
					textureTypes = UWTexture.TextureTypes.MAIN;
					break;
				case "OPSCR.BYT":
					piMainPaletteIndex = 2;
					textureTypes = UWTexture.TextureTypes.OPSCR;
					break;
				case "PRES1.BYT":
					piMainPaletteIndex = 5;
					textureTypes = UWTexture.TextureTypes.PRES1;
					break;
				case "PRES2.BYT":
					piMainPaletteIndex = 5;
					textureTypes = UWTexture.TextureTypes.PRES2;
					break;
				case "WIN1.BYT":
					piMainPaletteIndex = 7;
					textureTypes = UWTexture.TextureTypes.WIN1;
					break;
				case "WIN2.BYT":
					piMainPaletteIndex = 7;
					textureTypes = UWTexture.TextureTypes.WIN2;
					break;
				default:
					throw new Exception($"Unknown image file. {files[num].Name}");
				}
				mOBitmapFiles.Add(textureTypes, new UWTexture(files[num].Name, File.ReadAllBytes(files[num].FullName), textureTypes, mOPalettes, piMainPaletteIndex));
			}
		}

		public byte[] GetWallTextureRGB(int piIndex)
		{
			return mOTextures[TextureFile.Wall64][piIndex].GetRGB();
		}

		public UWColor32[] GetWallTextureUWColor32(int piIndex)
		{
			return mOTextures[TextureFile.Wall64][piIndex].GetUWColor32();
		}

		public UWColor32[] GetFloorTextureUWColor32(int piIndex)
		{
			return mOTextures[TextureFile.Floor32][piIndex].GetUWColor32();
		}

		/// <summary>
		/// PANELS.GR: four images of 83x114 each, without an image header. The size is stored nowhere in
		/// the file - that it is correct is shown by the distance between image starts: 9462 bytes, exactly
		/// 83 times 114. The last image is shorter: it is read as the narrow side of the panel,
		/// PanelEdgeWidth by PanelEdgeHeight (see below). Offsets outside the file are skipped.
		/// </summary>
		private void fImportPanels(string psFile)
		{
			byte[] lyData = File.ReadAllBytes(psFile);

			if (lyData.Length < 3)
				return;

			int liCount = BitConverter.ToInt16(lyData, 1);
			List<UWTexture> lOPanels = new List<UWTexture>();

			for (int liIndex = 0; liIndex < liCount; liIndex++)
			{
				int liOffset = BitConverter.ToInt32(lyData, 3 + (liIndex * 4));

				if (liOffset < 0 || liOffset >= lyData.Length)
					continue;

				// The last image is the NARROW SIDE of the panel - three pixels wide,
				// 120 high, i.e. six more than the front: the piece of chain hangs
				// from the bottom. It is visible in the middle step of the rotation, when the
				// panel is seen from the side (per user screenshot, 2026-09-02).
				bool lbIsEdge = liOffset + (PanelWidth * PanelHeight) > lyData.Length;

				int liWidth = lbIsEdge ? PanelEdgeWidth : PanelWidth;
				int liHeight = lbIsEdge ? PanelEdgeHeight : PanelHeight;

				if (liOffset + (liWidth * liHeight) > lyData.Length)
					continue;

				byte[] lyPixels = new byte[liWidth * liHeight];
				Array.Copy(lyData, liOffset, lyPixels, 0, lyPixels.Length);

				lOPanels.Add(new UWTexture("PANELS.GR", lyPixels, UWTexture.TextureTypes.PANELS,
					mOPalettes, 0, liWidth, liHeight));
			}

			mOImageFiles[UWTexture.TextureTypes.PANELS] = lOPanels;
		}

		public const int PanelWidth = 83;

		public const int PanelHeight = 114;

		public const int PanelEdgeWidth = 3;

		public const int PanelEdgeHeight = 120;

		public UWTexture GetTextureByType(UWTexture.TextureTypes peTextureType, int piIndex)
		{
			switch (peTextureType)
			{
			case UWTexture.TextureTypes.WALL:
				return mOTextures[TextureFile.Wall64][piIndex];
			case UWTexture.TextureTypes.FLOOR:
				return mOTextures[TextureFile.Floor32][piIndex];
			case UWTexture.TextureTypes.BLNKMAP:
			case UWTexture.TextureTypes.CHARGEN:
			case UWTexture.TextureTypes.CONV:
			case UWTexture.TextureTypes.MAIN:
			case UWTexture.TextureTypes.OPSCR:
			case UWTexture.TextureTypes.PRES1:
			case UWTexture.TextureTypes.PRES2:
			case UWTexture.TextureTypes.WIN1:
			case UWTexture.TextureTypes.WIN2:
				return mOBitmapFiles[peTextureType];
			default:
				return mOImageFiles[peTextureType][piIndex];
			}
		}

		public List<UWTexture> GetTexturesByType(UWTexture.TextureTypes peTextureType)
		{
			switch (peTextureType)
			{
			case UWTexture.TextureTypes.WALL:
				return mOTextures[TextureFile.Wall64];
			case UWTexture.TextureTypes.FLOOR:
				return mOTextures[TextureFile.Floor32];
			case UWTexture.TextureTypes.BLNKMAP:
			case UWTexture.TextureTypes.CHARGEN:
			case UWTexture.TextureTypes.CONV:
			case UWTexture.TextureTypes.MAIN:
			case UWTexture.TextureTypes.OPSCR:
			case UWTexture.TextureTypes.PRES1:
			case UWTexture.TextureTypes.PRES2:
			case UWTexture.TextureTypes.WIN1:
			case UWTexture.TextureTypes.WIN2:
				return new List<UWTexture> { mOBitmapFiles[peTextureType] };
			default:
				return mOImageFiles[peTextureType];
			}
		}
	}
}
