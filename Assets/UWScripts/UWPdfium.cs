using System;
using System.Runtime.InteropServices;

/// <summary>
/// The few PDFium functions the manual needs (decided per user, 2026-09-25/26): the library
/// renders the manual of the GOG version for the help window (UWManual). PDFium comes as a
/// native library from bblanchon/pdfium-binaries (release chromium/8066), under
/// Assets/Plugins/PDFium; its licences are in ThirdParty/PDFium and THIRD_PARTY_NOTICES.md.
///
/// The signatures follow include/fpdfview.h of that release. On x64 there is one calling
/// convention, so the default of DllImport fits. "pdfium" resolves to pdfium.dll,
/// libpdfium.so and libpdfium.dylib. In the project: pdfium.dll for Windows and, since
/// 2026-10-06, libpdfium.so for Linux, both x64 and of the same release; no macOS library yet.
/// </summary>
internal static class UWPdfium
{
    private const string Library = "pdfium";

    /// <summary>FPDF_ANNOT: draw the page's annotations too.</summary>
    public const int RenderAnnotations = 0x01;

    [DllImport(Library)]
    public static extern void FPDF_InitLibrary();

    [DllImport(Library)]
    public static extern void FPDF_DestroyLibrary();

    /// <summary>The path is UTF-8 and zero-terminated (fpdfview.h).</summary>
    [DllImport(Library)]
    public static extern IntPtr FPDF_LoadDocument(byte[] pbPath, string psPassword);

    [DllImport(Library)]
    public static extern void FPDF_CloseDocument(IntPtr pODocument);

    [DllImport(Library)]
    public static extern int FPDF_GetPageCount(IntPtr pODocument);

    [DllImport(Library)]
    public static extern IntPtr FPDF_LoadPage(IntPtr pODocument, int piIndex);

    [DllImport(Library)]
    public static extern void FPDF_ClosePage(IntPtr pOPage);

    [DllImport(Library)]
    public static extern float FPDF_GetPageWidthF(IntPtr pOPage);

    [DllImport(Library)]
    public static extern float FPDF_GetPageHeightF(IntPtr pOPage);

    [DllImport(Library)]
    public static extern IntPtr FPDFBitmap_Create(int piWidth, int piHeight, int piAlpha);

    [DllImport(Library)]
    public static extern int FPDFBitmap_FillRect(IntPtr pOBitmap, int piLeft, int piTop, int piWidth, int piHeight,
        uint piColour);

    [DllImport(Library)]
    public static extern void FPDF_RenderPageBitmap(IntPtr pOBitmap, IntPtr pOPage, int piStartX, int piStartY,
        int piSizeX, int piSizeY, int piRotate, int piFlags);

    [DllImport(Library)]
    public static extern IntPtr FPDFBitmap_GetBuffer(IntPtr pOBitmap);

    [DllImport(Library)]
    public static extern int FPDFBitmap_GetStride(IntPtr pOBitmap);

    [DllImport(Library)]
    public static extern void FPDFBitmap_Destroy(IntPtr pOBitmap);
}
