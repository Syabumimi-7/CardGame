using UnityEngine;
using UnityEngine.EventSystems; // マウスの押し込み・離しを検知するために必要
using DG.Tweening; // DOTweenを使用

// IPointerDownHandler(押し込み), IPointerUpHandler(離し) を使います
public class ButtonPressAnimator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("沈ませたいUIのリスト")]
    [Tooltip("ボタンを押したときに「沈む」アニメーションをつけたいUIをセットします。")]
    public GameObject[] targetUIs;

    [Header("アニメーション設定")]
    [Tooltip("どれくらい沈むか（縮小するか）。0.9なら元の90%のサイズになります。")]
    public float pressScale = 0.9f;

    [Tooltip("沈むまでのスピード（秒）。早く反応させたいので0.1秒など短い時間がおすすめです。")]
    public float duration = 0.1f;

    // 各UIの「元の大きさ」を記憶しておくための配列
    private Vector3[] defaultScales;

    private void Awake()
    {
        // ゲーム開始時に、登録されたすべてのUIの元の大きさを記憶します
        defaultScales = new Vector3[targetUIs.Length];
        
        for (int i = 0; i < targetUIs.Length; i++)
        {
            if (targetUIs[i] != null)
            {
                defaultScales[i] = targetUIs[i].transform.localScale;
            }
        }
    }

    // --- マウスのボタンを「押し込んだ」瞬間に呼ばれる処理 ---
    public void OnPointerDown(PointerEventData eventData)
    {
        for (int i = 0; i < targetUIs.Length; i++)
        {
            if (targetUIs[i] != null)
            {
                // アニメーションの重複を防ぐために一度停止
                targetUIs[i].transform.DOKill();
                
                // 現在のサイズから、設定した割合（pressScale）まで素早く縮小する
                targetUIs[i].transform.DOScale(defaultScales[i] * pressScale, duration).SetEase(Ease.OutQuad);
            }
        }
    }

    // --- マウスのボタンを「離した」瞬間に呼ばれる処理 ---
    public void OnPointerUp(PointerEventData eventData)
    {
        for (int i = 0; i < targetUIs.Length; i++)
        {
            if (targetUIs[i] != null)
            {
                // アニメーションを停止し、元の大きさに戻す
                targetUIs[i].transform.DOKill();
                
                // 少しバネのように（OutBack）跳ね返りながら元の大きさに戻る
                targetUIs[i].transform.DOScale(defaultScales[i], duration).SetEase(Ease.OutBack);
            }
        }
    }
}