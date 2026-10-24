using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// WHICH BAGS ARE OPEN in the modern scheme (cleanup 5.17, 2026-10-23: out of UWModernBags, as it
/// was): the containers whose windows are open, in the order they opened (a bag inside another one
/// is appended as well - the player chooses the order; per user, 2026-10-03), their number badges
/// (the lowest free number when one opens), their scrolled rows and the moment each opened, for
/// its window growing out of the slot (UWModernBagPages). THE OPEN CONTAINERS, their order and
/// numbers are kept with the save game (UWR.json through UWHelpNotes, as inventory paths) and are
/// open again after loading (Capture, Resolve).
/// </summary>
internal sealed class UWModernOpenBags
{
    public UWModernOpenBags(UWModernBags pOBags)
    {
        mOBags = pOBags;
    }

    private readonly UWModernBags mOBags;

    /// <summary>The containers whose windows are open, bottom first.</summary>
    private readonly List<UWObject> mOOpenBags = new List<UWObject>();

    /// <summary>The scrolled rows of each open bag.</summary>
    private readonly Dictionary<UWObject, int> mOScroll = new Dictionary<UWObject, int>();

    /// <summary>The number badge of each open bag.</summary>
    private readonly Dictionary<UWObject, int> mOBagNumbers = new Dictionary<UWObject, int>();

    /// <summary>When each bag opened (unscaled time), for its window growing out of the slot.</summary>
    private readonly Dictionary<UWObject, float> mOOpenedAt = new Dictionary<UWObject, float>();

    /// <summary>The load the open bags were last read from (UWHelpNotes.LoadCount), and the
    /// inventory load they were resolved against.</summary>
    private int miLoadCount = -1;

    private int miModelLoadCount = -1;

    private UWInventoryModel mOResolvedModel;

    /// <summary>The open containers, bottom first (the bags' windows are laid out in this order).</summary>
    internal List<UWObject> Bags => mOOpenBags;

    /// <summary>The scrolled rows of each open bag (the layout keeps them within the bag).</summary>
    internal Dictionary<UWObject, int> Scroll => mOScroll;

    /// <summary>The number badge of each open bag.</summary>
    internal Dictionary<UWObject, int> Numbers => mOBagNumbers;

    /// <summary>When each bag opened, until its window has grown out of its slot.</summary>
    internal Dictionary<UWObject, float> OpenedAt => mOOpenedAt;

    /// <summary>Every carried bag opens, in the backpack's order with a bag's inner bags after it -
    /// not the rune bag, whose shelf comes with the spells (as the click on the shut backpack
    /// leaves it). Passes, because an inner bag is found once its parent's contents are loaded
    /// (Toggle).</summary>
    internal void OpenAll()
    {
        UWInventory lOInventory = mOBags.Inventory;

        if (lOInventory == null)
            return;

        if (!mOBags.IsOpen)
            mOBags.Open();

        List<UWObject> lONew = new List<UWObject>();

        for (int liPass = 0; liPass < 8; liPass++)
        {
            lONew.Clear();

            foreach (UWObject lOItem in lOInventory.Model.EnumerateAll())
            {
                if (lOItem != null && lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers
                    && lOItem.ID != UWObjectMechanics.RuneBagId && !mOOpenBags.Contains(lOItem) && !lONew.Contains(lOItem))
                    lONew.Add(lOItem);
            }

            if (lONew.Count == 0)
                return;

            foreach (UWObject lOBag in lONew)
            {
                if (!mOOpenBags.Contains(lOBag))
                    Toggle(lOBag);
            }
        }
    }

    /// <summary>The open bags into UWHelpNotes for saving (UWHelpNotes.SaveTo): their paths in the
    /// order they opened, and their numbers. Not yet resolved since the last load: what was read
    /// stays.</summary>
    internal void Capture()
    {
        if (mOBags.Inventory == null || miLoadCount != UWHelpNotes.LoadCount)
            return;

        UWInventoryModel lOModel = mOBags.Inventory.Model;
        List<string> lOPaths = new List<string>();
        List<int> lONumbers = new List<int>();
        List<int> lOIds = new List<int>();

        foreach (UWObject lOBag in mOOpenBags)
        {
            string lsPath = UWInventoryPaths.PathOf(lOModel, lOBag);

            if (string.IsNullOrEmpty(lsPath))
                continue;

            lOPaths.Add(lsPath);
            lONumbers.Add(mOBagNumbers.TryGetValue(lOBag, out int liNumber) ? liNumber : 0);
            lOIds.Add(lOBag.ID);
        }

        UWHelpNotes.OpenBagPaths = lOPaths.ToArray();
        UWHelpNotes.OpenBagNumbers = lONumbers.ToArray();
        UWHelpNotes.OpenBagIds = lOIds.ToArray();
    }

    /// <summary>After a load or a new character: the open bags read back once - for a save game
    /// only after its inventory has come in. They show when the bags open.</summary>
    internal void Resolve()
    {
        UWInventory lOInventory = mOBags.Inventory;

        if (miLoadCount == UWHelpNotes.LoadCount || lOInventory == null)
            return;

        UWInventoryModel lOModel = lOInventory.Model;
        string[] lsPaths = UWHelpNotes.OpenBagPaths ?? new string[0];

        if (lsPaths.Length > 0 && lOModel == mOResolvedModel && lOModel.LoadCount == miModelLoadCount)
            return;

        miLoadCount = UWHelpNotes.LoadCount;
        mOResolvedModel = lOModel;
        miModelLoadCount = lOModel.LoadCount;

        mOOpenBags.Clear();
        mOScroll.Clear();
        mOBagNumbers.Clear();
        mOOpenedAt.Clear();

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        List<UWObject> lOMasterlist = lOLoader != null && lOLoader.CurrentLevel != null ? lOLoader.CurrentLevel.Masterlist : null;
        int[] liNumbers = UWHelpNotes.OpenBagNumbers ?? new int[0];

        for (int liAt = 0; liAt < lsPaths.Length; liAt++)
        {
            UWObject lOBag = UWInventoryPaths.ResolveChecked(lOModel, lsPaths[liAt],
                UWHelpNotes.IdAt(UWHelpNotes.OpenBagIds, liAt), UWHelpNotes.IsStale, lOMasterlist);

            if (lOBag == null || lOBag.GetCategory() != UWObject.ObjectCategoryEnum.Containers || mOOpenBags.Contains(lOBag))
                continue;

            if (lOBag.Contents == null && lOMasterlist != null)
                lOBag.EnsureContentsLoaded(lOMasterlist);

            if (lOBag.Contents == null)
                continue;

            // A sensible number not taken yet, else the lowest free one.
            int liNumber = liAt < liNumbers.Length && liNumbers[liAt] > 0 && liNumbers[liAt] <= 99
                && !mOBagNumbers.ContainsValue(liNumbers[liAt])
                ? liNumbers[liAt] : fFreeNumber();

            mOOpenBags.Add(lOBag);
            mOScroll[lOBag] = 0;
            mOBagNumbers[lOBag] = liNumber;
        }
    }

    // ------------------------------------------------- The open bags

    internal void Toggle(UWObject pOContainer)
    {
        // The original's page opens one container at a time in its slots, as the original
        // (UWInventoryModel.TryOpenContainer: again on the open one closes it).
        if (UWModernBagPages.IsPageShown)
        {
            UWInventory lOInventory = mOBags.Inventory;

            if (lOInventory != null && lOInventory.Model != null)
                lOInventory.Model.TryOpenContainer(pOContainer);

            return;
        }

        if (mOOpenBags.Contains(pOContainer))
        {
            Close(pOContainer);
            return;
        }

        DataImport lOData = mOBags.Data;
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (pOContainer.Contents == null && lOLoader != null && lOLoader.CurrentLevel != null)
            pOContainer.EnsureContentsLoaded(lOLoader.CurrentLevel.Masterlist);

        if (pOContainer.Contents == null || lOData == null)
            return;

        // From the character panel with the bags shut: they open with it, or nothing would show.
        if (!mOBags.IsOpen)
            mOBags.Open();

        mOOpenBags.Add(pOContainer);
        mOScroll[pOContainer] = 0;
        mOBagNumbers[pOContainer] = fFreeNumber();
        mOOpenedAt[pOContainer] = Time.unscaledTime;
    }

    /// <summary>The lowest number no open bag carries.</summary>
    private int fFreeNumber()
    {
        int liNumber = 1;

        while (mOBagNumbers.ContainsValue(liNumber))
            liNumber++;

        return liNumber;
    }

    private void fForgetBag(int piAt)
    {
        UWObject lOBag = mOOpenBags[piAt];

        mOOpenBags.RemoveAt(piAt);
        mOScroll.Remove(lOBag);
        mOBagNumbers.Remove(lOBag);
        mOOpenedAt.Remove(lOBag);
    }

    /// <summary>Closes a bag and every open bag inside it.</summary>
    internal void Close(UWObject pOContainer)
    {
        for (int liAt = mOOpenBags.Count - 1; liAt >= 0; liAt--)
        {
            if (mOOpenBags[liAt] == pOContainer || fIsInside(mOOpenBags[liAt], pOContainer))
                fForgetBag(liAt);
        }
    }

    /// <summary>Closes the windows of bags that are no longer carried (taken onto the pointer,
    /// dropped, used up) or are no longer containers - the bowl the stew recipe turns into
    /// rotworm stew (UWStewRules.TryMix; per user 2026-10-13 its window stayed open, empty, in
    /// the modern scheme, where several bags can be open; the classic scheme's one open
    /// container is closed by TryMix itself).</summary>
    internal void Prune()
    {
        for (int liAt = mOOpenBags.Count - 1; liAt >= 0; liAt--)
        {
            UWObject lOBag = mOOpenBags[liAt];

            if (!fIsCarried(lOBag) || lOBag.GetCategory() != UWObject.ObjectCategoryEnum.Containers)
                fForgetBag(liAt);
        }
    }

    private bool fIsCarried(UWObject pOItem)
    {
        UWInventory lOInventory = mOBags.Inventory;

        return lOInventory != null && lOInventory.Model.IsCarried(pOItem);
    }

    private static bool fIsInside(UWObject pOItem, UWObject pOContainer)
    {
        return fContains(pOContainer, pOItem, 0);
    }

    private static bool fContains(UWObject pOContainer, UWObject pOItem, int piDepth)
    {
        if (pOContainer?.Contents == null || piDepth > 16)
            return false;

        foreach (UWObject lOInside in pOContainer.Contents)
        {
            if (lOInside == pOItem || fContains(lOInside, pOItem, piDepth + 1))
                return true;
        }

        return false;
    }
}
