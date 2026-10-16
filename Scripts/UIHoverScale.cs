using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>任意のUIへ付けられる、ポインターホバー時の拡大演出です。</summary>
public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Min(1f)] public float hoverScale = 1.08f;
    [Min(0f)] public float duration = 0.12f;
    public Ease ease = Ease.OutQuad;
    private Vector3 normalScale;

    private void Awake() { normalScale = transform.localScale; }
    private void OnDisable() { transform.DOKill(); transform.localScale = normalScale; }

    public void SetNormalScale(Vector3 scale)
    {
        normalScale = scale;
        if (enabled) transform.localScale = normalScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || GetComponent<CardDesignLayout>() != null) return;
        transform.DOKill();
        transform.DOScale(normalScale * hoverScale, duration).SetEase(ease).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || GetComponent<CardDesignLayout>() != null) return;
        transform.DOKill();
        transform.DOScale(normalScale, duration).SetEase(ease).SetUpdate(true);
    }
}
