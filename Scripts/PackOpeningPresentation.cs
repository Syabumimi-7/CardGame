using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static CardAcquisitionPresentation;

/// <summary>パック開封だけを担当。獲得後の画面とカード表示は呼び出し元へ委譲します。</summary>
public sealed class PackOpeningPresentation : MonoBehaviour
{
    public bool IsPlaying { get; private set; }
    private Coroutine routine;
    private GameObject overlay;
    private CanvasGroup sourceGroup;
    private float sourceAlpha;
    private bool sourceInteractable, sourceRaycasts;
    private CanvasGroup selectedGroup;
    private float selectedAlpha;

    public void Play(PackSelectionItem selected, GameObject source, GameObject destination,
        Action switchScreen, Action showCards, bool appearAtCenter = false)
    {
        if (IsPlaying) return;
        IsPlaying = true;
        routine = StartCoroutine(Animate(selected, source, destination, switchScreen, showCards, appearAtCenter));
    }

    private IEnumerator Animate(PackSelectionItem selected, GameObject source,
        GameObject destination, Action switchScreen, Action showCards, bool appearAtCenter)
    {
        Canvas canvas = source.GetComponentInParent<Canvas>().rootCanvas;
        RectTransform layer = CreateRect("PackOpeningOverlay", GameViewport.ContentFor(canvas));
        overlay = layer.gameObject;
        Stretch(layer);
        Canvas topCanvas = overlay.AddComponent<Canvas>();
        topCanvas.overrideSorting = true;
        topCanvas.sortingOrder = 90;
        overlay.AddComponent<GraphicRaycaster>();
        Image blocker = overlay.AddComponent<Image>();
        blocker.color = Color.clear;

        if (!appearAtCenter) selected.ResetVisual();
        RectTransform original = (RectTransform)selected.transform.Find("PackVisual");
        RectTransform pack = Instantiate(original, layer, false);
        pack.name = "OpeningPack";
        pack.anchorMin = pack.anchorMax = new Vector2(.5f, .5f);
        pack.position = original.position;
        pack.rotation = original.rotation;
        pack.localScale = new Vector3(original.lossyScale.x / layer.lossyScale.x,
            original.lossyScale.y / layer.lossyScale.y, 1);
        if (appearAtCenter)
        {
            pack.anchoredPosition = Vector2.zero;
            pack.localRotation = Quaternion.identity;
            pack.localScale = Vector3.one * Mathf.Min(layer.rect.width / 1600f, layer.rect.height / 900f);
        }
        CanvasGroup cloneGroup = pack.GetComponent<CanvasGroup>();
        cloneGroup.alpha = appearAtCenter ? 0 : 1;
        foreach (Graphic graphic in pack.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;

        if (!appearAtCenter)
        {
            selectedGroup = original.GetComponent<CanvasGroup>();
            selectedAlpha = selectedGroup.alpha;
            selectedGroup.alpha = 0;
        }
        sourceGroup = source.GetComponent<CanvasGroup>();
        if (sourceGroup == null) sourceGroup = source.AddComponent<CanvasGroup>();
        sourceAlpha = sourceGroup.alpha;
        sourceInteractable = sourceGroup.interactable;
        sourceRaycasts = sourceGroup.blocksRaycasts;
        sourceGroup.interactable = sourceGroup.blocksRaycasts = false;
        yield return AnimateFor(.32f, t => sourceGroup.alpha = sourceAlpha * (1 - Smooth(t)));

        Vector2 start = pack.anchoredPosition;
        // Keep pack2 completely still; side packs meet its existing central position.
        if (appearAtCenter)
            yield return AnimateFor(.24f, t => cloneGroup.alpha = Smooth(t));
        else
            yield return AnimateFor(.24f, t => pack.anchoredPosition =
                new Vector2(Mathf.Lerp(start.x, 0, 1 - Mathf.Pow(1 - t, 3)), start.y));
        yield return AnimateFor(.10f, null);

        RectTransform detachedTop = SplitWrapper(pack, layer);
        RectTransform lightRect = CreateRect("OpeningLight", pack);
        lightRect.sizeDelta = new Vector2(300, 540);
        AcquisitionEffectGraphic light = lightRect.gameObject.AddComponent<AcquisitionEffectGraphic>();
        light.mode = AcquisitionEffectGraphic.Mode.Opening;
        light.raycastTarget = false;
        yield return AnimateFor(.22f, t => light.Cut = t);

        Vector2 topStart = detachedTop.anchoredPosition;
        CanvasGroup topGroup = detachedTop.gameObject.AddComponent<CanvasGroup>();
        yield return AnimateFor(.32f, t =>
        {
            detachedTop.anchoredPosition = topStart + new Vector2(18 * t, 65 * Smooth(t));
            detachedTop.localRotation = Quaternion.Euler(0, 0, -8 * t);
            topGroup.alpha = 1 - Smooth(t);
            light.Intensity = .25f * t;
        });
        yield return AnimateFor(.55f, t => light.Intensity = Mathf.Lerp(.25f, 1, t * t));

        RectTransform flashRect = CreateRect("Whiteout", layer);
        Stretch(flashRect);
        Image flash = flashRect.gameObject.AddComponent<Image>();
        flash.color = new Color(1, 1, 1, 0);
        yield return AnimateFor(.35f, t => flash.color = new Color(1, 1, 1, Smooth(t)));

        // Only replace the world/background once the whiteout is fully opaque.
        CardAcquisitionPresentation.InstallBackground(destination.transform);
        switchScreen?.Invoke();
        pack.gameObject.SetActive(false);
        RestoreSource();
        yield return AnimateFor(.08f, null);
        yield return AnimateFor(.40f, t => flash.color = new Color(1, 1, 1, 1 - Smooth(t)));
        Cleanup();
        routine = null;
        IsPlaying = false;
        showCards?.Invoke();
    }

    private static RectTransform SplitWrapper(RectTransform pack, RectTransform layer)
    {
        // Clone before creating slice containers so both halves contain exactly the same artwork.
        RectTransform topVisual = Instantiate(pack, layer, false);
        topVisual.name = "TopVisual";
        var children = new List<Transform>();
        foreach (Transform child in pack) children.Add(child);
        float cut = AcquisitionEffectGraphic.OpeningCutHeight;
        float bottom = pack.rect.yMin - 40f;
        float top = pack.rect.yMax + 40f;
        RectTransform lowerMask = CreateRect("RemainingWrapper", pack);
        lowerMask.sizeDelta = new Vector2(pack.rect.width + 80f, cut - bottom);
        lowerMask.anchoredPosition = new Vector2(0f, (bottom + cut) * .5f);
        lowerMask.gameObject.AddComponent<RectMask2D>();
        RectTransform lowerVisual = CreateRect("BottomVisual", lowerMask);
        lowerVisual.sizeDelta = pack.rect.size;
        lowerVisual.anchoredPosition = -lowerMask.anchoredPosition;
        foreach (Transform child in children) child.SetParent(lowerVisual, false);

        RectTransform upperMask = CreateRect("DetachedTop", pack);
        upperMask.sizeDelta = new Vector2(pack.rect.width + 80f, top - cut);
        upperMask.anchoredPosition = new Vector2(0f, (top + cut) * .5f);
        upperMask.gameObject.AddComponent<RectMask2D>();
        topVisual.SetParent(upperMask, false);
        topVisual.anchorMin = topVisual.anchorMax = new Vector2(.5f, .5f);
        topVisual.anchoredPosition = -upperMask.anchoredPosition;
        topVisual.localScale = Vector3.one;
        topVisual.localRotation = Quaternion.identity;
        return upperMask;
    }

    private static IEnumerator AnimateFor(float seconds, Action<float> update)
    {
        float elapsed = 0;
        update?.Invoke(0);
        while (elapsed < seconds)
        {
            yield return null;
            elapsed += Time.deltaTime;
            update?.Invoke(Mathf.Clamp01(elapsed / seconds));
        }
    }

    private static float Smooth(float t) => t * t * (3 - 2 * t);

    public void Cancel()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        Cleanup();
        IsPlaying = false;
    }

    private void RestoreSource()
    {
        if (sourceGroup != null)
        {
            sourceGroup.alpha = sourceAlpha;
            sourceGroup.interactable = sourceInteractable;
            sourceGroup.blocksRaycasts = sourceRaycasts;
            sourceGroup = null;
        }
        if (selectedGroup != null) selectedGroup.alpha = selectedAlpha;
        selectedGroup = null;
    }

    private void Cleanup()
    {
        RestoreSource();
        if (overlay != null) { overlay.SetActive(false); Destroy(overlay); }
        overlay = null;
    }

    private void OnDisable() { Cancel(); }

}
