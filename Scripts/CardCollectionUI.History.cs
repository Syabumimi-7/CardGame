using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class CardCollectionUI
{
    private GameObject historyPage, cardsTabMarker, historyTabMarker;
    private RectTransform historyList;
    private TMP_Text historyCount;
    private RectTransform cardsFrame;
    private GridLayoutGroup historyCardGrid;
    private readonly List<CardData> historyCardData = new List<CardData>();

    private void BuildHistoryTabs(RectTransform frame)
    {
        cardsFrame = frame;
        var cardsButton = MakeButton(screen.transform, "カード一覧", new Vector2(.36f, .87f),
            new Vector2(370, 64), () => SelectHistoryTab(false));
        var historyButton = MakeButton(screen.transform, "プレイ記録", new Vector2(.64f, .87f),
            new Vector2(370, 64), () => SelectHistoryTab(true));
        cardsTabMarker = SelectionFrame(cardsButton.transform, "SelectedTab");
        historyTabMarker = SelectionFrame(historyButton.transform, "SelectedTab");
        historyPage = Rect("PlayHistory", screen.transform, new Vector2(.5f, .5f), Vector2.zero).gameObject;
        Stretch((RectTransform)historyPage.transform);
        historyCount = Label(historyPage.transform, "", new Vector2(.5f, .79f), new Vector2(1600, 72), 23);
        historyList = HistoryScroll(historyPage.transform, .23f, .73f);
        SelectHistoryTab(false);
    }

    private void SelectHistoryTab(bool history)
    {
        CloseDetail();
        cardsFrame.gameObject.SetActive(!history);
        foreach (var marker in packMarkers) marker.transform.parent.gameObject.SetActive(!history);
        cardsTabMarker.SetActive(!history);
        historyTabMarker.SetActive(history);
        historyPage.SetActive(history);
        if (history) RefreshHistory();
    }

    private void RefreshHistory()
    {
        for (int i = historyList.childCount - 1; i >= 0; i--)
        {
            var child = historyList.GetChild(i).gameObject;
            child.SetActive(false); Destroy(child);
        }
        var records = PlayHistory.Read();
        int count = records == null ? 0 : records.Count;
        int archived = records == null ? 0 : records.FindAll(item => item.archived).Count;
        historyCount.text = $"記録 {count}/30　｜　保管中 {archived}件\n" +
            (PlayHistory.Notice ?? "保管中の記録は自動削除されません。30件すべて保管中の場合、新しい記録は保存されません。");
        if (count == 0)
        {
            var empty = HistoryRow(historyList, 130);
            RowText(empty, "まだプレイ記録がありません。\nゲームをクリア、またはゲームオーバーになると記録されます。", .04f, .96f, 27);
            return;
        }
        for (int i = count - 1; i >= 0; i--)
        {
            var record = records[i];
            var row = HistoryRow(historyList, 130);
            var open = row.gameObject.AddComponent<Button>();
            open.targetGraphic = row.GetComponent<BattleHudGraphic>();
            open.onClick.AddListener(() => ShowHistoryRecord(record));
            RowText(row, ResultText(record) + "\n" + DateText(record) + "　｜　" +
                GameManager.GetPackDisplayName(Mathf.Clamp(record.pack, 0, 2)) + $"　｜　デッキ {record.deck.Count}枚", .025f, .65f, 27);
            var pin = MakeButton(row, record.archived ? "保管中" : "保管する", new Vector2(.76f, .5f),
                new Vector2(200, 66), () => { PlayHistory.SetArchived(record.id, !record.archived); RefreshHistory(); });
            if (record.archived) SelectionFrame(pin.transform, "Archived");
            MakeButton(row, "削除", new Vector2(.92f, .5f), new Vector2(145, 66), () => ConfirmDeleteHistory(record));
        }
    }

    private void ConfirmDeleteHistory(PlayHistory.Record record)
    {
        CloseDetail();
        back.gameObject.SetActive(false);
        detail = Overlay("HistoryDeleteConfirmation", screen.transform, 75, .96f);
        var panel = Rect("ConfirmationPanel", detail.transform, new Vector2(.5f, .53f), new Vector2(1150, 480));
        panel.gameObject.AddComponent<BattleHudGraphic>().color = new Color(.045f, .04f, .03f, .98f);
        Label(panel, "プレイ記録の削除", new Vector2(.5f, .84f), new Vector2(1050, 70), 38, true);
        Label(panel, (record.archived ? "保管中の記録です。" : "") + "この記録を削除しますか？\n削除した記録は元に戻せません。",
            new Vector2(.5f, .63f), new Vector2(1050, 100), 27);
        Label(panel, ResultText(record) + "\n" + DateText(record), new Vector2(.5f, .40f), new Vector2(1050, 100), 25);
        MakeButton(panel, "キャンセル", new Vector2(.70f, .15f), new Vector2(320, 68), CloseDetail);
        MakeButton(panel, "削除する", new Vector2(.30f, .15f), new Vector2(320, 68), () =>
        {
            PlayHistory.Delete(record.id);
            CloseDetail();
            RefreshHistory();
        });
    }

    private void ShowHistoryRecord(PlayHistory.Record record)
    {
        CloseDetail();
        back.gameObject.SetActive(false);
        detail = Overlay("HistoryDetail", screen.transform, 72, .98f);
        Label(detail.transform, ResultText(record), new Vector2(.5f, .94f), new Vector2(1650, 80), 38, true);
        Label(detail.transform, DateText(record) + "　｜　" + GameManager.GetPackDisplayName(Mathf.Clamp(record.pack, 0, 2)) +
            $"　｜　最終デッキ {record.deck.Count}枚", new Vector2(.5f, .85f), new Vector2(1650, 65), 25);
        var list = HistoryScroll(detail.transform, .23f, .79f, true);
        historyCardGrid = list.GetComponent<GridLayoutGroup>();
        foreach (var card in record.deck)
        {
            if (card == null) continue;
            var data = RestoreHistoryCard(card);
            var instance = card.pattern < 0 ? new CardInstance(data) : CardInstance.CreateCollectionPreview(data, card.pattern);
            var view = Instantiate(game.deckUIManager.cardUIPrefab, list).GetComponent<CardUIView>();
            view.Setup(instance);
        }
        var close = MakeButton(detail.transform, "もどる", new Vector2(.5f, .14f), new Vector2(240, 68), CloseDetail);
        BottomScreenButton.Apply(close);
    }

    private CardData RestoreHistoryCard(PlayHistory.Card card)
    {
        CardData source = null;
        for (int pack = 0; pack < 3 && source == null; pack++)
            source = game.deckManager.GetPool(pack).Find(item => item != null && item.name == card.assetName);
        var data = source != null ? Instantiate(source) : ScriptableObject.CreateInstance<CardData>();
        data.hideFlags = HideFlags.HideAndDontSave;
        data.name = card.assetName;
        data.cardName = card.name;
        data.description = card.description;
        // Keep the saved text/cost even if the original card is balanced later.
        var performance = new CardPerformance { cost = card.cost, effects = new List<BattleEffect>() };
        data.lowPerformance = data.mediumPerformance = data.highPerformance = performance;
        historyCardData.Add(data);
        return data;
    }

    private void ClearHistoryCardData()
    {
        if (historyCardData.Count > 0)
            foreach (var overlay in FindObjectsOfType<CardDetailOverlay>(true)) overlay.Close();
        foreach (var data in historyCardData) if (data != null) Destroy(data);
        historyCardData.Clear();
        historyCardGrid = null;
    }

    private void ResizeHistoryCards()
    {
        if (historyCardGrid == null) return;
        var rect = (RectTransform)historyCardGrid.transform;
        float width = Mathf.Clamp((rect.rect.width - historyCardGrid.padding.horizontal - historyCardGrid.spacing.x * 4) / 5, 80, 220);
        historyCardGrid.cellSize = new Vector2(width, width * 1.4f);
    }

    private static string ResultText(PlayHistory.Record record) => record.cleared ? "CLEAR　10連戦クリア" :
        $"GAME OVER　{record.battle}体目・{record.enemyName} に敗北";
    private static string DateText(PlayHistory.Record record)
    {
        DateTimeOffset date;
        return DateTimeOffset.TryParse(record.endedAt, out date) ? date.ToString("yyyy/MM/dd HH:mm") : record.endedAt;
    }
    private static RectTransform HistoryScroll(Transform parent, float bottom, float top, bool cards = false)
    {
        var frame = Rect("HistoryScroll", parent, new Vector2(.5f, .5f), Vector2.zero);
        frame.anchorMin = new Vector2(.07f, bottom); frame.anchorMax = new Vector2(.93f, top);
        var image = frame.gameObject.AddComponent<Image>(); image.color = new Color(.025f, .035f, .04f, .94f);
        var viewport = Rect("Viewport", frame, new Vector2(.5f, .5f), Vector2.zero);
        Stretch(viewport); viewport.offsetMin = new Vector2(14, 12); viewport.offsetMax = new Vector2(-14, -12);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        var list = Rect("Records", viewport, new Vector2(.5f, 1), Vector2.zero);
        list.anchorMin = new Vector2(0, 1); list.anchorMax = Vector2.one; list.pivot = new Vector2(.5f, 1);
        if (cards)
        {
            var grid = list.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
            grid.cellSize = new Vector2(220, 308); grid.spacing = new Vector2(24, 24);
            grid.padding = new RectOffset(24, 24, 24, 24); grid.childAlignment = TextAnchor.UpperCenter;
        }
        else
        {
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14; layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
        }
        list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scrollRect = frame.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport; scrollRect.content = list; scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 45; scrollRect.movementType = ScrollRect.MovementType.Clamped;
        // Keep a visible scroll handle for mouse, touch, and WebGL players.
        viewport.offsetMax = new Vector2(-34, -12);
        var track = Rect("Scrollbar", frame, new Vector2(1, .5f), new Vector2(12, -28));
        track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
        track.anchoredPosition = new Vector2(-15, 0);
        track.gameObject.AddComponent<Image>().color = new Color(.10f, .09f, .07f);
        var handle = Rect("Handle", track, new Vector2(.5f, .5f), Vector2.zero); Stretch(handle);
        var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(.81f, .65f, .39f);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop; scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
        scrollRect.verticalScrollbar = scrollbar;
        return list;
    }
    private static RectTransform HistoryRow(Transform parent, float height)
    {
        var row = Rect("Record", parent, new Vector2(.5f, .5f), Vector2.zero);
        var layout = row.gameObject.AddComponent<LayoutElement>(); layout.minHeight = layout.preferredHeight = height;
        row.gameObject.AddComponent<BattleHudGraphic>().color = new Color(.07f, .065f, .05f, .97f);
        return row;
    }
    private static void RowText(RectTransform row, string value, float left, float right, float fontSize)
    {
        var text = Label(row, value, new Vector2(.5f, .5f), Vector2.zero, fontSize);
        text.rectTransform.anchorMin = new Vector2(left, .06f); text.rectTransform.anchorMax = new Vector2(right, .94f);
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = fontSize;
    }
}
