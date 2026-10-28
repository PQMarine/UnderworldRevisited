using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The motion world of a REAL level (the motor plan's test bench, M0, 2026-10-27): the tiles,
    /// the objects lying in them and the creatures standing in them, as the game's motion host
    /// (UWMotionHost) hands them to the core - the type, the floor nibble and the floor's terrain
    /// class of a tile; the lying things by their master-list index; a creature by its own record's
    /// place. Nothing moves but the mover under test: the creatures stand, a pushed object stays,
    /// a move trigger is only written down (Triggers), so a run can be compared with another.
    /// </summary>
    public sealed class LevelMotionWorld : IUWMotionWorld
    {
        private readonly UWLevel mOLevel;

        private readonly UWTerrain mOTerrain;

        private readonly Dictionary<UWObject, int> mOIndex = new Dictionary<UWObject, int>();

        private UWOriginalRandom mORandom;

        /// <summary>The move triggers met since the last Reset, by master-list index, in order.</summary>
        public readonly List<int> Triggers = new List<int>();

        public LevelMotionWorld(UWLevel pOLevel, UWTerrain pOTerrain, int piSeed)
        {
            mOLevel = pOLevel;
            mOTerrain = pOTerrain;
            mORandom = new UWOriginalRandom(piSeed);

            List<UWObject> lOMaster = pOLevel != null ? pOLevel.Masterlist : null;

            if (lOMaster == null)
                return;

            for (int liAt = 0; liAt < lOMaster.Count; liAt++)
            {
                if (lOMaster[liAt] != null && !mOIndex.ContainsKey(lOMaster[liAt]))
                    mOIndex[lOMaster[liAt]] = liAt;
            }
        }

        public UWLevel Level => mOLevel;

        /// <summary>A new run: the trigger list emptied, the generator seeded anew.</summary>
        public void Reset(int piSeed)
        {
            Triggers.Clear();
            mORandom = new UWOriginalRandom(piSeed);
        }

        public bool TryGetTile(int piTileX, int piTileY, out int piType, out int piFloorNibble, out int piTerrain)
        {
            piType = 0;
            piFloorNibble = 0;
            piTerrain = 0;

            UWTile lOTile = mOLevel != null ? mOLevel.GetTile(piTileX, piTileY) : null;

            if (lOTile == null)
                return false;

            piType = (int)lOTile.TileType & 0xF;
            piFloorNibble = (lOTile.FloorHeight >> 4) & 0xF;

            if (mOTerrain != null)
                piTerrain = ((int)mOTerrain.GetFloorTerrain(lOTile.TextureFloor) & 0xFF) >> 4;

            return true;
        }

        public void GetBodiesInTile(int piTileX, int piTileY, List<UWMotionBody> pOInto)
        {
            UWTile lOTile = mOLevel != null ? mOLevel.GetTile(piTileX, piTileY) : null;

            if (lOTile == null || lOTile.ObjectsInTile == null)
                return;

            foreach (UWObject lOObject in lOTile.ObjectsInTile)
            {
                if (lOObject == null)
                    continue;

                int liIndex;

                if (!mOIndex.TryGetValue(lOObject, out liIndex))
                    liIndex = 0;

                bool lbCreature = lOObject is UWNpc;

                pOInto.Add(new UWMotionBody
                {
                    Index = liIndex,
                    ItemId = lOObject.ID,
                    XPos = lOObject.XPos,
                    YPos = lOObject.YPos,
                    ZPos = lOObject.ZPos,
                    IsMobile = lbCreature,
                    IsCreature = lbCreature,
                    Direction = lOObject.Heading & 7
                });
            }
        }

        public int Random()
        {
            return mORandom.Next();
        }

        public UWGameSession Session => UWGameSession.Current;

        public void UseOnMover(int piOtherIndex, int piMoverIndex, int piHitTileX, int piHitTileY)
        {
        }

        public int TriggerMove(int piMoverIndex, int piOtherIndex, int piHitTileX, int piHitTileY)
        {
            List<UWObject> lOMaster = mOLevel != null ? mOLevel.Masterlist : null;
            UWObject lOTrigger = lOMaster != null && piOtherIndex > 0 && piOtherIndex < lOMaster.Count ? lOMaster[piOtherIndex] : null;

            if (lOTrigger != null && lOTrigger.ID == UWObjectMechanics.MoveTriggerId && !Triggers.Contains(piOtherIndex))
                Triggers.Add(piOtherIndex);

            return 2;
        }

        public bool MissileHits(int piMoverIndex, int piOtherIndex, int piMoverTileX, int piMoverTileY)
        {
            return false;
        }

        public void PushObject(int piOtherIndex, int piHeading, int piSpeed, int piVz, int piHitTileX, int piHitTileY)
        {
        }

        public void PlaySoundAt(int piSound, int piX8, int piY8, int piVolume)
        {
        }
    }
}
