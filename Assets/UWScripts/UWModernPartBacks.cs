using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE BACKS BEHIND THE PARTS (per user, 2026-10-09: "the back behind" for the parts with art of
/// their own): the heading's strip, the vitals' shelf, the messages and the active spells keep
/// their art, a generated back (UWModernBacks) lies under it, the part's rect and a few of its
/// pixels more all round. For the messages it stands in the dark box's place. The compass draws
/// its own (UWModernCompass); the leather parts take theirs in the leather's place. The power
/// gem's stand joined on 2026-10-10 (per user: the background could be chosen for the then
/// movement arrows but did not show), the flasks apart the same day; the stone shelf, a deco
/// panel itself, offers none (UWModernLayoutEditor). Out of UWModernHud since cleanup 5.16
/// (2026-10-22).
/// </summary>
public class UWModernPartBacks : UWModernPart
{
    private static readonly UWModernLayout.ElementEnum[] mePartsWithBackBehind =
    {
        UWModernLayout.ElementEnum.Heading, UWModernLayout.ElementEnum.Vitals, UWModernLayout.ElementEnum.Messages,
        UWModernLayout.ElementEnum.Spells, UWModernLayout.ElementEnum.PowerGem,
        UWModernLayout.ElementEnum.HealthFlask, UWModernLayout.ElementEnum.ManaFlask, UWModernLayout.ElementEnum.RuneHollow
    };

    /// <summary>The room around a part's rect, in its own pixels.</summary>
    private const int PartBackPad = 3;

    private readonly RawImage[] mOPartBacks = new RawImage[8];

    /// <summary>Each back built for its kind, size and the backs' and colours' version (a string
    /// key per frame until 2026-10-19).</summary>
    private readonly UWVersionedTexture[] mOPartBackTextures =
    {
        new UWVersionedTexture(), new UWVersionedTexture(), new UWVersionedTexture(), new UWVersionedTexture(),
        new UWVersionedTexture(), new UWVersionedTexture(), new UWVersionedTexture(), new UWVersionedTexture()
    };

    /// <summary>The messages, whose dark box the back takes the place of.</summary>
    private readonly UWModernMessageBox mOMessages;

    public UWModernPartBacks(UWModernHud pOHud, UWModernMessageBox pOMessages) : base(pOHud)
    {
        mOMessages = pOMessages;
    }

    /// <summary>The backs behind the parts drawn on the HUD's canvas, before their art so they lie
    /// under it.</summary>
    public override void Build(Transform pORoot)
    {
        for (int liAt = 0; liAt < mePartsWithBackBehind.Length; liAt++)
        {
            mOPartBacks[liAt] = UWUiKit.CreateRawImage(pORoot, "Back " + mePartsWithBackBehind[liAt], Vector2.zero, Vector2.zero);
            UWPixelArtUI.Apply(mOPartBacks[liAt]);
        }
    }

    /// <summary>The backs behind the heading, the vitals, the messages and the spells (see
    /// mePartsWithBackBehind): placed on the parts' rects of this frame, rebuilt when the size, the
    /// back or the colours change.</summary>
    protected override void fUpdate()
    {
        // Classic Wide: the frame is the back of everything.
        bool lbNone = UWModernClassicFrame.IsActive;

        for (int liAt = 0; liAt < mePartsWithBackBehind.Length; liAt++)
        {
            if (lbNone)
            {
                mOPartBacks[liAt].enabled = false;
                continue;
            }

            UWModernLayout.ElementEnum leElement = mePartsWithBackBehind[liAt];
            RawImage lOImage = mOPartBacks[liAt];
            UWModernBacks.Back lOBack = UWModernBacks.Get(leElement);
            Rect lORect = Rect.zero;
            bool lbShown = lOBack.Shape != UWDataImport.UWData.UWBackdropArt.ShapeEnum.None
                && UWModernLayout.TryGetRect(leElement, out lORect);

            if (leElement == UWModernLayout.ElementEnum.Messages && lbShown)
            {
                // In the dark box's place: shown when the box would be, or under the scroll.
                lbShown = mOMessages.IsBoxShown || UWModernLayout.IsScrollShown;
                mOMessages.HideBox();
            }

            if (!lbShown)
            {
                lOImage.enabled = false;
                continue;
            }

            float lfScale = Mathf.Max(1f, fScaleOf(leElement));
            int liWidth = Mathf.CeilToInt(lORect.width / lfScale) + (2 * PartBackPad);
            int liHeight = Mathf.CeilToInt(lORect.height / lfScale) + (2 * PartBackPad);
            UWArtStamp lOBackStamp = UWVersionedTexture.Backs(lOBack.Key, liWidth, liHeight);

            if (!mOPartBackTextures[liAt].IsCurrent(lOBackStamp))
            {
                lOImage.texture = mOPartBackTextures[liAt].Set(lOBackStamp, UWModernHudArt.BuildBackdrop(mOUi.mOUWData.Textures, liWidth, liHeight,
                    lOBack, mOUi.TextureFilterMode, liAt + 1));
            }

            RectTransform lOTransform = (RectTransform)lOImage.transform;

            lOTransform.anchoredPosition = new Vector2(lORect.center.x - (liWidth * lfScale * 0.5f), lORect.center.y - (liHeight * lfScale * 0.5f));
            lOTransform.sizeDelta = new Vector2(liWidth * lfScale, liHeight * lfScale);
            lOImage.enabled = true;
        }
    }
}
