using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening; // DOTweenを使用

// IPointerDownHandler(押し込み) を使ってボタンを押した瞬間に反応させます
public class ButtonBlinkAnimator : MonoBehaviour, IPointerDownHandler
{
    [Header("点滅させたいUIのリスト")]
    [Tooltip("ボタンを押したときに点滅させたいUIをセットします。")]
    public GameObject[] targetUIs;

    [Header("アニメーション設定")]
    [Tooltip("1回の明暗にかかる時間（秒）。0.08秒などにすると素早い点滅になります。")]
    public float blinkDuration = 0.08f;

    [Tooltip("点滅時の最小透明度（0で完全透明、0.2でうっすら見える程度）")]
    [Range(0f, 1f)]
    public float minAlpha = 0.1f;

    // 各UIの CanvasGroup を保持する配列
    private CanvasGroup[] canvasGroups;

    private void Awake()
    {
        if (targetUIs == null) return;

        canvasGroups = new CanvasGroup[targetUIs.Length];

        // 各UIに CanvasGroup がなければ自動で追加して準備します
        for (int i = 0; i < targetUIs.Length; i++)
        {
            if (targetUIs[i] != null)
            {
                CanvasGroup cg = targetUIs[i].GetComponent<CanvasGroup>();
                if (cg == null)
                {
                    cg = targetUIs[i].AddComponent<CanvasGroup>();
                }
                canvasGroups[i] = cg;
            }
        }
    }

    // --- マウスのボタンを「押し込んだ」瞬間に呼ばれる処理 ---
    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetUIs == null) return;

        for (int i = 0; i < targetUIs.Length; i++)
        {
            if (targetUIs[i] != null && canvasGroups[i] != null)
            {
                CanvasGroup cg = canvasGroups[i];

                // 実行中のアニメーションを停止して不透明度を1にリセット
                cg.DOKill();
                cg.alpha = 1f;

                // DOFadeで minAlpha まで透明にし、SetLoops(4, LoopType.Yoyo) で「消える・つく」を2往復（計4回）行います
                cg.DOFade(minAlpha, blinkDuration)
                    .SetLoops(4, LoopType.Yoyo)
                    .SetEase(Ease.InOutQuad)
                    .OnComplete(() => cg.alpha = 1f); // 完了後に確実に不透明に戻す
            }
        }
    }
}