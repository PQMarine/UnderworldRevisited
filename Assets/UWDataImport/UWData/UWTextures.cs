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

		/// <summary>The game whose data this is (UW2 plan phase 2 step 1).</summary>
		public UWGame Game { get; }

		public UWTextures(string psUWDataPath, UWPalettes pOPalettes)
			: this(psUWDataPath, pOPalettes, UWGame.Uw1)
		{
		}

		/// <summary>
		/// THE PICTURES OF EITHER GAME (UW2 plan phase 2 step 1, 2026-10-29). The first game's as until
		/// then: F16, F32, W16 and W64.TR, its .GR files, its .BYT screens. The second game's: one
		/// T64.TR for walls and floors alike (Underworld Exhumed, docs/FORMATS.md "TERRAIN.DAT": a word
		/// per T64.TR texture, the level's texture map naming both from it), served under Wall64 and
		/// Floor32 so the lookups by type stay; its .GR files with three names the first game has not
		/// (WEAP.GR for the weapon pictures, GEMPT.GR, GHED.GR); its screens from BYT.ARK under the
		/// first game's screen types, each in its palette (UWScreensUw2).
		/// </summary>
		public UWTextures(string psUWDataPath, UWPalettes pOPalettes, UWGame peGame)
		{
			mOPalettes = pOPalettes;
			msUWDataPath = psUWDataPath;
			Game = peGame;

			if (peGame == UWGame.Uw2)
				fImportTexturesUw2();
			else
				fImportTextures();

			fImportImages();

			if (peGame == UWGame.Uw2)
				fImportScreensUw2();
			else
				fImportBitmaps();
		}

		/// <summary>The second game's wall and floor textures, T64.TR.</summary>
		public const string Uw2TextureFile = "T64.TR";

		/// <summary>
		/// T64.TR: the .TR header (type 2, the edge, the count) and an offset per texture, as the first
		/// game's files. Its 264 offsets name 256 textures; the last eight point to the file's end
		/// (measured 2026-10-29), so a texture whose pixels would run past the end is absent and left
		/// out - they are the last, so every texture keeps its number.
		/// </summary>
		private void fImportTexturesUw2()
		{
			string lsFile = Path.Combine(msUWDataPath, Uw2TextureFile);

			myRawData = File.ReadAllBytes(lsFile);

			int liEdge = myRawData[1];
			int liCount = BitConverter.ToUInt16(myRawData, 2);
			List<UWTexture> lOList = new List<UWTexture>();

			for (int liIndex = 0; liIndex < liCount; liIndex++)
			{
				long llOffset = BitConverter.ToUInt32(myRawData, 4 + (liIndex * 4));

				if (llOffset + (liEdge * liEdge) > myRawData.Length)
					continue;

				byte[] lyPixels = new byte[liEdge * liEdge];
				Array.Copy(myRawData, llOffset, lyPixels, 0L, lyPixels.Length);
				lOList.Add(new UWTexture(Uw2TextureFile, liIndex, liEdge, lyPixels, mOPalettes, UWTexture.TextureTypes.WALL));
			}

			mOTextures.Add(TextureFile.Wall64, lOList);
			mOTextures.Add(TextureFile.Floor32, lOList);
		}

		/// <summary>The second game's screens from BYT.ARK under the first game's screen types: the
		/// automap BLNKMAP, character creation CHARGEN, the conversation CONV, the game screen MAIN, the
		/// main menu OPSCR, the two logos PRES1 and PRES2, the two end screens WIN1 and WIN2.</summary>
		private void fImportScreensUw2()
		{
			mOBitmapFiles = new Dictionary<UWTexture.TextureTypes, UWTexture>();

			string lsArchive = Path.Combine(msUWDataPath, UWScreensUw2.ArchiveName);

			if (!File.Exists(lsArchive))
				return;

			UWArk lOArk = UWArk.Read(lsArchive, UWArk.LayoutEnum.Uw2);

			foreach (KeyValuePair<UWScreensUw2.ScreenEnum, UWTexture.TextureTypes> lOPair in Uw2ScreenTypes)
			{
				byte[] lyIndices = lOArk.ReadBlock((int)lOPair.Key);

				if (lyIndices.Length < UWScreensUw2.ScreenWidth * UWScreensUw2.ScreenHeight)
					continue;

				mOBitmapFiles.Add(lOPair.Value, new UWTexture(UWScreensUw2.ArchiveName, lyIndices, lOPair.Value, mOPalettes,
					UWScreensUw2.PaletteOf(lOPair.Key)));
			}
		}

		/// <summary>Which first-game screen type each BYT.ARK screen is served as.</summary>
		public static readonly Dictionary<UWScreensUw2.ScreenEnum, UWTexture.TextureTypes> Uw2ScreenTypes =
			new Dictionary<UWScreensUw2.ScreenEnum, UWTexture.TextureTypes>
		{
			{ UWScreensUw2.ScreenEnum.Automap, UWTexture.TextureTypes.BLNKMAP },
			{ UWScreensUw2.ScreenEnum.CharacterCreation, UWTexture.TextureTypes.CHARGEN },
			{ UWScreensUw2.ScreenEnum.Conversation, UWTexture.TextureTypes.CONV },
			{ UWScreensUw2.ScreenEnum.GameScreen, UWTexture.TextureTypes.MAIN },
			{ UWScreensUw2.ScreenEnum.MainMenu, UWTexture.TextureTypes.OPSCR },
			{ UWScreensUw2.ScreenEnum.OriginLogo, UWTexture.TextureTypes.PRES1 },
			{ UWScreensUw2.ScreenEnum.LookingGlassLogo, UWTexture.TextureTypes.PRES2 },
			{ UWScreensUw2.ScreenEnum.EndOfGame, UWTexture.TextureTypes.WIN1 },
			{ UWScreensUw2.ScreenEnum.EndOfGameSecond, UWTexture.TextureTypes.WIN2 }
		};

		/// <summary>The data files carry the image's upper-case names; on a case-sensitive file
		/// system (Linux) a lower-case pattern found nothing and the screen stayed black (patch
		/// by a Linux user, 2026-10-05).</summary>
		private static readonly EnumerationOptions CaseInsensitive = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };

		private void fImportTextures()
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(msUWDataPath);
			FileInfo[] files = directoryInfo.GetFiles("*.TR", CaseInsensitive);
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
			FileInfo[] files = directoryInfo.GetFiles("*.GR", CaseInsensitive);
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
					// The second game's (UW2 plan phase 2 step 1): its weapon pictures under the first
					// game's type, and two files of its own.
					case "WEAP.GR":
						textureTypes = UWTexture.TextureTypes.WEAPONS;
						break;
					case "GEMPT.GR":
						textureTypes = UWTexture.TextureTypes.GEMPT;
						break;
					case "GHED.GR":
						textureTypes = UWTexture.TextureTypes.GHED;
						break;
					default:
						throw new Exception($"Unknown image file. {files[num].Name}");
					}
					List<UWTexture> list = ReadImageFile(pyImagefileData, files[num].Name, mOPalettes, piMainPaletteIndex, textureTypes);
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

		/// <summary>
		/// THE PICTURES OF ONE .GR FILE (UW2 plan phase 0 step 6, 2026-10-28): the format byte, the
		/// count at 1 and the offsets from 3, as fImportImages read them inline for the first game's
		/// files until then - here for any file, the second game's .GR files sharing the format
		/// (Underworld Exhumed, docs/FORMATS.md "Pictures") but not the names this class knows.
		/// </summary>
		public static List<UWTexture> ReadImageFile(byte[] pyData, string psName, UWPalettes pOPalettes, int piMainPaletteIndex,
			UWTexture.TextureTypes peType)
		{
			UWTexture.UWImageFileFormatEnum peImageFileFormat = (UWTexture.UWImageFileFormatEnum)pyData[0];
			short num2 = BitConverter.ToInt16(pyData, 1);
			List<UWTexture> list = new List<UWTexture>();
			int num3 = 3;
			for (int num4 = 0; num4 < num2; num4++)
			{
				list.Add(new UWTexture(peImageFileFormat, ref pyData, BitConverter.ToInt32(pyData, num3), pOPalettes, piMainPaletteIndex, peType, num4, psName));
				num3 += 4;
			}
			return list;
		}

		private void fImportBitmaps()
		{
			mOBitmapFiles = new Dictionary<UWTexture.TextureTypes, UWTexture>();
			DirectoryInfo directoryInfo = new DirectoryInfo(msUWDataPath);
			FileInfo[] files = directoryInfo.GetFiles("*.BYT", CaseInsensitive);
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
				// Palette 6, as the original loads it (UWEDIT.C, LoadBitMap(6, pres2.byt)); 5 and 6
				// of PALS.DAT are byte-identical (Exhumed audit row 33).
				case "PRES2.BYT":
					piMainPaletteIndex = 6;
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
		/// PANELS.GR: four images of 83x114 each, without an image header (the second game's 79x112 and
		/// a narrow side of 3x112, from the distances between its offsets: 8848 bytes, 336 for the last;
		/// UW2 plan phase 2 step 1).
		/// The size is stored nowhere in
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
				int liPanelWidth = Game == UWGame.Uw2 ? Uw2PanelWidth : PanelWidth;
				int liPanelHeight = Game == UWGame.Uw2 ? Uw2PanelHeight : PanelHeight;
				bool lbIsEdge = liOffset + (liPanelWidth * liPanelHeight) > lyData.Length;

				int liWidth = lbIsEdge ? PanelEdgeWidth : liPanelWidth;
				int liHeight = lbIsEdge ? (Game == UWGame.Uw2 ? Uw2PanelEdgeHeight : PanelEdgeHeight) : liPanelHeight;

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

		public const int Uw2PanelWidth = 79;

		public const int Uw2PanelHeight = 112;

		public const int Uw2PanelEdgeHeight = 112;

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
