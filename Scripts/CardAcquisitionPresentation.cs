using UnityEngine;
using DG.Tweening;

/// <summary>任意のカード獲得画面に取り付け可能な背景。パックやデッキ生成には依存しません。</summary>
public static class CardAcquisitionPresentation
{
    public static Tween AddCardEntry(Sequence sequence, Transform card, int index)
    {
        card.localScale = Vector3.zero;
        Tween tween = card.DOScale(Vector3.one, .4f).SetEase(Ease.OutBack);
        sequence.Insert(index * .2f, tween);
        return tween;
    }

    public static AcquisitionEffectGraphic InstallBackground(Transform screen)
    {
        Transform existing = screen.Find("CardAcquisitionBackground");
        if (existing != null) return existing.GetComponent<AcquisitionEffectGraphic>();
        RectTransform rect = CreateRect("CardAcquisitionBackground", screen);
        Stretch(rect);
        rect.SetAsFirstSibling();
        AcquisitionEffectGraphic effect = rect.gameObject.AddComponent<AcquisitionEffectGraphic>();
        effect.mode = AcquisitionEffectGraphic.Mode.Background;
        effect.raycastTarget = false;
        return effect;
    }

    internal static RectTransform CreateRect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        return rect;
    }

    internal static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
