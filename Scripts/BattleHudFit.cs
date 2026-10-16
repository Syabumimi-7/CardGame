using UnityEngine;

/// <summary>Keeps the complete 1920 x 1080 HUD inside any canvas aspect ratio.</summary>
[ExecuteAlways]
public class BattleHudFit : MonoBehaviour
{
    void OnEnable() { Fit(); }
    void LateUpdate() { Fit(); }
    void Fit()
    {
        var parent = transform.parent as RectTransform;
        if (parent == null) return;
        float scale = Mathf.Min(parent.rect.width / 1920f, parent.rect.height / 1080f);
        if (scale > 0) transform.localScale = Vector3.one * scale;
    }
}
