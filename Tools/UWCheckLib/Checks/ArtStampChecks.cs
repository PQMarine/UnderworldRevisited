using System.Collections.Generic;
using UWDataImport.Presentation;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.4 (2026-10-19): the modern parts keep their built pictures by a stamp
    /// (UWArtStamp: the colours' version and up to three numbers) instead of hand-kept version,
    /// size and switch fields. Pinned: equal stamps only when every part is equal, a slot is current
    /// only for its own stamp and while its picture lives, Set releases exactly the picture it
    /// replaces (not the same one twice), and the keyed cache answers per key and stamp.
    /// </summary>
    public static partial class SelfChecks
    {
        private sealed class ArtProbe
        {
            public bool Alive = true;
        }

        private static void fCheckArtStamp()
        {
            Check.Group("Art stamps (UWArtStamp, UWStampedSlot, UWStampedCache - cleanup step 5.4)");

            fExpectBool("stamp: equal when every part is", new UWArtStamp(3, 10, 20) == new UWArtStamp(3, 10, 20), true);
            fExpectBool("stamp: another colour version differs", new UWArtStamp(3, 10, 20) == new UWArtStamp(4, 10, 20), false);
            fExpectBool("stamp: another size differs", new UWArtStamp(3, 10, 20) != new UWArtStamp(3, 20, 10), true);
            fExpectBool("stamp: the third number counts", new UWArtStamp(3, 1, 2, 0) == new UWArtStamp(3, 1, 2, 1), false);

            List<ArtProbe> lOReleased = new List<ArtProbe>();
            UWStampedSlot<ArtProbe> lOSlot = new UWStampedSlot<ArtProbe>(pOArt =>
            {
                pOArt.Alive = false;
                lOReleased.Add(pOArt);
            }, pOArt => pOArt.Alive);

            UWArtStamp lOFirst = new UWArtStamp(1, 5);

            fExpectBool("slot: nothing is current at first", lOSlot.IsCurrent(lOFirst), false);

            ArtProbe lOA = lOSlot.Set(lOFirst, new ArtProbe());

            fExpectBool("slot: current for its own stamp", lOSlot.IsCurrent(lOFirst), true);
            fExpectBool("slot: not for another", lOSlot.IsCurrent(new UWArtStamp(2, 5)), false);

            lOSlot.Set(lOFirst, lOA);
            fExpectInt("slot: setting the same picture again releases nothing", lOReleased.Count, 0);

            ArtProbe lOB = lOSlot.Set(new UWArtStamp(2, 5), new ArtProbe());

            fExpectBool("slot: a new picture releases the old one", lOReleased.Count == 1 && lOReleased[0] == lOA && !lOA.Alive, true);
            fExpectBool("slot: and keeps the new", lOSlot.Art == lOB && lOB.Alive, true);

            lOB.Alive = false;
            fExpectBool("slot: a picture destroyed elsewhere is not current", lOSlot.IsCurrent(new UWArtStamp(2, 5)), false);

            lOSlot.Release();
            fExpectBool("slot: Release empties it", lOSlot.Art == null && !lOSlot.IsCurrent(new UWArtStamp(2, 5)), true);

            int liBuilt = 0;
            int liCacheReleased = 0;
            UWStampedCache<int, ArtProbe> lOCache = new UWStampedCache<int, ArtProbe>(pOArt => liCacheReleased++);

            lOCache.Get(7, new UWArtStamp(1), () => { liBuilt++; return new ArtProbe(); });
            lOCache.Get(7, new UWArtStamp(1), () => { liBuilt++; return new ArtProbe(); });
            fExpectInt("cache: a key is built once per stamp", liBuilt, 1);

            lOCache.Get(8, new UWArtStamp(1), () => { liBuilt++; return new ArtProbe(); });
            lOCache.Get(7, new UWArtStamp(2), () => { liBuilt++; return new ArtProbe(); });
            fExpectBool("cache: another key or a new stamp builds anew and releases only the stale one",
                liBuilt == 3 && liCacheReleased == 1 && lOCache.Count == 2, true);
            fExpectBool("cache: TryGet answers only for the stamp asked", lOCache.TryGet(8, new UWArtStamp(1), out _)
                && !lOCache.TryGet(8, new UWArtStamp(2), out _), true);

            lOCache.ReleaseAll();
            fExpectBool("cache: ReleaseAll releases everything", liCacheReleased == 3 && lOCache.Count == 0, true);
        }
    }
}
