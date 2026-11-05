using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UWDataImport.Install;
using UWDataImport.UWData;

/// <summary>
/// The game's manual in the help window (decided per user, 2026-09-25): the GOG version
/// brings it as a PDF next to game.gog; nothing of it is shipped with this project. PDFium
/// (UWPdfium) renders a page straight at the size it is shown, 3 to 7 ms a page (measured
/// 2026-09-26), so there is no image cache on disk - text rendered for its real size stays
/// sharper than a scaled picture. The last few pages rendered are kept as textures.
///
/// The PDF sets its text in fonts it does not embed (Palatino, Garamond and others), so
/// PDFium substitutes similar ones; a probe render on 2026-09-26 read well.
///
/// SEVERAL DOCUMENTS (per user, 2026-11-05, with the Steam version's extras): the running game's PDFs that
/// UWGameDocuments finds - manual, reference card, map, its own book, cluebook - one open at a time
/// (Select); the help's Manual tab shows a button for each. Found anew when the game changes.
/// </summary>
public static class UWManual
{
    /// <summary>The name the GOG version gives the manual.</summary>
    public const string FileName = UWGameDocuments.GogManualName;

    /// <summary>The running game's documents (null until first asked), for which game, and the one open.</summary>
    private static List<UWGameDocuments.Document> mODocuments;

    private static IUWGameProfile mOFoundFor;

    private static int miSelected;

    /// <summary>The running game's documents, in the help's order; empty when there is none.</summary>
    public static IReadOnlyList<UWGameDocuments.Document> Documents
    {
        get
        {
            fEnsureDocuments();

            return mODocuments;
        }
    }

    /// <summary>The document shown, an index into Documents.</summary>
    public static int Selected => miSelected;

    /// <summary>Shows another document: the open one is closed, the next opens with EnsureOpen.</summary>
    public static void Select(int piIndex)
    {
        fEnsureDocuments();

        if (piIndex == miSelected || piIndex < 0 || piIndex >= mODocuments.Count)
            return;

        fCloseDocument();
        miSelected = piIndex;
        mbTried = false;
    }

    private static void fEnsureDocuments()
    {
        IUWGameProfile lOGame = UnderworldRevisited.UWSettings.Game;

        if (mODocuments != null && lOGame == mOFoundFor)
            return;

        fCloseDocument();
        mbTried = false;
        miSelected = 0;
        mOFoundFor = lOGame;
        mODocuments = UWGameDocuments.Find(fFolders(), lOGame == UWGameProfiles.Uw2 ? 2 : 1);
    }

    /// <summary>How many rendered pages are kept - a spread in the reading mode and the side
    /// view, with room for turning back and forth.</summary>
    private const int KeptPages = 8;

    private static bool mbTried;

    private static bool mbLibraryUp;

    private static IntPtr mODocument = IntPtr.Zero;

    private static Vector2[] mOPageSizes = new Vector2[0];

    private class RenderedPage
    {
        public int Index;
        public int Width;
        public int Height;
        public Texture2D Texture;
    }

    /// <summary>Most recently used first.</summary>
    private static readonly List<RenderedPage> mORendered = new List<RenderedPage>();

    /// <summary>Why the manual cannot be shown, or null.</summary>
    public static string Problem { get; private set; }

    public static int PageCount => mOPageSizes.Length;

    public static bool IsOpen => mODocument != IntPtr.Zero;

    /// <summary>Opens the manual on first use; afterwards only answers whether it is open.
    /// </summary>
    public static bool EnsureOpen()
    {
        if (mbTried)
            return IsOpen;

        mbTried = true;

        fEnsureDocuments();

        string lsPath = miSelected < mODocuments.Count ? mODocuments[miSelected].Path : null;

        if (lsPath == null)
        {
            Problem = UWText.F("help.manual.not_found", FileName);

            return false;
        }

        try
        {
            if (!mbLibraryUp)
            {
                UWPdfium.FPDF_InitLibrary();
                mbLibraryUp = true;
                Application.quitting += Close;
            }

            mODocument = UWPdfium.FPDF_LoadDocument(System.Text.Encoding.UTF8.GetBytes(lsPath + "\0"), null);

            if (mODocument == IntPtr.Zero)
            {
                Problem = UWText.F("help.manual.not_opened", lsPath);

                return false;
            }

            int liCount = UWPdfium.FPDF_GetPageCount(mODocument);

            mOPageSizes = new Vector2[Mathf.Max(0, liCount)];

            for (int liAt = 0; liAt < mOPageSizes.Length; liAt++)
            {
                IntPtr lOPage = UWPdfium.FPDF_LoadPage(mODocument, liAt);

                if (lOPage == IntPtr.Zero)
                {
                    mOPageSizes[liAt] = new Vector2(396f, 612f);
                    continue;
                }

                mOPageSizes[liAt] = new Vector2(UWPdfium.FPDF_GetPageWidthF(lOPage), UWPdfium.FPDF_GetPageHeightF(lOPage));
                UWPdfium.FPDF_ClosePage(lOPage);
            }

            return true;
        }
        catch (Exception lOError) when (lOError is DllNotFoundException || lOError is EntryPointNotFoundException
            || lOError is BadImageFormatException)
        {
            Problem = UWText.T("help.manual.unavailable");
            Debug.LogWarning("[Manual] PDFium could not be loaded: " + lOError.Message);
            mbLibraryUp = false;

            return false;
        }
    }

    /// <summary>The page's size in points (1/72 inch).</summary>
    public static Vector2 GetPageSize(int piIndex)
    {
        return piIndex >= 0 && piIndex < mOPageSizes.Length ? mOPageSizes[piIndex] : new Vector2(396f, 612f);
    }

    /// <summary>The page rendered at exactly this many pixels, or null.</summary>
    public static Texture2D GetPage(int piIndex, int piWidth, int piHeight)
    {
        if (!IsOpen || piIndex < 0 || piIndex >= PageCount || piWidth <= 0 || piHeight <= 0)
            return null;

        for (int liAt = 0; liAt < mORendered.Count; liAt++)
        {
            RenderedPage lOKept = mORendered[liAt];

            if (lOKept.Index != piIndex || lOKept.Width != piWidth || lOKept.Height != piHeight || lOKept.Texture == null)
                continue;

            mORendered.RemoveAt(liAt);
            mORendered.Insert(0, lOKept);

            return lOKept.Texture;
        }

        Texture2D lOTexture = fRender(piIndex, piWidth, piHeight);

        if (lOTexture == null)
            return null;

        mORendered.Insert(0, new RenderedPage { Index = piIndex, Width = piWidth, Height = piHeight, Texture = lOTexture });

        while (mORendered.Count > KeptPages)
        {
            UnityEngine.Object.Destroy(mORendered[mORendered.Count - 1].Texture);
            mORendered.RemoveAt(mORendered.Count - 1);
        }

        return lOTexture;
    }

    private static Texture2D fRender(int piIndex, int piWidth, int piHeight)
    {
        IntPtr lOPage = UWPdfium.FPDF_LoadPage(mODocument, piIndex);

        if (lOPage == IntPtr.Zero)
            return null;

        IntPtr lOBitmap = UWPdfium.FPDFBitmap_Create(piWidth, piHeight, 0);

        if (lOBitmap == IntPtr.Zero)
        {
            UWPdfium.FPDF_ClosePage(lOPage);
            return null;
        }

        UWPdfium.FPDFBitmap_FillRect(lOBitmap, 0, 0, piWidth, piHeight, 0xFFFFFFFF);
        UWPdfium.FPDF_RenderPageBitmap(lOBitmap, lOPage, 0, 0, piWidth, piHeight, 0, UWPdfium.RenderAnnotations);

        int liStride = UWPdfium.FPDFBitmap_GetStride(lOBitmap);
        byte[] lbSource = new byte[liStride * piHeight];

        Marshal.Copy(UWPdfium.FPDFBitmap_GetBuffer(lOBitmap), lbSource, 0, lbSource.Length);

        UWPdfium.FPDFBitmap_Destroy(lOBitmap);
        UWPdfium.FPDF_ClosePage(lOPage);

        // PDFium writes BGRx top down; the texture wants its rows bottom up, and the fourth
        // byte is left undefined without an alpha channel.
        byte[] lbPixels = new byte[piWidth * piHeight * 4];

        for (int liRow = 0; liRow < piHeight; liRow++)
        {
            int liFrom = liRow * liStride;
            int liTo = (piHeight - 1 - liRow) * piWidth * 4;

            Buffer.BlockCopy(lbSource, liFrom, lbPixels, liTo, piWidth * 4);

            for (int liAlpha = liTo + 3; liAlpha < liTo + (piWidth * 4); liAlpha += 4)
                lbPixels[liAlpha] = 255;
        }

        Texture2D lOTexture = new Texture2D(piWidth, piHeight, TextureFormat.BGRA32, false)
        {
            name = "UWManual page " + (piIndex + 1),
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        lOTexture.LoadRawTextureData(lbPixels);
        lOTexture.Apply(false, true);

        return lOTexture;
    }

    /// <summary>
    /// Where the documents are looked for: the image of this session first, then the installation folder
    /// from the settings, then the usual GOG and Steam folders (UWGameDocuments adds each one's Extras).
    /// </summary>
    private static List<string> fFolders()
    {
        List<string> lOFolders = new List<string>();

        if (!string.IsNullOrEmpty(UnderworldRevisited.UWSettings.GogImagePath))
            lOFolders.Add(Path.GetDirectoryName(UnderworldRevisited.UWSettings.GogImagePath));

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
        string lsInstall = lOSettings != null && !string.IsNullOrEmpty(lOSettings.GogInstallPath)
            ? lOSettings.GogInstallPath : UWUserSettings.GogInstallPath;

        if (!string.IsNullOrEmpty(lsInstall))
            lOFolders.Add(File.Exists(lsInstall) ? Path.GetDirectoryName(lsInstall) : lsInstall);

        if (!string.IsNullOrEmpty(UWUserSettings.GogInstallPathUw2))
            lOFolders.Add(File.Exists(UWUserSettings.GogInstallPathUw2) ? Path.GetDirectoryName(UWUserSettings.GogInstallPathUw2) : UWUserSettings.GogInstallPathUw2);

        lOFolders.AddRange(UnderworldRevisited.UWSettings.GetCandidateGogPaths());

        return lOFolders;
    }

    /// <summary>The open document and its pages go; the library stays up.</summary>
    private static void fCloseDocument()
    {
        foreach (RenderedPage lOKept in mORendered)
        {
            if (lOKept.Texture != null)
                UnityEngine.Object.Destroy(lOKept.Texture);
        }

        mORendered.Clear();

        if (mODocument != IntPtr.Zero)
            UWPdfium.FPDF_CloseDocument(mODocument);

        mODocument = IntPtr.Zero;
        mOPageSizes = new Vector2[0];
        Problem = null;
    }

    /// <summary>Closes the document and the library - at the end of the game (and of play mode
    /// in the editor, where the native library outlives the scripts).</summary>
    public static void Close()
    {
        fCloseDocument();

        if (mbLibraryUp)
            UWPdfium.FPDF_DestroyLibrary();

        mbLibraryUp = false;
        mbTried = false;
        mOPageSizes = new Vector2[0];
        Application.quitting -= Close;
    }
}
