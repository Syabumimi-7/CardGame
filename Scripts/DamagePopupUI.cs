using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum DamagePopupKind
{
    Attack,
    Poison,
    Bleeding,
    Electrocution,
    Heal,
    Blocked,
    BlockBroken,
    Thorns,
    SelfDamage
}

/// <summary>Screen-space damage numbers anchored above the combatant models.</summary>
[DefaultExecutionOrder(200)]
public class DamagePopupUI : MonoBehaviour
{
    [Range(48, 72)]
    public float fontSize = 64;
    [Min(.4f)]
    public float duration = 1f;
    public float rise = 85;
    public float headOffset = .18f;
    public int sortingOrder = 60;
    BattleManager battle;
    RectTransform root;
    readonly List<Popup> active = new List<Popup>();
    readonly Stack<Popup> pool = new Stack<Popup>();
    readonly List<Popup> preparing = new List<Popup>();
    bool visualsPrepared;
    public bool AreVisualsPrepared => visualsPrepared;
    static readonly string[] Colors = {"#FFD27A", "#FF8A2B", "#6A6BFF", "#FF333B", "#B56BFF", "#D12B2B", "#FFD63B", "#54E66B", "#4DA7FF", "#4DA7FF", "#B0B0B0", "#FF4D4D"};
    class Popup
    {
        public RectTransform rect;
        public CanvasGroup group;
        public TextMeshProUGUI text;
        public DamagePopupGraphic effect;
        public Transform target;
        public Vector3 localAnchor, lastAnchor;
        public Vector2 offset;
        public float elapsed;
        public float delay;
    }

    public void Initialize(BattleManager owner)
    {
        battle = owner;
        if (root != null)
            return;
        var go = new GameObject("Damage Display Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        root = go.GetComponent<RectTransform>();
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        root = GameViewport.ContentFor(canvas);
    }

    // Call only while an opaque transition covers this canvas. Alpha must stay nonzero
    // for the normal UI renderer to submit the actual font/effect shader variants.
    public IEnumerator PrepareForBattle()
    {
        if (visualsPrepared || root == null || preparing.Count > 0) yield break;
        try
        {
            for (int i = 0; i < 4; i++)
            {
                Popup p = pool.Count > 0 ? pool.Pop() : CreatePopup();
                preparing.Add(p);
                p.target = null;
                p.rect.anchoredPosition = Vector2.zero;
                p.rect.localScale = Vector3.one;
                p.group.alpha = 1;
                p.text.text = "0123456789";
                p.text.fontSize = fontSize;
                p.effect.style = i == 0 ? 0 : i == 1 ? 7 : i == 2 ? 8 : 11;
                p.effect.progress = .5f;
                p.effect.SetVerticesDirty();
                p.rect.gameObject.SetActive(true);
                p.text.ForceMeshUpdate();
            }
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            visualsPrepared = preparing.Count > 0 && root.gameObject.activeInHierarchy;
        }
        finally { ReleasePreparation(); }
    }

    void ReleasePreparation()
    {
        foreach (Popup p in preparing)
        {
            p.group.alpha = 0;
            p.text.text = string.Empty;
            p.rect.gameObject.SetActive(false);
            pool.Push(p);
        }
        preparing.Clear();
    }

    // バトル処理からの表示要求。対象と表示スタイルを決めてEmitへ渡す。
    public static void ShowDamage(MonoBehaviour recipient, int bodyDamage, int absorbed, bool blockBroken, DamagePopupKind kind)
    {
        if (absorbed > 0)
            Show(recipient, absorbed, blockBroken ? DamagePopupKind.BlockBroken : DamagePopupKind.Blocked);
        if (bodyDamage > 0)
            Show(recipient, bodyDamage, kind, absorbed > 0 ? .18f : 0);
    }

    public static void Show(MonoBehaviour recipient, int amount, DamagePopupKind kind, float delay = 0)
    {
        if (amount <= 0 || !Application.isPlaying)
            return;
        var owner = FindObjectOfType<BattleManager>();
        if (owner == null || !owner.isActiveAndEnabled)
            return;
        bool player = recipient == owner.playerStatus;
        if (!player && (owner.enemyManager == null || recipient != owner.enemyManager.CurrentEnemy))
            return;
        var ui = owner.GetComponent<DamagePopupUI>();
        if (ui == null)
            ui = owner.gameObject.AddComponent<DamagePopupUI>();
        ui.Initialize(owner);
        Transform target = player ? (owner.playerVisualManager != null && owner.playerVisualManager.CurrentPlayer != null ? owner.playerVisualManager.CurrentPlayer.transform : recipient.transform) : recipient.transform;
        int style = kind == DamagePopupKind.Attack ? (player ? 3 : Mathf.Clamp(owner.SelectedPackIndex, 0, 2)) : (int)kind + 3;
        ui.Emit(target, amount, style, delay);
    }

    // プールから表示を取得し、対象モデル上の開始位置を設定する。
    void Emit(Transform target, int amount, int style, float delay)
    {
        Popup p = pool.Count > 0 ? pool.Pop() : CreatePopup();
        p.target = target;
        var bounds = new Bounds(target.position, Vector3.zero);
        bool found = false;
        foreach (var renderer in target.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer)
                continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
                bounds.Encapsulate(renderer.bounds);
        }

        p.lastAnchor = new Vector3(bounds.center.x, bounds.max.y + headOffset, bounds.center.z);
        p.localAnchor = target.InverseTransformPoint(p.lastAnchor);
        int count = active.FindAll(item => item.target == target).Count;
        // Alternate sides and rows so simultaneous status hits remain legible.
        p.offset = count == 0 ? Vector2.zero : new Vector2((count % 2 == 1 ? -1 : 1) * (70 + count / 2 * 22), count / 2 * 42);
        p.offset += new Vector2(Random.Range(-12f, 12f), Random.Range(0f, 16f));
        p.elapsed = 0;
        p.delay = delay;
        ColorUtility.TryParseHtmlString(Colors[style], out Color tint);
        p.text.text = amount.ToString();
        p.text.fontSize = fontSize;
        // Keep a hint of the damage type in the face, with a much brighter value
        // than the effect behind it. The dark outline separates crossing rays.
        p.text.color = Color.Lerp(tint, Color.white, .82f);
        p.effect.color = new Color(tint.r * .72f, tint.g * .72f, tint.b * .72f, 1);
        p.effect.style = style;
        p.effect.progress = 0;
        p.effect.SetVerticesDirty();
        p.group.alpha = 0;
        p.rect.localScale = Vector3.one * .25f;
        p.rect.gameObject.SetActive(true);
        active.Add(p);
    }

    Popup CreatePopup()
    {
        var go = new GameObject("Damage Popup", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(root, false);
        var p = new Popup{rect = go.GetComponent<RectTransform>(), group = go.GetComponent<CanvasGroup>()};
        p.rect.sizeDelta = new Vector2(240, 180);
        p.group.blocksRaycasts = false;
        p.group.interactable = false;
        var fx = new GameObject("Effect", typeof(RectTransform), typeof(CanvasRenderer), typeof(DamagePopupGraphic));
        fx.transform.SetParent(go.transform, false);
        p.effect = fx.GetComponent<DamagePopupGraphic>();
        p.effect.rectTransform.sizeDelta = new Vector2(220, 160);
        p.effect.raycastTarget = false;
        var label = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        p.text = label.GetComponent<TextMeshProUGUI>();
        p.text.rectTransform.sizeDelta = new Vector2(280, 130);
        p.text.font = TMP_Settings.defaultFontAsset;
        p.text.fontStyle = FontStyles.Bold | FontStyles.Italic;
        p.text.alignment = TextAlignmentOptions.Center;
        p.text.raycastTarget = false;
        p.text.outlineColor = new Color(.025f, .018f, .04f, 1);
        p.text.outlineWidth = .32f;
        var material = p.text.fontMaterial;
        if (material.HasProperty("_UnderlayColor"))
        {
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(.015f, .01f, .025f, .9f));
            material.SetFloat("_UnderlayOffsetX", .8f);
            material.SetFloat("_UnderlayOffsetY", -.8f);
            material.SetFloat("_UnderlayDilate", .15f);
            material.SetFloat("_UnderlaySoftness", .12f);
        }

        p.text.UpdateMeshPadding();
        return p;
    }

    // 表示の経過時間と画面上の位置を更新する。
    void LateUpdate()
    {
        Camera camera = Camera.main;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Popup p = active[i];
            if (!battle.IsPaused)
                p.elapsed += Time.deltaTime;
            float elapsed = p.elapsed - p.delay;
            if (elapsed < 0)
            {
                p.group.alpha = 0;
                continue;
            }

            if (elapsed >= duration)
            {
                Recycle(i);
                continue;
            }

            if (p.target != null)
                p.lastAnchor = p.target.TransformPoint(p.localAnchor);
            if (camera == null)
            {
                p.group.alpha = 0;
                continue;
            }

            Vector3 screen = camera.WorldToScreenPoint(p.lastAnchor);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 position);
            p.rect.anchoredPosition = position + p.offset + Vector2.up * rise * Mathf.Clamp01((elapsed - .1f) / Mathf.Max(.1f, duration - .1f));
            float scale = elapsed < .1f ? Mathf.Lerp(.25f, 1.18f, elapsed / .1f) : Mathf.Lerp(1.18f, 1, Mathf.Clamp01((elapsed - .1f) / .12f));
            p.rect.localScale = Vector3.one * scale;
            p.group.alpha = screen.z <= 0 ? 0 : Mathf.Min(Mathf.Clamp01(elapsed / .06f), Mathf.Clamp01((duration - elapsed) / .3f));
            if (p.effect.style >= 7)
            {
                p.effect.progress = Mathf.Clamp01(elapsed / duration);
                p.effect.SetVerticesDirty();
            }
        }
    }

    // 表示終了時は削除せずプールへ戻す。
    void Recycle(int index)
    {
        var p = active[index];
        p.rect.gameObject.SetActive(false);
        active.RemoveAt(index);
        pool.Push(p);
    }

    public void Clear()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            Recycle(i);
    }

    void OnDisable()
    {
        ReleasePreparation();
        Clear();
    }

    void OnDestroy()
    {
        foreach (var p in active)
            if (p.text != null)
                Destroy(p.text.fontMaterial);
        foreach (var p in pool)
            if (p.text != null)
                Destroy(p.text.fontMaterial);
    }
}
