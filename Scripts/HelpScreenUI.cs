using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Title help book. Enemy statistics use the same data as the battle.</summary>
public class HelpScreenUI : MonoBehaviour
{
    private HelpTextContent textContent;
    // Inspectorの編集内容を優先し、未登録の項目は既定文を使う。
    private string Copy(string key, string fallback)
    {
        if (textContent == null)
            textContent = Resources.Load<HelpTextContent>("HelpTextContent");
        return textContent != null ? textContent.Get(key, fallback) : fallback;
    }

    private GameManager game;
    private GameObject screen, menu;
    private bool menuWasActive;
    private bool openedFromPause;
    private RectTransform book;
    private ScrollRect scroll;
    private TMP_Text heading, body, counter;
    private Button previous, next;
    private readonly List<Button> chapters = new List<Button>();
    private readonly List<GameObject> chapterMarkers = new List<GameObject>();
    private readonly List<string> titles = new List<string>();
    private readonly List<string> pages = new List<string>();
    private sealed class EnemyHelpEntry
    {
        public EnemyData data;
        public bool boss;
        public int number;
    }

    private readonly Dictionary<int, List<EnemyHelpEntry>> enemyPages = new Dictionary<int, List<EnemyHelpEntry>>();
    private RectTransform enemyLayout;
    private int page;
    private readonly int[] chapterStarts = {0, 3, 5, 0};
    public bool IsOpen => screen != null;
    // 開閉とページ操作。タイトルから開く場合とポーズから開く場合を扱う。
    public void Configure(GameManager owner, bool bindTitleButton = true)
    {
        game = owner;
        if (!bindTitleButton)
            return;
        var target = game.startScreenRoot.transform.Find("MenuPanel/HelpButton");
        menu = target != null ? target.parent.gameObject : null;
        var button = target != null ? target.GetComponent<Button>() : null;
        if (button != null)
        {
            // Replace the old serialized menu action so it cannot hide the new screen.
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(Open);
        }
    }

    public void Open()
    {
        Open(false, game.startScreenRoot.transform);
    }

    public void OpenFromPause(Transform parent)
    {
        Open(true, parent);
    }

    private void Open(bool fromPause, Transform parent)
    {
        if (IsOpen)
            return;
        openedFromPause = fromPause;
        BuildPages();
        menuWasActive = !fromPause && menu != null && menu.activeSelf;
        if (!fromPause && menu != null)
            menu.SetActive(false);
        var root = Rect("HelpScreen", parent, Vector2.zero, Vector2.zero);
        Stretch(root);
        screen = root.gameObject;
        root.gameObject.AddComponent<Image>().color = new Color(.018f, .025f, .032f, .99f);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = fromPause ? 115 : 70;
        root.gameObject.AddComponent<GraphicRaycaster>();
        book = Rect("HelpBook", root, Vector2.zero, new Vector2(1600, 900));
        Text("Title", book, Copy("001 冒険の手引き", "冒険の手引き"), new Vector2(0, 388), new Vector2(1000, 64), 40, true);
        string[] labels = {Copy("002 ゲームの流れ", "ゲームの流れ"), Copy("003 詳しい仕様", "詳しい仕様"), Copy("004 状態・効果", "状態・効果"), Copy("005 敵図鑑", "敵図鑑")};
        chapters.Clear();
        chapterMarkers.Clear();
        for (int i = 0; i < labels.Length; i++)
        {
            int first = chapterStarts[i];
            chapters.Add(MakeButton(labels[i], book, new Vector2(-555 + i * 370, 302), new Vector2(342, 58), () => ShowPage(first)));
            var marker = Rect("SelectedChapter", chapters[i].transform, Vector2.zero, Vector2.zero);
            Stretch(marker);
            marker.offsetMin = new Vector2(-3, -3);
            marker.offsetMax = new Vector2(3, 3);
            var gold = marker.gameObject.AddComponent<BattleHudGraphic>();
            gold.color = new Color(.47f, .32f, .095f, .85f);
            gold.metal = new Color(1f, .85f, .40f);
            gold.glow = new Color(1f, .72f, .25f, .8f);
            gold.raycastTarget = false;
            marker.SetSiblingIndex(1);
            chapterMarkers.Add(marker.gameObject);
        }

        var panel = Rect("PageFrame", book, new Vector2(0, -17), new Vector2(1480, 546));
        var frame = panel.gameObject.AddComponent<BattleHudGraphic>();
        frame.color = new Color(.035f, .044f, .052f);
        frame.metal = new Color(.64f, .50f, .29f);
        frame.glow = new Color(.34f, .25f, .11f, .4f);
        frame.raycastTarget = false;
        heading = Text("PageTitle", panel, "", new Vector2(0, 221), new Vector2(1350, 52), 30, true);
        var area = Rect("ReadingArea", panel, new Vector2(-5, -28), new Vector2(1370, 420));
        scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 55;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = Rect("Viewport", area, Vector2.zero, Vector2.zero);
        Stretch(viewport);
        viewport.offsetMax = new Vector2(-24, 0);
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport, Vector2.zero, Vector2.zero);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = Vector2.zero;
        body = content.gameObject.AddComponent<TextMeshProUGUI>();
        GameScreenTextStyle.Apply(body);
        body.fontSize = 25;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Overflow;
        body.lineSpacing = 10;
        body.paragraphSpacing = 16;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        var track = Rect("ScrollTrack", area, new Vector2(679, 0), new Vector2(8, 420));
        track.gameObject.AddComponent<Image>().color = new Color(.10f, .10f, .09f);
        var handleArea = Rect("HandleArea", track, Vector2.zero, Vector2.zero);
        Stretch(handleArea);
        var handle = Rect("Handle", handleArea, Vector2.zero, Vector2.zero);
        Stretch(handle);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(.72f, .57f, .34f);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle;
        bar.targetGraphic = handleImage;
        scroll.verticalScrollbar = bar;
        previous = MakeButton(Copy("007 前のページ", "前のページ"), book, new Vector2(-550, -365), new Vector2(260, 64), () => ShowPage(page - 1));
        next = MakeButton(Copy("008 次のページ", "次のページ"), book, new Vector2(550, -365), new Vector2(260, 64), () => ShowPage(page + 1));
        MakeButton(Copy("009 もどる", "もどる"), book, new Vector2(0, -365), new Vector2(310, 64), Return);
        counter = Text("PageNumber", book, "", new Vector2(0, -315), new Vector2(800, 28), 18);
        Resize();
        ShowPage(0);
    }

    public void ShowPage(int index)
    {
        if (!IsOpen || index < 0 || index >= pages.Count)
            return;
        page = index;
        heading.text = titles[index];
        body.text = pages[index];
        if (enemyLayout != null)
        {
            enemyLayout.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(enemyLayout.gameObject);
            else
                DestroyImmediate(enemyLayout.gameObject);
            enemyLayout = null;
        }

        body.enabled = true;
        scroll.content.GetComponent<ContentSizeFitter>().enabled = true;
        Canvas.ForceUpdateCanvases();
        if (enemyPages.TryGetValue(index, out var entries))
            ShowEnemyPage(entries);
        counter.text = $"{index + 1} / {pages.Count}";
        previous.interactable = index > 0;
        next.interactable = index < pages.Count - 1;
        int chapter = 0;
        for (int i = 1; i < chapterStarts.Length; i++)
            if (index >= chapterStarts[i])
                chapter = i;
        for (int i = 0; i < chapterMarkers.Count; i++)
            chapterMarkers[i].SetActive(i == chapter);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        screen.SetActive(false);
        Destroy(screen);
        screen = null;
        if (!openedFromPause && menu != null)
            menu.SetActive(menuWasActive);
        chapters.Clear();
        chapterMarkers.Clear();
    }

    private void Return()
    {
        if (openedFromPause)
            Close();
        else
            game.ShowTitle();
    }

    private void Update()
    {
        if (!IsOpen)
            return;
        // Escape is owned by the global pause menu on every screen.
        if (!openedFromPause && game.PauseUI != null && game.PauseUI.IsPaused)
            return;
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            ShowPage(page - 1);
        if (Input.GetKeyDown(KeyCode.RightArrow))
            ShowPage(page + 1);
        Resize();
    }

    private void Resize()
    {
        if (book == null)
            return;
        var size = ((RectTransform)screen.transform).rect.size;
        book.localScale = Vector3.one * Mathf.Max(.01f, Mathf.Min(size.x / 1600, size.y / 900));
    }

    // 共通のUI部品生成。
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize, bool title = false)
    {
        var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        GameScreenTextStyle.Apply(text, title);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private static Button MakeButton(string label, Transform parent, Vector2 position, Vector2 size, UnityAction action)
    {
        var rect = Rect(label, parent, position, size);
        rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        var text = Text("Label", rect, label, Vector2.zero, size, 25);
        Stretch(text.rectTransform);
        ButtonDesign.Apply(button);
        return button;
    }

    // 章とページの登録。本文のキーはHelpTextContentの編集項目と対応する。
    private void AddPage(string title, string text)
    {
        titles.Add(title);
        pages.Add(text);
    }

    private static string Entry(string title, string text) => $"<color=#D9B878><b>{title}</b></color>\n{text}\n\n";
    private void BuildPages()
    {
        titles.Clear();
        pages.Clear();
        enemyPages.Clear();
        var balance = game.battleManager.balanceData;
        int draw = balance != null ? balance.handDrawCount : 5;
        int cost = balance != null ? balance.maxCost : 3;
        int maxDeck = balance != null ? balance.maxDeckSize : 20;
        int recovery = balance != null ? balance.hpRecoveryAmount : 20;
        int candidates = balance != null ? balance.rewardCardCandidateCount : 10;
        int rewards = balance != null ? balance.maxRewardCardSelect : 3;
        int hp = game.battleManager.playerStatus.maxHP;
        AddPage(Copy("002 ゲームの流れ", "ゲームの流れ") + "　1/3", Entry(Copy("flow.01.title パック選択", "01　パック選択"), Copy("flow.01.body パック選択", "パックを選ぶと、カードプールが決まります。各パックのカードプールはコレクションで確認できます。")) + Entry(Copy("flow.02.title 初期デッキ", "02　初期デッキ"), Copy("flow.02.body 初期デッキ", "攻撃5枚・防御2枚・バフ2枚・デバフ1枚の、合計10枚で開始します。同じカードが複数枚入る場合があります。")) + Entry(Copy("flow.03.title カード性能の確定", "03　カード性能の確定"), Copy("flow.03.body カード性能の確定", "カードを初めて引くと、コストと効果が確定します。未確定の値は「?」で表示されます。")));
        AddPage(Copy("002 ゲームの流れ", "ゲームの流れ") + "　2/3", Entry(Copy("flow.04.title ターン開始", "04　ターン開始"), Copy("flow.04.body ターン開始", "自分のターンから戦闘を開始します。基本{draw}枚を引き、使用できるコストは基本{cost}です。").Replace("{draw}", draw.ToString()).Replace("{cost}", cost.ToString())) + Entry(Copy("flow.05.title カード使用", "05　カード使用"), Copy("flow.05.body カード使用", "手札を左クリックすると、コストを支払い、効果を上から順に発動します。使用したカードは墓地に移動します。")) + Entry(Copy("flow.06.title ターン終了", "06　ターン終了"), Copy("flow.06.body ターン終了", "「ターン終了」を押すと、残りの手札が墓地に移動し、敵が行動します。敵の行動後、次の自分のターンが始まります。")));
        AddPage(Copy("002 ゲームの流れ", "ゲームの流れ") + "　3/3", Entry(Copy("flow.07.title 戦闘終了", "07　戦闘終了"), Copy("flow.07.body 戦闘終了", "敵のHPが0になると勝利、自分のHPが0になると敗北します。")) + Entry(Copy("flow.08.title 勝利報酬", "08　勝利報酬"), Copy("flow.08.body 勝利報酬", "勝利後は、カード獲得・カード削除・HP回復から1つを選びます。カード獲得は候補{candidates}枚から最大{rewards}枚、カード削除は1枚、HP回復は{recovery}です。").Replace("{candidates}", candidates.ToString()).Replace("{rewards}", rewards.ToString()).Replace("{recovery}", recovery.ToString())) + Entry(Copy("flow.09.title 冒険終了", "09　冒険終了"), Copy("flow.09.body 冒険終了", "1～9戦目は通常敵、10戦目はボスと戦います。10戦目に勝利するとゲームクリアです。敗北すると冒険が終了します。")));
        AddPage(Copy("003 詳しい仕様", "詳しい仕様") + "　1/2", Entry(Copy("rules.01.title ドロー", "01　ドロー"), Copy("rules.01.body ドロー", "毎ターン、山札から基本{draw}枚をランダムに引きます。山札が空になると墓地のカードを山札に戻します。山札・墓地の両方が空になると、ドローを終了します。").Replace("{draw}", draw.ToString())) + Entry(Copy("rules.02.title コスト", "02　コスト"), Copy("rules.02.body コスト", "毎ターン、使用できるコストは基本{cost}に戻ります。残りコストは通常持ち越しません。使用コストの下限は0です。コスト回復により、基本値を超える場合があります。").Replace("{cost}", cost.ToString())) + Entry(Copy("rules.03.title カード性能", "03　カード性能"), Copy("rules.03.body カード性能", "コストと効果は、カード1枚ごとに3パターンから等確率で決まります。同じカード名でも別々に決まり、その冒険が終わるまで変わりません。")) + Entry(Copy("rules.04.title レアリティ", "04　レアリティ"), Copy("rules.04.body レアリティ", "カードのふちの色が、灰色ならコモン、緑色ならアンコモン、金色ならレア、虹色ならスーパーレアです。\nコモン50％・アンコモン35％・レア10％・スーパーレア5％で抽選します。該当するカードがないレアリティの確率は、コモンに加算します。")));
        AddPage(Copy("003 詳しい仕様", "詳しい仕様") + "　2/2", Entry(Copy("rules.05.title デッキ", "05　デッキ"), Copy("rules.05.body デッキ", "上限は{maxDeck}枚です。上限に達している場合、既存の1枚と入れ替えて獲得します。カード獲得・カード削除は見送ることもできます。").Replace("{maxDeck}", maxDeck.ToString())) + Entry(Copy("rules.06.title 戦闘間の引き継ぎ", "06　戦闘間の引き継ぎ"), Copy("rules.06.body 戦闘間の引き継ぎ", "HP・デッキ・確定したカード性能を引き継ぎます。状態・戦闘中のカードの成長・一時的なコスト軽減はリセットされます。HPは自動回復しません。最大HPは{hp}です。").Replace("{hp}", hp.ToString())) + Entry(Copy("rules.07.title カード一覧", "07　カード一覧"), Copy("rules.07.body カード一覧", "山札・墓地のアイコンから、各カードの一覧を確認できます。コレクションでは、各カードの3パターンを確認できます。")) + Entry(Copy("rules.08.title ポーズ・設定", "08　ポーズ・設定"), Copy("rules.08.body ポーズ・設定", "右上のポーズボタンまたはEscで、ポーズ画面を開きます。ヘルプ・音量設定を開くことができます。BGM・効果音の音量は0～100で設定し、次回起動時も引き継ぎます。")));
        BuildStatusPages();
        chapterStarts[3] = pages.Count;
        BuildEnemyPages();
    }

    private void BuildStatusPages()
    {
        AddPage(Copy("status.page.1 状態・効果　1/5", "状態・効果　1/5"), Entry(Copy("status.01.title ブロック", "01　ブロック"), Copy("status.01.body ブロック", "毒以外のダメージを肩代わりし、受け止めた分だけ減ります。出血・感電のダメージも肩代わりします。ターン経過では減りません。")) + Entry(Copy("status.02.title 攻撃力アップ／ダウン", "02　攻撃力アップ／ダウン"), Copy("status.02.body 攻撃力アップ／ダウン", "直接攻撃で与えるダメージが、表示された値だけ増減します。各ヒットに適用されます。毒・出血・感電・固定反撃には適用されません。")) + Entry(Copy("status.03.title 受けるダメージアップ／ダウン", "03　受けるダメージアップ／ダウン"), Copy("status.03.body 受けるダメージアップ／ダウン", "直接攻撃・固定反撃で受けるダメージが、表示された値だけ増減します。各ヒットに適用されます。毒・出血・感電には適用されません。ブロックで受け止める前に計算します。")) + Entry(Copy("status.04.title カードから受けるダメージアップ", "04　カードから受けるダメージアップ"), Copy("status.04.body カードから受けるダメージアップ", "カードによる直接攻撃で受けるダメージが、表示された値だけ増えます。各ヒットに適用されます。毒・出血・感電・固定反撃には適用されません。ブロックで受け止める前に計算します。")) + Entry(Copy("status.05.title 次の攻撃ダメージ軽減", "05　次の攻撃ダメージ軽減"), Copy("status.05.body 次の攻撃ダメージ軽減", "次の直接攻撃で与えるダメージが、表示された値だけ減ります。1ヒットに適用され、完全にブロックされても消費されます。ターン経過では消えません。")));
        AddPage(Copy("status.page.2 状態・効果　2/5", "状態・効果　2/5"), Entry(Copy("status.06.title 毒", "06　毒"), Copy("status.06.body 毒", "自分に付いた毒は敵のターン終了時、敵に付いた毒は自分のターン終了時に発動します。毒の値の合計だけダメージを受けます。ブロックを無視します。発動するたびに、付与ごとの残りターンが1減ります。")) + Entry(Copy("status.07.title 出血", "07　出血"), Copy("status.07.body 出血", "相手への直接攻撃の後、出血の値の合計だけダメージを受けます。各ヒットで発動します。ブロックで受け止められます。発動するたびに、付与ごとの値が表示された減少量だけ減ります。ターン経過では減りません。")) + Entry(Copy("status.08.title 感電", "08　感電"), Copy("status.08.body 感電", "直接攻撃を受けた後、感電の値の合計だけダメージを受けます。自分の感電は敵の直接攻撃、敵の感電は攻撃カテゴリーのカードによる直接攻撃で発動します。各ヒットで発動します。ブロックで受け止められます。発動するたびに、付与ごとの値が表示された減少量だけ減ります。ターン経過では減りません。")) + Entry(Copy("status.09.title 付与する毒の継続ターンダウン", "09　付与する毒の継続ターンダウン"), Copy("status.09.body 付与する毒の継続ターンダウン", "新しく付与する毒の継続ターンが、表示された値だけ減ります。付与済みの毒には適用されません。毒反撃で付与する毒には適用されません。")) + Entry(Copy("status.10.title カードの感電値アップ", "10　カードの感電値アップ"), Copy("status.10.body カードの感電値アップ", "カード・種類連携で新しく付与する感電の値が、表示された値だけ増えます。付与済みの感電には適用されません。付与済みの感電を増幅する量には適用されません。")));
        AddPage(Copy("status.page.3 状態・効果　3/5", "状態・効果　3/5"), Entry(Copy("status.11.title カードのブロック獲得量アップ／ダウン", "11　カードのブロック獲得量アップ／ダウン"), Copy("status.11.body カードのブロック獲得量アップ／ダウン", "カードの効果で得るブロックの量が、表示された値だけ増減します。カードのカテゴリーにかかわらず適用されます。ターン終了時ブロックには適用されません。獲得量の下限は0です。")) + Entry(Copy("status.12.title ブロック獲得量ダウン", "12　ブロック獲得量ダウン"), Copy("status.12.body ブロック獲得量ダウン", "新しく得るブロックの量が、表示された値だけ減ります。獲得済みのブロックには適用されません。獲得量の下限は0です。")) + Entry(Copy("status.13.title ターン終了時ブロック", "13　ターン終了時ブロック"), Copy("status.13.body ターン終了時ブロック", "自分のターン終了時に、表示された値のブロックを得ます。カードのブロック獲得量アップ／ダウンは適用されません。")) + Entry(Copy("status.14.title カードの使用コストアップ", "14　カードの使用コストアップ"), Copy("status.14.body カードの使用コストアップ", "カードの使用コストが、表示された値だけ増えます。軽量化・逆境による減少量と合算します。使用コストの下限は0です。")) + Entry(Copy("status.15.title 軽量化", "15　軽量化"), Copy("status.15.body 軽量化", "次に使用する指定枚数のカードの使用コストが1減ります。重ね掛けは付与ごとに適用され、残り枚数も付与ごとに数えます。使用コストが0でも残り枚数が1減ります。ターン経過では減りません。使用コストの下限は0です。")) + Entry(Copy("status.16.title 逆境", "16　逆境"), Copy("status.16.body 逆境", "HPが表示された値以下の間、カードの使用コストが1減ります。条件を満たさない間は適用されません。重ね掛けは付与ごとに適用されます。使用コストの下限は0です。")));
        AddPage(Copy("status.page.4 状態・効果　4/5", "状態・効果　4/5"), Entry(Copy("status.17.title 次ターンからドロー追加", "17　次ターンからドロー追加"), Copy("status.17.body 次ターンからドロー追加", "次の自分のターンから、ターン開始時に引くカードの枚数が表示された値だけ増えます。自分のターン開始時に、付与ごとの残りターンが1減ります。")) + Entry(Copy("status.18.title 次ターンコスト追加", "18　次ターンコスト追加"), Copy("status.18.body 次ターンコスト追加", "次の自分のターン開始時に、使用できるコストが表示された値だけ増えます。重ね掛けは付与ごとに適用されます。1回発動すると消えます。")) + Entry(Copy("status.19.title 残りコスト温存", "19　残りコスト温存"), Copy("status.19.body 残りコスト温存", "自分のターン終了時の残りコストを、次の自分のターン開始時のコストに加算します。重ね掛けしても加算量は増えず、残りターンが長い方を維持します。")) + Entry(Copy("status.20.title 連鎖強化", "20　連鎖強化"), Copy("status.20.body 連鎖強化", "そのターンのカード使用枚数に表示された値を掛けた分だけ、攻撃力が増えます。この状態を得る際のカード使用は数えません。次の自分のターン開始時に、使用枚数が0に戻ります。")) + Entry(Copy("status.21.title 循環", "21　循環"), Copy("status.21.body 循環", "この状態を得た後、指定枚数のカードを使用するたびに1枚引きます。使用枚数はターンをまたいで持ち越します。重ね掛けは付与ごとに適用され、使用枚数も付与ごとに数えます。")) + Entry(Copy("status.22.title コスト還元", "22　コスト還元"), Copy("status.22.body コスト還元", "この状態を得た後、指定枚数のカードを使用するたびにコストが1回復します。使用枚数はターンをまたいで持ち越します。重ね掛けは付与ごとに適用され、使用枚数も付与ごとに数えます。")) + Entry(Copy("status.23.title 種類連携", "23　種類連携"), Copy("status.23.body 種類連携", "この状態を得た後、同じターンに異なる3カテゴリーのカードを使用するたびに、敵に感電を付与します。発動時と次の自分のターン開始時に、カテゴリーの記録がリセットされます。")));
        AddPage(Copy("status.page.5 状態・効果　5/5", "状態・効果　5/5"), Entry(Copy("status.24.title 反撃", "24　反撃"), Copy("status.24.body 反撃", "敵の直接攻撃を受けた後、敵に固定ダメージを与えます。各ヒットで発動します。完全にブロックしても発動します。毒・出血・感電・自傷では発動しません。攻撃力アップ／ダウンは適用されません。")) + Entry(Copy("status.25.title 毒反撃", "25　毒反撃"), Copy("status.25.body 毒反撃", "敵の直接攻撃を受けた後、敵に毒を付与します。各ヒットで発動します。完全にブロックしても発動します。毒・出血・感電・自傷では発動しません。")) + Entry(Copy("status.26.title 出血の盾", "26　出血の盾"), Copy("status.26.body 出血の盾", "敵の直接攻撃を受けた後、敵に出血を付与します。各ヒットで発動します。完全にブロックしても発動します。毒・出血・感電・自傷では発動しません。付与した出血は、敵の次の直接攻撃から発動します。")) + Entry(Copy("status.27.title 弱体の盾", "27　弱体の盾"), Copy("status.27.body 弱体の盾", "敵の直接攻撃を受けた後、敵に攻撃力ダウンを付与します。各ヒットで発動します。完全にブロックしても発動します。毒・出血・感電・自傷では発動しません。")) + Entry(Copy("status.28.title HP被害時の報復", "28　HP被害時の報復"), Copy("status.28.body HP被害時の報復", "敵の直接攻撃でHPが減った後、敵に固定ダメージを与えます。各ヒットで発動します。完全にブロックした場合は発動しません。毒・出血・感電・自傷では発動しません。攻撃力アップ／ダウンは適用されません。")) + Entry(Copy("status.29.title 報復体制", "29　報復体制"), Copy("status.29.body 報復体制", "敵の直接攻撃で減ったHPを表示された値で割り、切り上げた分だけ攻撃力アップを得ます。各ヒットで発動します。完全にブロックした場合は発動しません。毒・出血・感電・自傷では発動しません。")) + Entry(Copy("status.30.title 攻撃後に出血", "30　攻撃後に出血"), Copy("status.30.body 攻撃後に出血", "相手への直接攻撃の後、出血を得ます。各ヒットで発動します。得た出血は、次の直接攻撃から発動します。")) + Entry(Copy("status.31.title 攻撃後に攻撃力ダウン", "31　攻撃後に攻撃力ダウン"), Copy("status.31.body 攻撃後に攻撃力ダウン", "相手への直接攻撃の後、攻撃力ダウンを得ます。各ヒットで発動します。")));
    }

    private void BuildEnemyPages()
    {
        var manager = game.battleManager.enemyManager;
        var normals = manager.normalEnemies.FindAll(e => e != null);
        var bosses = manager.bossEnemies.FindAll(e => e != null);
        // Split all registered normal enemies across two pages, then show all bosses.
        int split = (normals.Count + 1) / 2;
        var first = new List<EnemyHelpEntry>();
        var second = new List<EnemyHelpEntry>();
        for (int i = 0; i < normals.Count; i++)
        {
            var entry = new EnemyHelpEntry{data = normals[i], boss = false, number = i + 1};
            (i < split ? first : second).Add(entry);
        }

        AddEnemyPage(Copy("005 敵図鑑", "敵図鑑") + "　1/3", first);
        AddEnemyPage(Copy("005 敵図鑑", "敵図鑑") + "　2/3", second);
        var last = new List<EnemyHelpEntry>();
        for (int i = 0; i < bosses.Count; i++)
            last.Add(new EnemyHelpEntry{data = bosses[i], boss = true, number = normals.Count + i + 1});
        AddEnemyPage(Copy("005 敵図鑑", "敵図鑑") + "　3/3", last);
    }

    private void AddEnemyPage(string title, List<EnemyHelpEntry> entries)
    {
        enemyPages[pages.Count] = entries;
        AddPage(title, "");
    }

    // 敵図鑑の画像と本文の配置。
    private void ShowEnemyPage(List<EnemyHelpEntry> entries)
    {
        const float portraitSize = 224f, gap = 28f, titleHeight = 44f;
        body.enabled = false;
        scroll.content.GetComponent<ContentSizeFitter>().enabled = false;
        float width = scroll.viewport.rect.width;
        float descriptionWidth = width - portraitSize - gap;
        enemyLayout = Rect("EnemyEntries", scroll.content, Vector2.zero, Vector2.zero);
        enemyLayout.anchorMin = new Vector2(0, 1);
        enemyLayout.anchorMax = Vector2.one;
        enemyLayout.pivot = new Vector2(.5f, 1);
        float y = 0;
        foreach (var entry in entries)
        {
            var title = Text("EnemyTitle", enemyLayout, $"{entry.number:00}　{entry.data.enemyName}", Vector2.zero, new Vector2(width, titleHeight), 27, true);
            PlaceEnemyElement(title.rectTransform, 0, y, width, titleHeight);
            title.alignment = TextAlignmentOptions.TopLeft;
            y += titleHeight + 8f;
            var portrait = Rect("EnemyPortrait", enemyLayout, Vector2.zero, new Vector2(portraitSize, portraitSize));
            PlaceEnemyElement(portrait, 0, y, portraitSize, portraitSize);
            var image = portrait.gameObject.AddComponent<Image>();
            image.sprite = entry.data.helpPortrait;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = image.sprite != null;
            var description = Text("EnemyDescription", enemyLayout, EnemyDescription(entry.data, entry.boss), Vector2.zero, new Vector2(descriptionWidth, portraitSize), 25);
            description.alignment = TextAlignmentOptions.TopLeft;
            description.enableWordWrapping = true;
            description.overflowMode = TextOverflowModes.Overflow;
            description.lineSpacing = body.lineSpacing;
            description.paragraphSpacing = body.paragraphSpacing;
            float height = Mathf.Max(portraitSize, description.GetPreferredValues(description.text, descriptionWidth, Mathf.Infinity).y);
            PlaceEnemyElement(description.rectTransform, portraitSize + gap, y, descriptionWidth, height);
            y += height + 32f;
        }

        enemyLayout.sizeDelta = new Vector2(0, y);
        scroll.content.sizeDelta = new Vector2(0, y);
    }

    private static void PlaceEnemyElement(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // 敵データから図鑑の説明文を生成する。
    private string EnemyDescription(EnemyData data, bool boss)
    {
        int minHP = Mathf.RoundToInt(data.baseMaxHP * Mathf.Pow(data.scalingFactor, boss ? 9 : 0));
        int maxHP = Mathf.RoundToInt(data.baseMaxHP * Mathf.Pow(data.scalingFactor, boss ? 9 : 8));
        var text = new StringBuilder();
        text.Append("HP ").Append(minHP == maxHP ? minHP.ToString() : $"{minHP}～{maxHP}").Append("　／　毎ターン ").Append(Mathf.Max(1, data.actionsPerTurn)).Append("回行動\n");
        if (data.reactsToPlayerCards)
        {
            text.Append("使用枚数が最も多いカテゴリーに応じて行動します。同数の場合、最後に使用したカテゴリーを優先します。\n");
            string[] categories = {"攻撃", "防御", "バフ", "デバフ"};
            for (int i = 0; i < data.categoryActions.Count; i++)
                text.Append(i < categories.Length ? categories[i] : "その他").Append("中心：").Append(ActionText(data.categoryActions[i])).Append('\n');
            foreach (var action in data.actionPattern)
                text.Append("カード未使用：").Append(ActionText(action)).Append('\n');
        }
        else
        {
            for (int i = 0; i < data.actionPattern.Count; i++)
                text.Append("行動").Append((i + 1).ToString("00")).Append("：").Append(ActionText(data.actionPattern[i])).Append('\n');
        }

        return text.ToString();
    }

    private static string ActionText(EnemyActionData action)
    {
        if (action == null)
            return "—";
        var effects = new List<string>();
        foreach (var effect in action.effects)
        {
            string value = effect.value.ToString();
            if (effect.valuePerExtraCard != 0)
                value += $"＋追加使用枚数×{effect.valuePerExtraCard}";
            string target = effect.target == EffectTarget.Self ? "敵自身" : "プレイヤー";
            switch (effect.effectType)
            {
                case BattleEffectType.Damage:
                    effects.Add($"{target}に{value}ダメージ");
                    break;
                case BattleEffectType.GainBlock:
                    effects.Add($"{target}が{value}ブロックを得る");
                    break;
                case BattleEffectType.ApplyPoison:
                    effects.Add($"{target}に毒{value}を付与（{effect.duration}ターン）");
                    break;
                case BattleEffectType.PermanentAttackBonus:
                    effects.Add($"{target}に攻撃力{value}アップを付与（戦闘中）");
                    break;
                case BattleEffectType.IncomingDamageModifier:
                    effects.Add(ModifierText(target, "受けるダメージ", effect));
                    break;
                case BattleEffectType.OutgoingDamageModifier:
                    effects.Add(ModifierText(target, "攻撃力", effect));
                    break;
                case BattleEffectType.CardBlockBonus:
                    effects.Add(ModifierText(target, "カードのブロック獲得量", effect));
                    break;
                case BattleEffectType.CardCostModifier:
                    effects.Add(ModifierText(target, "カードの使用コスト", effect));
                    break;
                case BattleEffectType.OutgoingPoisonDurationModifier:
                    effects.Add(ModifierText(target, "付与する毒の継続ターン", effect));
                    break;
                case BattleEffectType.ConsumeBlockDamage:
                    effects.Add($"自身の全ブロックを消費し、その量＋{value}ダメージ");
                    break;
                case BattleEffectType.LifestealDamage:
                    effects.Add($"{value}ダメージ、HP被害の{effect.duration}％を回復（切り捨て）");
                    break;
                default:
                    effects.Add($"{effect.effectType} {value}");
                    break;
            }
        }

        return action.actionName + "：" + string.Join(" → ", effects);
    }

    private static string ModifierText(string target, string name, BattleEffect effect)
    {
        string direction = effect.value >= 0 ? "アップ" : "ダウン";
        return $"{target}に{name}{Mathf.Abs(effect.value)}{direction}を付与（{effect.duration}ターン）";
    }
}
