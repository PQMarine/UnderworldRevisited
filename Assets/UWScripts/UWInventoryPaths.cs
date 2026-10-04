using System.Collections.Generic;
using UWDataImport.UWData;

/// <summary>
/// A carried piece's PLACE in the inventory as text, for what the modern scheme keeps with a save
/// game in UWR.json (UWHelpNotes): the action bar's slots and the open bags. The inventory's
/// structure survives saving and loading - the backpack's places, the equipment slots, the order
/// of a container's contents -, the pieces themselves are new objects after a load.
/// </summary>
public static class UWInventoryPaths
{
    /// <summary>
    /// A recorded piece found again, checked: the piece at the path counts only when it has the
    /// recorded object number. Otherwise - and when nothing was recorded but the file is older
    /// than the save (UWHelpNotes.IsStale) - the one carried piece of that number, if exactly one;
    /// else null. Never throws.
    /// </summary>
    public static UWObject ResolveChecked(UWInventoryModel pOModel, string psPath, int piId, bool pbStale,
        List<UWObject> pOMasterlist)
    {
        try
        {
            UWObject lOItem = Resolve(pOModel, psPath, pOMasterlist);

            if (piId < 0)
                return pbStale ? null : lOItem;

            if (lOItem != null && lOItem.ID == piId)
                return lOItem;

            UWObject lOFound = null;
            int liCount = 0;

            foreach (UWObject lOCarried in pOModel.EnumerateAll())
            {
                if (lOCarried.ID != piId)
                    continue;

                lOFound = lOCarried;
                liCount++;
            }

            return liCount == 1 ? lOFound : null;
        }
        catch (System.Exception lOError)
        {
            UnityEngine.Debug.LogWarning("[Help] inventory path \"" + psPath + "\" could not be resolved: " + lOError.Message);
            return null;
        }
    }

    /// <summary>"B3" backpack slot 3, "E7" equipment slot 7, then ".n" for the n-th thing inside.</summary>
    public static string PathOf(UWInventoryModel pOModel, UWObject pOItem)
    {
        if (pOItem == null)
            return null;

        for (int liSlot = 0; liSlot < pOModel.Backpack.Length; liSlot++)
        {
            string lsPath = fPathIn(pOModel.Backpack[liSlot], pOItem, "B" + liSlot, 0);

            if (lsPath != null)
                return lsPath;
        }

        for (int liSlot = 0; liSlot < pOModel.EquipSlots.Length; liSlot++)
        {
            string lsPath = fPathIn(pOModel.EquipSlots[liSlot], pOItem, "E" + liSlot, 0);

            if (lsPath != null)
                return lsPath;
        }

        return null;
    }

    private static string fPathIn(UWObject pOHere, UWObject pOItem, string psPath, int piDepth)
    {
        if (pOHere == null || piDepth > 16)
            return null;

        if (pOHere == pOItem)
            return psPath;

        if (pOHere.Contents == null)
            return null;

        for (int liAt = 0; liAt < pOHere.Contents.Count; liAt++)
        {
            string lsPath = fPathIn(pOHere.Contents[liAt], pOItem, psPath + "." + liAt, piDepth + 1);

            if (lsPath != null)
                return lsPath;
        }

        return null;
    }

    public static UWObject Resolve(UWInventoryModel pOModel, string psPath, List<UWObject> pOMasterlist)
    {
        if (string.IsNullOrEmpty(psPath) || psPath.Length < 2)
            return null;

        string[] lsParts = psPath.Substring(1).Split('.');

        if (!int.TryParse(lsParts[0], out int liSlot))
            return null;

        UWObject[] lOTop = psPath[0] == 'B' ? pOModel.Backpack : psPath[0] == 'E' ? pOModel.EquipSlots : null;

        if (lOTop == null || liSlot < 0 || liSlot >= lOTop.Length)
            return null;

        UWObject lOHere = lOTop[liSlot];

        for (int liPart = 1; liPart < lsParts.Length && lOHere != null; liPart++)
        {
            if (lOHere.Contents == null && pOMasterlist != null)
                lOHere.EnsureContentsLoaded(pOMasterlist);

            if (lOHere.Contents == null || !int.TryParse(lsParts[liPart], out int liAt)
                || liAt < 0 || liAt >= lOHere.Contents.Count)
                return null;

            lOHere = lOHere.Contents[liAt];
        }

        return lOHere;
    }
}
