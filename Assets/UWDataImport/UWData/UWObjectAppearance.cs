using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHICH PICTURE AN OBJECT WEARS: the OBJECTS.GR image of its id, and for the few kinds
	/// that are drawn from another file (pillars, levers, bridges, gravestones, tables, chairs,
	/// wall decorations, switches, doors) that file's image, with the OBJECTS.GR one kept as
	/// the icon. Until 2026-10-12 the per-id rules lived in UWLevel.load_object_list and about
	/// thirty sites that make an object during play looked its OBJECTS.GR picture up by hand in
	/// a try/catch (cleanup plan section 4.1); now the level loader and UWObjectFactory both
	/// come through here.
	/// </summary>
	public static class UWObjectAppearance
	{
		/// <summary>The highest id with an OBJECTS.GR picture; the four above it have none.</summary>
		public const int LastPicturedObjectId = 460;

		/// <summary>The level's texture table (UWLevel.TextureInfos) holds the six door
		/// textures from this entry on.</summary>
		public const int DoorTextureTableStart = 58;

		/// <summary>Bridges with flags of 2 and above take the level's floor list from this entry
		/// on (the reference's bridge.cs: texture_map[flags - 2 + 48]).</summary>
		public const int BridgeFloorTableStart = 48;

		/// <summary>
		/// THE SECOND GAME'S TABLE (UW2 plan phase 2 step 3): UWLevel.Uw2TextureInfoCount entries - its 64
		/// textures for walls and floors alike, its six doors after them (UWLevel). So its doors start at
		/// 64 and a bridge's floor at 0 (the floor index straight into the map, as the tiles'). Told apart
		/// by the table's length.
		/// </summary>
		public const int Uw2DoorTextureTableStart = UWTextureMapUw2.TextureCount;

		public const int Uw2BridgeFloorTableStart = 0;

		private static bool fIsUw2Table(ushort[] pOTextureInfos) => pOTextureInfos != null && pOTextureInfos.Length == UWLevel.Uw2TextureInfoCount;

		/// <summary>TMOBJ.GR has 38 images in the shipped installation (checked by dumping them);
		/// a documented index beyond them is clamped to the last.</summary>
		public const int DocumentedChairTexture = 38;

		/// <summary>
		/// Attaches the pictures of its id to the object. pOTextureInfos is the level's texture
		/// table for the kinds that draw a level texture (bridge, wall decoration, door); null
		/// leaves those with their icon only, which is what an object made away from a level
		/// gets. Returns whether the object has a picture at all afterwards - the data has none
		/// for an id beyond LastPicturedObjectId or when the file lacks the image.
		/// </summary>
		public static bool Apply(UWObject pOObject, UWTextures pOTextures, ushort[] pOTextureInfos)
		{
			if (pOObject == null || pOTextures == null)
				return false;

			int liId = pOObject.ID;

			if (liId <= LastPicturedObjectId)
				pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.OBJECTS, liId);

			// Pillar (352, 0x0160): otherwise falls back to the generic object icon. In the
			// original it shows a texture that differs per object instance
			// (confirmed by screenshot comparison). Originally implemented via Owner->wall texture
			// (as for the wall decoration 366/367) - according to uw-formats.txt
			// (vividos/UnderworldAdventures), however, it is "the lower byte of the flags field"
			// from tmobj.gr, not the wall texture. tmobj.gr images 0-3 really are
			// narrow pillar edge textures (confirmed by visual check), hence masked to 2 bits
			// (4 variants).
			if (liId == 352)
			{
				pOObject.Icon = fGet(pOTextures, UWTexture.TextureTypes.OBJECTS, liId);
				pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.TMOBJ, pOObject.Flags & 3);
			}
			if (liId == 353)
			{
				pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.TMOBJ, (pOObject.Flags & 7) + 4);
			}
			if (liId == 356)
			{
				pOObject.Icon = fGet(pOTextures, UWTexture.TextureTypes.OBJECTS, liId);
				if (pOObject.Flags < 2)
				{
					pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.TMOBJ, pOObject.Flags + 30);
				}
				// From flags 2 on the level's floor list, entry flags - 2 (the reference's bridge.cs:
				// texture_map[flags - 2 + 48]). Ours took flags - 1 until 2026-09-29 and showed the
				// plate over the Wine of Compassion (level 6, 27/50, flags 2) in the grey floor 27
				// instead of the brown marble 12 (per user with screenshots of the original).
				if (pOObject.Flags > 1 && pOTextureInfos != null)
				{
					pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.FLOOR,
						pOTextureInfos[pOObject.Flags - 2 + (fIsUw2Table(pOTextureInfos) ? Uw2BridgeFloorTableStart : BridgeFloorTableStart)]);
				}
			}
			if (liId == 358)
			{
				int liVariant = pOObject.Flags & 7;
				pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.TMOBJ, liVariant + 20);
			}
			// Gravestone (357, 0x0165): otherwise falls back to the generic object icon.
			// According to uw-formats.txt (vividos/UnderworldAdventures) the wall texture of the
			// gravestone is "flags" + 28 from tmobj.gr. But tmobj.gr has only 38 images, and of those
			// only 28 and 29 are actually gravestone reliefs by visual check (dump of all 38
			// images) (28 matches the original screenshot exactly) - so "flags" here
			// means only 1 bit (2 variants), not the full value range. Fits the
			// same mask pattern as the lever (353: Flags&7+4) or object 358
			// (Flags&7+20). The grave text display (grave.dat/quantity) is separate and not
			// implemented here.
			if (liId == 357)
			{
				pOObject.Icon = fGet(pOTextures, UWTexture.TextureTypes.OBJECTS, liId);
				pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.TMOBJ, (pOObject.Flags & 1) + 28);
			}
			// Table (344, 0x0158) and chair (348, 0x015C): according to uw-formats.txt fixed
			// TMOBJ textures (not flags-dependent as with other special cases), otherwise
			// both fall back to the generic object icon. tmobj.gr has only 38 images in this
			// installation (index 0-37, confirmed by visual check) - the
			// documented chair index 38 would be outside the valid range
			// (ArgumentOutOfRangeException, same pattern as with the gravestone before), hence
			// defensively clamped here against the actual image count.
			if (liId == 344)
			{
				pOObject.Icon = fGet(pOTextures, UWTexture.TextureTypes.OBJECTS, liId);
				pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.TMOBJ, 32);
			}
			if (liId == 348)
			{
				List<UWTexture> lOTmobjTextures = pOTextures.GetTexturesByType(UWTexture.TextureTypes.TMOBJ);
				int liChairTextureIndex = (lOTmobjTextures != null && lOTmobjTextures.Count > 0)
					? System.Math.Min(DocumentedChairTexture, lOTmobjTextures.Count - 1)
					: 0;

				pOObject.Icon = fGet(pOTextures, UWTexture.TextureTypes.OBJECTS, liId);
				pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.TMOBJ, liChairTextureIndex);
			}
			// Wall decorations 366/367 take their wall texture from the Owner via the level
			// texture table. 365 "force field" is the same wall panel: on level 9 with Owner 0 and thus
			// wall texture 64, "nothing" according to string block 10 - black, it hides the paths
			// (per user in the original, 2026-09-14). The reference's tmap.cs also uses the Owner.
			if (liId == 365 || liId == 366 || liId == 367)
			{
				pOObject.Icon = fGet(pOTextures, UWTexture.TextureTypes.OBJECTS, liId);
				if (pOTextureInfos != null)
					pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.WALL, pOTextureInfos[pOObject.Owner]);
			}
			if (liId >= 368 && liId <= 383)
			{
				pOObject.Icon = fGet(pOTextures, UWTexture.TextureTypes.OBJECTS, liId);
				pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.TMFLAT, liId & 0xF);
			}
			// Doors come in TWO groups of eight (string block 4): 320-325 are closed
			// doors, 326 a portcullis, 327 a secret door - and 328-333, 334, 335 the same
			// in open state.
			//
			// Until 2026-09-03 the condition ended at 327, which made the branch for the open
			// doors inside it unreachable. This could not show up as a missing
			// texture: they kept the general OBJECTS.GR assignment, i.e. their
			// inventory icon, and were therefore drawn as placeholders (reported by the user on
			// level 7; the ids occur on every level, see the removed tool UWDoorIdDump).
			if (UWObjectIds.IsDoor(liId))
			{
				pOObject.Icon = fGet(pOTextures, UWTexture.TextureTypes.OBJECTS, liId);

				// Six door images per group, and the level table holds exactly six
				// entries (58 to 63, see UWLevel.TextureInfos). Both groups use
				// the same six; the portcullis and the secret door have none.
				if (pOTextureInfos != null)
				{
					int liDoors = fIsUw2Table(pOTextureInfos) ? Uw2DoorTextureTableStart : DoorTextureTableStart;

					if (liId < UWObjectIds.Portcullis)
					{
						pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.DOORS,
							pOTextureInfos[liId - UWObjectIds.FirstClosedDoor + liDoors]);
					}
					else if (liId >= UWObjectIds.FirstOpenDoor && liId < UWObjectIds.OpenPortcullis)
					{
						pOObject.Texture = fGet(pOTextures, UWTexture.TextureTypes.DOORS,
							pOTextureInfos[liId - UWObjectIds.FirstOpenDoor + liDoors]);
					}
				}

				// Portcullis (326 and 334): no texture from lev.ark/DOORS.GR - the
				// grid geometry is procedural (UWObjectSpawner.fSpawnPortcullis), the
				// real original graphic could not be found in any accessible data source
				// (model table, DOORS.GR, TMOBJ.GR, TMFLAT.GR - all searched).
				//
				// Secret door (327 and 335): according to the user's knowledge (not from the docs)
				// almost identical to the normal door, only with the tile's wall texture instead of
				// the door graphic - disguised as part of the wall. That happens in
				// UWObjectSpawner.fSpawnDoor directly via the tile (pOTile.TextureWall),
				// analogous to the door frame (fSpawnDoorFrames); the texture deliberately stays
				// unchanged here.
			}

			return pOObject.Texture != null;
		}

		/// <summary>The OBJECTS.GR picture alone, as the sites that built an object by hand
		/// looked it up - null for an id the file has no image for.</summary>
		public static UWTexture Picture(UWTextures pOTextures, int piObjectId)
		{
			return pOTextures != null ? fGet(pOTextures, UWTexture.TextureTypes.OBJECTS, piObjectId) : null;
		}

		/// <summary>
		/// THE PICTURE OF AN OPEN CONTAINER: the OBJECTS.GR image of its open variant
		/// (UWObjectIds.OpenPicture - sack to gold coffer with bit 0 set, urn, quiver and bowl
		/// themselves), null without a container or an image. The ONE home of this rule since
		/// cleanup step 5.6 (2026-10-20): the classic frame's picture above the open container
		/// (UWHudInventory), the modern bags' open bag (UWModernBags) and the modern original page
		/// (UWModernInventoryPage) all ask here. The last showed the closed picture until then - it
		/// took the object's own Icon ?? Texture (B29 of the cleanup plan; decided by the user the
		/// same day: it shows the open one, as the other two do).
		/// </summary>
		public static UWTexture OpenContainerPicture(UWTextures pOTextures, UWObject pOContainer)
		{
			return pOContainer != null ? Picture(pOTextures, UWObjectIds.OpenPicture(pOContainer.ID)) : null;
		}

		/// <summary>UWTextures throws for an index its file does not have; an object without a
		/// picture is not an error here (the spawner skips it, the sites gave up on it).</summary>
		private static UWTexture fGet(UWTextures pOTextures, UWTexture.TextureTypes peType, int piIndex)
		{
			try
			{
				return pOTextures.GetTextureByType(peType, piIndex);
			}
			catch
			{
				return null;
			}
		}
	}
}
