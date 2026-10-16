using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The shrine on the Unity side. The mantras and the skill raising are engine-free in
/// UWShrineRules since 2026-09-18 (P3 of the engine separation); this class keeps the
/// old entry points for Interaction and, since 2026-09-22, hands in what the two QUEST
/// MANTRAS need - where the player stands, the two bits of PLAYER.DAT 0x60, and a hand to
/// put the Key of Truth in.
/// </summary>
public static class UWShrine
{
    /// <summary>Object number of the shrine.</summary>
    public const int ShrineObjectId = UWShrineRules.ShrineObjectId;

    /// <summary>What is entered at the shrine - the wording comes from UW.EXE.</summary>
    public const string Prompt = UWShrineRules.Prompt;

    /// <summary>Accepts a chanted mantra - see UWShrineRules.Chant.</summary>
    public static void Chant(string psMantra, UWCharacter pOCharacter, Interaction pOInteraction,
        DataImport pOData)
    {
        if (pOCharacter == null || pOInteraction == null || pOData == null)
            return;

        UWShrineRules.Chant(psMantra, pOCharacter.Vitals, pOData,
            pOInteraction.AddGeneralMessage, pOInteraction.AddMessage, new Host(pOData));
    }

    /// <summary>What the two quest mantras ask the world about - see UWShrineRules.IHost.</summary>
    private sealed class Host : UWShrineRules.IHost
    {
        private readonly DataImport mOData;

        private readonly UWLevelLoader mOLoader;

        internal Host(DataImport pOData)
        {
            mOData = pOData;
            mOLoader = UWScene.LevelLoader;
        }

        public int TileX
        {
            get { return fGetTile().X; }
        }

        public int TileY
        {
            get { return fGetTile().Y; }
        }

        /// <summary>One-based, as PLAYER.DAT keeps it and as the level phrases are counted.
        /// </summary>
        public int DungeonLevel
        {
            get { return mOLoader != null ? mOLoader.CurrentLevelIndex + 1 : 0; }
        }

        public bool CupOfWonderFound
        {
            get { return UWGameFlags.CupOfWonderFound; }
        }

        public bool KeyOfTruthGiven
        {
            get { return UWGameFlags.KeyOfTruthGiven; }
            set { UWGameFlags.KeyOfTruthGiven = value; }
        }

        /// <summary>
        /// The key goes onto the pointer, as place_new puts it; a held object no longer refuses
        /// it (the backpack, else the feet - UWInventory.GiveToPlayer, one behaviour for the
        /// four gifts since 2026-10-12).
        /// </summary>
        public bool GiveObject(int piObjectId)
        {
            UWInventory lOInventory = UWScene.Inventory;

            if (lOInventory == null || lOInventory.Model == null)
                return false;

            // CreateObj's fresh object, as place_new makes the key (mantra_advance, SKILLS.C):
            // quality 40 and the stack rule of its class. Until 2026-10-12 the key came at
            // quality 0 without a count.
            UWObject lOItem = UWObjectFactory.Create(piObjectId, mOData);

            if (lOItem == null)
                return false;

            return lOInventory.GiveToPlayer(lOItem, false);
        }

        private UWTilePos fGetTile()
        {
            UWPlayerMovement lOPlayer = UWScene.PlayerMovement;

            if (lOPlayer == null)
                return new UWTilePos(0, 0);

            Vector3 lOAt = lOPlayer.transform.position;

            return UWTileQueries.WorldToTile(lOAt.x, lOAt.z);
        }
    }
}
