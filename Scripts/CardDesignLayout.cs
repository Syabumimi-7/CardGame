using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>全カード画面で共有する200x280のデザインとカード固有のイラスト。</summary>
[DisallowMultipleComponent]
public class CardDesignLayout : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private CardDesignGraphic surface;
    private CardUIView view;
    private bool hovered;
    private float emphasis;
    private Canvas hoverCanvas;
    private float collectionScale = 1.276f, collectionLift = 72f;
    private CollectionCardClick collectionClick;
    private RectTransform collectionClip, collectionViewport;
    private readonly Vector3[] clipCorners = new Vector3[4];
    public CardDesignGraphic Surface => surface;
    public void EnableCollectionHover(CollectionCardClick click, float scale, float lift)
    {
        collectionClick = click; collectionScale = scale; collectionLift = lift;
        var viewportMask = GetComponentInParent<RectMask2D>();
        if (viewportMask != null && surface != null)
        {
            collectionViewport = viewportMask.rectTransform;
            var clip = new GameObject("CollectionHoverClip", typeof(RectTransform), typeof(RectMask2D));
            clip.layer = gameObject.layer;
            collectionClip = clip.GetComponent<RectTransform>();
            collectionClip.SetParent(transform, false);
            collectionClip.anchorMin = collectionClip.anchorMax = new Vector2(.5f, .5f);
            surface.transform.SetParent(collectionClip, false);
            Fit();
        }
    }

    public void Apply(CardUIView card)
    {
        view=card;
        bool redrawSurface = surface == null;
        if(surface==null)
        {
            // Preserve existing text references and click handlers on both card prefabs.
            foreach(var layout in GetComponentsInChildren<CardResponsiveLayout>(true)) layout.enabled=false;
            foreach(var graphic in GetComponentsInChildren<Graphic>(true))
                if(graphic!=card.cardNameText && graphic!=card.costText && graphic!=card.descriptionText) graphic.enabled=false;
            var rootImage=GetComponent<Image>();
            if(rootImage!=null) {rootImage.sprite=null;rootImage.color=Color.clear;rootImage.enabled=true;rootImage.raycastTarget=true;}
            var go=new GameObject("CardDesign",typeof(RectTransform),typeof(CanvasRenderer),typeof(CardDesignGraphic));
            go.layer=gameObject.layer;go.transform.SetParent(transform,false);
            surface=go.GetComponent<CardDesignGraphic>();surface.raycastTarget=true;
            var rect=surface.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=Vector2.zero;
            rect.sizeDelta=new Vector2(200,280);
            Place(card.cardNameText,56,235,130,34,17,9,Color.white,FontStyles.Bold);
            Place(card.costText,9,229,42,42,30,18,Color.white,FontStyles.Bold);
            Place(card.descriptionText,17,15,166,58,13,7,new Color(.12f,.10f,.08f),FontStyles.Normal);
            var illustration=new GameObject("Illustration",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            illustration.layer=gameObject.layer;illustration.transform.SetParent(rect,false);
            var artRect=illustration.GetComponent<RectTransform>();artRect.anchorMin=artRect.anchorMax=Vector2.zero;artRect.pivot=Vector2.zero;
            artRect.anchoredPosition=new Vector2(10,81);artRect.sizeDelta=new Vector2(180,150);
            card.cardImage=illustration.GetComponent<Image>();card.cardImage.color=Color.clear;card.cardImage.raycastTarget=false;
        }
        if(card.Card!=null && card.Card.Data!=null && surface.rarity != card.Card.Data.rarity)
        {
            surface.rarity=card.Card.Data.rarity;
            redrawSurface=true;
        }
        if(!surface.highlight.Equals(emphasis))
        {
            surface.highlight=emphasis;
            redrawSurface=true;
        }
        ApplyIllustration(card);
        ApplyCategory(card);
        ApplyCostBadge(card);
        var hitArea = GetComponent<Image>();
        if (hitArea != null) hitArea.raycastTarget = card.Owner == null;
        // The frame geometry depends only on rarity and highlight; content still updates above.
        if(redrawSurface) surface.SetVerticesDirty();
        Fit();
    }

    void ApplyCategory(CardUIView card)
    {
        Transform badge = surface.transform.Find("CategoryBadge");
        if (badge == null)
        {
            var go = new GameObject("CategoryBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(surface.transform, false);
            badge = go.transform;
            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            label.layer = gameObject.layer;
            label.transform.SetParent(badge, false);
        }
        var rect = (RectTransform)badge;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(132f, 213f);
        rect.sizeDelta = new Vector2(56f, 18f);
        rect.localScale = Vector3.one;
        var background = badge.GetComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.075f, 0.96f);
        background.raycastTarget = false;
        var text = badge.GetComponentInChildren<TextMeshProUGUI>(true);
        if (card.cardNameText != null)
        {
            text.font = card.cardNameText.font;
            text.fontSharedMaterial = card.cardNameText.fontSharedMaterial;
        }
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(3f, 1f);
        text.rectTransform.offsetMax = new Vector2(-3f, -1f);
        text.color = new Color(0.97f, 0.91f, 0.76f);
        text.fontStyle = FontStyles.Bold;
        text.fontSize = text.fontSizeMax = 10f;
        text.fontSizeMin = 8f;
        text.enableAutoSizing = true;
        text.enableWordWrapping = false;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = card.Card == null || card.Card.Data == null ? string.Empty : CategoryLabel(card.Card.Data.category);
        badge.gameObject.SetActive(card.Card != null && card.Card.Data != null);
        badge.SetAsLastSibling();
    }

    static string CategoryLabel(CardCategory category)
    {
        switch (category)
        {
            case CardCategory.Attack: return "攻撃";
            case CardCategory.Defense: return "防御";
            case CardCategory.Buff: return "バフ";
            case CardCategory.Debuff: return "デバフ";
            default: return string.Empty;
        }
    }

    void ApplyCostBadge(CardUIView card)
    {
        Transform badge = surface.transform.Find("CostBadge");
        if (badge == null)
        {
            var go = new GameObject("CostBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(CardCostBadgeGraphic));
            go.layer = gameObject.layer;
            go.transform.SetParent(surface.transform, false);
            badge = go.transform;
        }
        var rect = (RectTransform)badge;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(200f, 280f);
        rect.localScale = Vector3.one;
        var graphic = badge.GetComponent<CardCostBadgeGraphic>();
        graphic.enabled = true;
        graphic.raycastTarget = false;
        badge.SetAsLastSibling();
        if (card.costText != null) card.costText.transform.SetAsLastSibling();
    }

    void ApplyIllustration(CardUIView card)
    {
        if(card.cardImage==null)return;
        var image=card.cardImage;
        var frame=image.transform.parent as RectTransform;
        if(frame==null || frame.GetComponent<RectMask2D>()==null)
        {
            var go=new GameObject("IllustrationViewport",typeof(RectTransform),typeof(RectMask2D));
            go.layer=gameObject.layer;go.transform.SetParent(surface.transform,false);
            frame=go.GetComponent<RectTransform>();
            image.transform.SetParent(frame,false);
        }
        frame.anchorMin=frame.anchorMax=Vector2.zero;frame.pivot=Vector2.zero;
        frame.anchoredPosition=new Vector2(10,81);frame.sizeDelta=new Vector2(180,150);
        var sprite=card.Card!=null && card.Card.Data!=null ? card.Card.Data.cardImage : null;
        image.overrideSprite=null;image.sprite=sprite;
        image.enabled=true;image.color=sprite!=null ? Color.white : Color.clear;
        image.raycastTarget=false;image.type=Image.Type.Simple;image.preserveAspect=true;
        var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
        rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;
        rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
        // Cover the art window without stretching; the viewport clips only the excess.
        float aspect=sprite!=null && sprite.rect.height>0 ? sprite.rect.width/sprite.rect.height : 1;
        rect.sizeDelta=aspect>=1.2f ? new Vector2(150*aspect,150) : new Vector2(180,180/aspect);
    }

    void Place(TextMeshProUGUI text,float x,float y,float w,float h,float size,float min,Color color,FontStyles style)
    {
        if(text==null)return;
        var rect=text.rectTransform;rect.SetParent(surface.transform,false);rect.anchorMin=rect.anchorMax=Vector2.zero;
        rect.pivot=Vector2.zero;rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
        rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);
        text.enabled=true;text.color=color;text.fontStyle=style;text.alignment=TextAlignmentOptions.Center;
        text.fontSize=size;text.enableAutoSizing=true;text.fontSizeMax=size;text.fontSizeMin=min;
        text.enableWordWrapping=true;text.overflowMode=TextOverflowModes.Truncate;text.margin=Vector4.zero;text.raycastTarget=false;
    }
    void LateUpdate()
    {
        bool interactive=hovered && view!=null && view.enabled &&
            ((view.Owner!=null && !view.Owner.IsAnimating && view.Owner.battleManager!=null && view.Owner.battleManager.CanPlayerAct)
                || (collectionClick != null && collectionClick.CanHover));
        var group=GetComponent<CanvasGroup>();
        if(group!=null && (!group.interactable || !group.blocksRaycasts)) interactive=false;
        UpdateHoverSorting(interactive);
        float next=Mathf.MoveTowards(emphasis,interactive?1:0,Time.unscaledDeltaTime/0.12f);
        if(!Mathf.Approximately(next,emphasis))
        {
            emphasis=next;
            if(surface!=null){surface.highlight=emphasis;surface.SetVerticesDirty();}
        }
        Fit();
    }
    void OnRectTransformDimensionsChange() { Fit(); }
    private void UpdateHoverSorting(bool active)
    {
        if (active && hoverCanvas == null)
        {
            hoverCanvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
        }
        if (hoverCanvas == null) return;
        // A nested canvas changes drawing order without moving cards in the layout.
        Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        hoverCanvas.overrideSorting = active;
        if (active && parentCanvas != null)
        {
            hoverCanvas.sortingLayerID = parentCanvas.sortingLayerID;
            hoverCanvas.sortingOrder = parentCanvas.sortingOrder + 1;
        }
    }
    void Fit()
    {
        if(surface==null)return;
        var parent=(RectTransform)transform;
        // Previously, clicking multiplied the existing 1.1x hover by selectedScale.
        // Keep that same visible size when hovering a hand card, without selection.
        float hoverScale=view!=null && view.Owner!=null ? 1.1f*view.Owner.selectedScale : collectionClick != null ? collectionScale : 1.1f;
        float scale=Mathf.Min(parent.rect.width/200f,parent.rect.height/280f)*Mathf.Lerp(1f,hoverScale,emphasis);
        surface.rectTransform.localScale=Vector3.one*scale;
        surface.rectTransform.anchoredPosition=new Vector2(-100,-140)*scale;
        if (view != null && view.Owner != null)
        {
            // Leave an exposed strip beneath the raised card so adjacent cards remain reachable.
            float lift = Mathf.Max(view.Owner.selectedLift, parent.rect.height * (hoverScale - 1f) * 0.5f + 20f);
            surface.rectTransform.anchoredPosition += Vector2.up * lift * emphasis;
        }
        else if (collectionClick != null)
            surface.rectTransform.anchoredPosition += Vector2.up * collectionLift * emphasis;
        if (collectionClip != null && collectionViewport != null)
        {
            // The hover canvas bypasses ancestor masks. Repeat the viewport mask
            // inside that canvas, while keeping the card centered in its grid cell.
            collectionViewport.GetWorldCorners(clipCorners);
            Vector2 bottomLeft = parent.InverseTransformPoint(clipCorners[0]);
            Vector2 topRight = parent.InverseTransformPoint(clipCorners[2]);
            collectionClip.sizeDelta = topRight - bottomLeft;
            collectionClip.anchoredPosition = (bottomLeft + topRight) * .5f - parent.rect.center;
            surface.rectTransform.anchoredPosition -= collectionClip.anchoredPosition;
        }
    }
    public void OnPointerEnter(PointerEventData eventData) { if(isActiveAndEnabled)hovered=true; }
    public void OnPointerExit(PointerEventData eventData) { hovered=false; }
    void OnDisable()
    {
        hovered=false;emphasis=0;
        UpdateHoverSorting(false);
        if(surface!=null){surface.highlight=0;surface.SetVerticesDirty();Fit();}
    }
}
