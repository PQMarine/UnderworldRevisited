using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The motion core's small map (IUWMotionWorld) and the step targets of the motion stepper.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>A 32 x 32 map: solid border, open floor of height 2 inside, and whatever a
        /// check paints onto it. Bodies per tile, a host that only remembers.</summary>
        private sealed class FakeMotionWorld : IUWMotionWorld, IUWMobileObjectHost
        {
            public const int Size = 32;

            public readonly int[,] Type = new int[Size, Size];

            public readonly int[,] Floor = new int[Size, Size];

            public readonly int[,] Terrain = new int[Size, Size];

            private readonly Dictionary<int, List<UWMotionBody>> mOBodies = new Dictionary<int, List<UWMotionBody>>();

            public UWOriginalRandom Rng = new UWOriginalRandom(1234);

            public int Level = 1;

            public int Phase;

            public readonly List<string> Log = new List<string>();

            public int Splashes;

            public int Removed;

            public int Rested;

            public int RestTileX;

            public int RestTileY;

            public int RestXPos;

            public int RestYPos;

            public int RestZPos;

            public int RestOctant;

            public int RestQuality;

            public int LandingSounds;

            public int Pushes;

            public FakeMotionWorld()
            {
                for (int liY = 0; liY < Size; liY++)
                {
                    for (int liX = 0; liX < Size; liX++)
                    {
                        bool lbBorder = liX == 0 || liY == 0 || liX == Size - 1 || liY == Size - 1;

                        Type[liX, liY] = lbBorder ? 0 : 1;
                        Floor[liX, liY] = 2;
                    }
                }
            }

            public void Paint(int piX0, int piY0, int piX1, int piY1, int piType, int piFloor, int piTerrain)
            {
                for (int liY = piY0; liY <= piY1; liY++)
                {
                    for (int liX = piX0; liX <= piX1; liX++)
                    {
                        Type[liX, liY] = piType;
                        Floor[liX, liY] = piFloor;
                        Terrain[liX, liY] = piTerrain;
                    }
                }
            }

            public void AddBody(int piTileX, int piTileY, UWMotionBody pOBody)
            {
                int liKey = (piTileY * Size) + piTileX;

                if (!mOBodies.ContainsKey(liKey))
                    mOBodies[liKey] = new List<UWMotionBody>();

                mOBodies[liKey].Add(pOBody);
            }

            public bool TryGetTile(int piTileX, int piTileY, out int piType, out int piFloorNibble, out int piTerrain)
            {
                piType = 0;
                piFloorNibble = 0;
                piTerrain = 0;

                if (piTileX < 0 || piTileY < 0 || piTileX >= Size || piTileY >= Size)
                    return false;

                piType = Type[piTileX, piTileY];
                piFloorNibble = Floor[piTileX, piTileY];
                piTerrain = Terrain[piTileX, piTileY];

                return true;
            }

            public void GetBodiesInTile(int piTileX, int piTileY, List<UWMotionBody> pOInto)
            {
                List<UWMotionBody> lOList;

                if (piTileX >= 0 && piTileY >= 0 && piTileX < Size && piTileY < Size
                    && mOBodies.TryGetValue((piTileY * Size) + piTileX, out lOList))
                    pOInto.AddRange(lOList);
            }

            public int Random()
            {
                return Rng.Next();
            }

            public void UseOnMover(int piOtherIndex, int piMoverIndex, int piHitTileX, int piHitTileY)
            {
                Log.Add("use " + piOtherIndex + " on " + piMoverIndex);
            }

            public int TriggerMove(int piMoverIndex, int piOtherIndex, int piHitTileX, int piHitTileY)
            {
                Log.Add("trigger " + piOtherIndex);

                return 2;
            }

            public bool MissileHits(int piMoverIndex, int piOtherIndex, int piMoverTileX, int piMoverTileY)
            {
                Log.Add("missile " + piMoverIndex + " hits " + piOtherIndex);

                return false;
            }

            public void PushObject(int piOtherIndex, int piHeading, int piSpeed, int piVz, int piHitTileX, int piHitTileY)
            {
                Pushes++;
                Log.Add("push " + piOtherIndex + " heading " + piHeading + " vz " + piVz);
            }

            public void PlaySoundAt(int piSound, int piX8, int piY8, int piVolume)
            {
                if (piSound == 0x0F)
                    LandingSounds++;

                Log.Add("sound " + piSound);
            }

            public int DungeonLevel => Level;

            public int ClockPhase => Phase;

            public void MoveToTile(UWMobileRecord pORecord, int piOldTileX, int piOldTileY, int piNewTileX, int piNewTileY)
            {
            }

            public bool DamageSelf(UWMobileRecord pORecord, int piDamage, int piType)
            {
                Log.Add("damage " + piDamage + " type " + piType);

                return false;
            }

            public void PlaySoundAtObject(int piSound, UWMobileRecord pORecord, int piVolume)
            {
                Log.Add("sound at object " + piSound);
            }

            public void Splash(UWMobileRecord pORecord)
            {
                Splashes++;
            }

            public bool TalismanBurns(UWMobileRecord pORecord, int piTileX, int piTileY)
            {
                return false;
            }

            public void OnDemoted(UWMobileRecord pORecord)
            {
            }

            public int Detonate(UWMobileRecord pORecord, int piTileX, int piTileY, int piOwner)
            {
                Log.Add("detonate at " + piTileX + "/" + piTileY);

                return 0;
            }

            public void Remove(UWMobileRecord pORecord)
            {
                Removed++;
            }

            public void PlaceAtRest(UWMobileRecord pORecord, int piOctant, int piQuality)
            {
                Rested++;
                RestTileX = pORecord.TileX;
                RestTileY = pORecord.TileY;
                RestXPos = pORecord.XPos;
                RestYPos = pORecord.YPos;
                RestZPos = pORecord.ZPos;
                RestOctant = piOctant;
                RestQuality = piQuality;
            }
        }

        /// <summary>Records what the stepper asks of the host.</summary>
        private sealed class RecordingStepTarget : IUWMotionStepTarget
        {
            public readonly List<string> Calls = new List<string>();

            public readonly List<int> Dts = new List<int>();

            public readonly List<UWPlayerMotion.Command> Commands = new List<UWPlayerMotion.Command>();

            public void BeforeStep()
            {
                Calls.Add("before");
            }

            public void RunFrame(int piDt, UWPlayerMotion.Command peCommand)
            {
                Calls.Add("frame");
                Dts.Add(piDt);
                Commands.Add(peCommand);
            }

            public int TotalDt
            {
                get
                {
                    int liSum = 0;

                    foreach (int liDt in Dts)
                        liSum += liDt;

                    return liSum;
                }
            }

            public int Count(UWPlayerMotion.Command peCommand)
            {
                int liCount = 0;

                foreach (UWPlayerMotion.Command leCommand in Commands)
                    if (leCommand == peCommand)
                        liCount++;

                return liCount;
            }
        }

        /// <summary>The real motion behind the stepper, as UWPlayerMovement runs it.</summary>
        private sealed class MotionStepTarget : IUWMotionStepTarget
        {
            private readonly UWPlayerMotion mOMotion;

            public int Ran;

            public MotionStepTarget(UWPlayerMotion pOMotion)
            {
                mOMotion = pOMotion;
            }

            public void BeforeStep()
            {
            }

            public void RunFrame(int piDt, UWPlayerMotion.Command peCommand)
            {
                UWPlayerMotion.Command leHeld = mOMotion.MotionCommand;

                mOMotion.MotionCommand = peCommand;

                if (mOMotion.Frame(piDt))
                    Ran++;

                mOMotion.MotionCommand = leHeld;
            }
        }
    }
}
