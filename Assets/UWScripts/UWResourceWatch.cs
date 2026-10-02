using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reports when graphics resources pile up.
///
/// REASON (per user, 2026-09-13): after playing for a long time the screen went dark, torch and
/// inventory disappeared, and the log was full of "Resource ID out of range in GetResource
/// ... (max is 1048575)" - Unity had run out of IDs for graphics resources because
/// somewhere textures or meshes keep being created that are never released. Two places
/// are fixed (weapon image and flasks in UWCharacter); whether there are more is shown by this
/// watch.
///
/// A count is taken every 20 seconds. A message is only issued when a kind has grown by more than
/// MinGrowth - then with the most frequent texture sizes, because the created
/// textures usually carry no name, but their size reveals where they come from.
/// </summary>
public class UWResourceWatch : MonoBehaviour
{
    private const float Interval = 20f;

    private const int MinGrowth = 200;

    private float mfNext;

    private int miTextures = -1;

    private int miMeshes = -1;

    private int miRenderTextures = -1;

    private void Update()
    {
        if (Time.unscaledTime < mfNext)
            return;

        mfNext = Time.unscaledTime + Interval;

        Texture2D[] lOTextures = Resources.FindObjectsOfTypeAll<Texture2D>();
        int liMeshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;
        int liRenderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>().Length;

        bool lbFirst = miTextures < 0;
        bool lbGrew = !lbFirst && (lOTextures.Length - miTextures > MinGrowth
            || liMeshes - miMeshes > MinGrowth || liRenderTextures - miRenderTextures > MinGrowth);

        if (lbGrew)
        {
            Dictionary<string, int> lOBySize = new Dictionary<string, int>();

            foreach (Texture2D lOTexture in lOTextures)
            {
                string lsKey = (string.IsNullOrEmpty(lOTexture.name) ? "(unnamed)" : lOTexture.name)
                    + " " + lOTexture.width + "x" + lOTexture.height;
                int liCount;

                lOBySize.TryGetValue(lsKey, out liCount);
                lOBySize[lsKey] = liCount + 1;
            }

            List<KeyValuePair<string, int>> lOSorted = new List<KeyValuePair<string, int>>(lOBySize);

            lOSorted.Sort((lOA, lOB) => lOB.Value.CompareTo(lOA.Value));

            System.Text.StringBuilder lOText = new System.Text.StringBuilder();

            lOText.Append("Resources growing: textures ").Append(miTextures).Append(" -> ").Append(lOTextures.Length)
                .Append(", Meshes ").Append(miMeshes).Append(" -> ").Append(liMeshes)
                .Append(", RenderTextures ").Append(miRenderTextures).Append(" -> ").Append(liRenderTextures)
                .Append(". Most frequent textures:");

            for (int liAt = 0; liAt < lOSorted.Count && liAt < 8; liAt++)
                lOText.Append(" [").Append(lOSorted[liAt].Key).Append(": ").Append(lOSorted[liAt].Value).Append("]");

            Debug.LogWarning(lOText.ToString());
        }

        miTextures = lOTextures.Length;
        miMeshes = liMeshes;
        miRenderTextures = liRenderTextures;
    }
}
