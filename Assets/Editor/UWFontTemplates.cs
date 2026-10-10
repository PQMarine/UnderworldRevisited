using UnityEditor;
using UnityEngine;

/// <summary>
/// THE LINE HEIGHT TEMPLATES for fonts built at run time (UWUiFonts): a Font made in code has no
/// line height (Unity sets it only through the asset's serialized m_LineSpacing, and at run time it
/// stays 0, so every line of a Text lands on the first - tried 2026-10-09). These empty font
/// assets carry nothing but a line height of 1 to MaxLineHeight pixels and an ascent as high (the
/// glyphs then stand above the baseline like a normal font's - with an ascent of 0 every text hung
/// a line low and measured 0 high, per user's screenshots the same day);
/// UWUiFonts copies the one it needs and fills in the glyphs from the player's own FONT*.SYS, so no
/// original art is in them. Made once with this menu entry (or -executeMethod
/// UWFontTemplates.Create); they live in Resources/UWFontTemplates.
/// </summary>
public static class UWFontTemplates
{
    public const int MaxLineHeight = 96;

    private const string Folder = "Assets/Resources/UWFontTemplates";

    [MenuItem("Underworld Revisited/Fonts/Create line height templates")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Resources", "UWFontTemplates");

        for (int liHeight = 1; liHeight <= MaxLineHeight; liHeight++)
        {
            string lsPath = Folder + "/LineHeight" + liHeight + ".fontsettings";
            Font lOFont = AssetDatabase.LoadAssetAtPath<Font>(lsPath);

            if (lOFont == null)
            {
                lOFont = new Font("LineHeight" + liHeight);
                AssetDatabase.CreateAsset(lOFont, lsPath);
            }

            SerializedObject lOSerialized = new SerializedObject(lOFont);

            lOSerialized.FindProperty("m_LineSpacing").floatValue = liHeight;
            lOSerialized.FindProperty("m_Ascent").floatValue = liHeight;
            lOSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        Debug.Log("UWFontTemplates: " + MaxLineHeight + " line height templates in " + Folder);
    }
}
