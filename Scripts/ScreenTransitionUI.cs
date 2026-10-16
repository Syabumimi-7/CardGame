using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>各画面で共通利用する、入力遮断付きのフェード遷移です。</summary>
public class ScreenTransitionUI : MonoBehaviour
{
    [Min(0f)] public float fadeOutDuration = 0.22f;
    [Min(0f)] public float fadeInDuration = 0.32f;

    private CanvasGroup canvasGroup;
    private Sequence sequence;
    private Coroutine preparedTransition;
    private IEnumerator preparation;

    public static ScreenTransitionUI Create(params GameObject[] screenRoots)
    {
        Canvas canvas = null;
        foreach (GameObject root in screenRoots)
        {
            if (root == null) continue;
            canvas = root.GetComponentInParent<Canvas>();
            if (canvas != null) break;
        }
        if (canvas == null) return null;

        GameObject obj = new GameObject("ScreenTransition", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasRenderer), typeof(Image), typeof(GraphicRaycaster),
            typeof(CanvasGroup), typeof(ScreenTransitionUI));
        obj.layer = 5;
        obj.transform.SetParent(GameViewport.ContentFor(canvas), false);
        obj.transform.SetAsLastSibling();
        Canvas transitionCanvas = obj.GetComponent<Canvas>();
        transitionCanvas.overrideSorting = true;
        transitionCanvas.sortingOrder = 100;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        obj.GetComponent<Image>().color = new Color(0.008f, 0.014f, 0.022f, 1f);
        CanvasGroup group = obj.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return obj.GetComponent<ScreenTransitionUI>();
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Play(Action switchScreen, Action afterTransition = null)
    {
        CancelPreparation();
        if (canvasGroup == null)
        {
            switchScreen?.Invoke();
            return;
        }

        sequence?.Kill();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(canvasGroup.DOFade(1f, fadeOutDuration).SetEase(Ease.InOutQuad));
        sequence.AppendCallback(() => switchScreen?.Invoke());
        sequence.AppendInterval(0.06f);
        sequence.Append(canvasGroup.DOFade(0f, fadeInDuration).SetEase(Ease.OutCubic));
        sequence.OnComplete(() =>
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            afterTransition?.Invoke();
        });
    }

    // Keep the existing opaque cover in place until the real UI has rendered once.
    public void PlayPrepared(Action showCoveredScreen, IEnumerator prepare, Action startScreen)
    {
        CancelPreparation();
        if (canvasGroup == null)
        {
            (prepare as IDisposable)?.Dispose();
            showCoveredScreen?.Invoke();
            startScreen?.Invoke();
            return;
        }
        sequence?.Kill();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        preparation = prepare;
        preparedTransition = StartCoroutine(PrepareAndPlay(showCoveredScreen, startScreen));
    }

    private IEnumerator PrepareAndPlay(Action showCoveredScreen, Action startScreen)
    {
        try
        {
            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(canvasGroup.DOFade(1f, fadeOutDuration).SetEase(Ease.InOutQuad));
            while (sequence.IsActive() && !sequence.IsComplete()) yield return null;
            showCoveredScreen?.Invoke();
            while (preparation != null && preparation.MoveNext()) yield return preparation.Current;
            (preparation as IDisposable)?.Dispose();
            preparation = null;
            startScreen?.Invoke();
            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.AppendInterval(.06f);
            sequence.Append(canvasGroup.DOFade(0f, fadeInDuration).SetEase(Ease.OutCubic));
            while (sequence.IsActive() && !sequence.IsComplete()) yield return null;
        }
        finally
        {
            (preparation as IDisposable)?.Dispose();
            preparation = null;
            preparedTransition = null;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
    }

    private void CancelPreparation()
    {
        if (preparedTransition != null) StopCoroutine(preparedTransition);
        preparedTransition = null;
        (preparation as IDisposable)?.Dispose();
        preparation = null;
    }

    private void OnDisable() { CancelPreparation(); }
}
