using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bindings for the designed battle HUD. All visual objects are authored in the scene.</summary>
public class BattleHudUI : MonoBehaviour
{
    public GameManager game;
    public BattleManager battle;
    public TextMeshProUGUI battleCount, enemyName;
    private TMP_Text playerName;
    public CanvasGroup banner;
    public BattleHudGraphic bannerFrame;
    public Button pauseButton, resumeButton, titleButton;
    public GameObject pausePanel;
    public HealthBarUI playerHealth, enemyHealth;
    public RectTransform playerStatusRows, enemyStatusRows, costGems;
    public TextMeshProUGUI statusRowTemplate;
    public BattleHudGraphic gemTemplate;
    readonly List<TextMeshProUGUI> playerRows = new List<TextMeshProUGUI>();
    readonly List<TextMeshProUGUI> enemyRows = new List<TextMeshProUGUI>();
    readonly List<BattleHudGraphic> gems = new List<BattleHudGraphic>();
    string previousBanner, previousPlayerStatus, previousEnemyStatus;
    int cachedBattleNumber;
    string cachedBattleCountText;
    float bannerElapsed;
    public bool IsPaused => game != null && game.PauseUI != null && game.PauseUI.IsPaused;

    void Awake()
    {
        playerName = playerHealth.transform.Find("Name").GetComponent<TMP_Text>();
        if (battle != null && battle.intentText != null && battle.intentText.transform.parent.name == "EnemyIntent")
        {
            var frame = battle.intentText.transform.parent as RectTransform;
            frame.sizeDelta = new Vector2(frame.sizeDelta.x, 156);
            var content = battle.intentText.rectTransform;
            content.sizeDelta = new Vector2(content.sizeDelta.x, 104);
        }
        // Pause is provided by the always-active global menu.
        pauseButton.gameObject.SetActive(false);
        pausePanel.SetActive(false);
        statusRowTemplate.transform.parent.gameObject.SetActive(false);
        gemTemplate.gameObject.SetActive(false);
        banner.alpha = 0;
        banner.blocksRaycasts = false;
        banner.interactable = false;
    }
    void OnEnable() { previousBanner = null; previousPlayerStatus = null; previousEnemyStatus = null; }
    void LateUpdate()
    {
        playerName.text = GameManager.GetPackDisplayName(battle.SelectedPackIndex);
        // Reuse the label while the battle number is unchanged; still assign it each update.
        if (cachedBattleCountText == null || cachedBattleNumber != battle.currentBattleCount)
        {
            cachedBattleNumber = battle.currentBattleCount;
            cachedBattleCountText = $"{cachedBattleNumber} / 10 連戦目";
        }
        battleCount.text = cachedBattleCountText;
        var enemy = battle.enemyManager.CurrentEnemy;
        enemyName.text = enemy != null && enemy.Data != null ? enemy.Data.enemyName : "敵";
        UpdateStatuses(playerHealth, playerStatusRows, playerRows, ref previousPlayerStatus);
        UpdateStatuses(enemyHealth, enemyStatusRows, enemyRows, ref previousEnemyStatus);
        int maximum = battle.balanceData != null ? battle.balanceData.maxCost : 3;
        int remaining = battle.playerStatus != null ? battle.playerStatus.currentCost : 0;
        maximum = Mathf.Max(maximum, remaining);
        while (gems.Count < maximum)
        {
            var gem = Instantiate(gemTemplate, costGems); gem.gameObject.SetActive(true); gems.Add(gem);
        }
        for (int i = 0; i < gems.Count; i++)
        {
            gems[i].gameObject.SetActive(i < maximum);
            Color color = i < remaining ? new Color(.06f,.65f,1) : new Color(.035f,.07f,.11f);
            Color metal = i < remaining ? new Color(.5f,.9f,1) : new Color(.17f,.28f,.36f);
            if (!gems[i].color.Equals(color) || !gems[i].metal.Equals(metal))
            {
                gems[i].color = color;
                gems[i].metal = metal;
                gems[i].SetVerticesDirty();
            }
        }
        string message = battle.turnText.text;
        if (!string.IsNullOrEmpty(message) && message != previousBanner)
        {
            previousBanner = message; bannerElapsed = 0;
            bool enemyTurn = battle.State == BattleTurnState.Enemy;
            bannerFrame.color = enemyTurn ? new Color(.19f,.02f,.035f,.96f) : new Color(.015f,.07f,.15f,.96f);
            bannerFrame.metal = enemyTurn ? new Color(1,.28f,.25f) : new Color(.28f,.73f,1);
            bannerFrame.glow = enemyTurn ? new Color(1,.1f,.1f,.9f) : new Color(.1f,.55f,1,.9f);
            bannerFrame.SetVerticesDirty();
        }
        if (string.IsNullOrEmpty(message)) previousBanner = null;
        if (!IsPaused) bannerElapsed += Time.deltaTime;
        // Fade before the manager clears short turn announcements, and expire action/result banners.
        float duration = battle.State == BattleTurnState.PlayerIntro || message == "敵のターン"
            ? Mathf.Max(.3f, battle.turnBannerDuration) : 2.4f;
        banner.alpha = string.IsNullOrEmpty(message) ? 0 : Mathf.Min(Mathf.Clamp01(bannerElapsed/.12f), Mathf.Clamp01((duration-bannerElapsed)/.25f));
    }
    void UpdateStatuses(HealthBarUI health, RectTransform container, List<TextMeshProUGUI> rows, ref string previous)
    {
        string value = health.statusText.text;
        if (value == previous) return;
        previous = value;
        string[] lines = string.IsNullOrEmpty(value) ? new string[0] : value.Split('\n');
        while (rows.Count < lines.Length)
        {
            var rowObject = Instantiate(statusRowTemplate.transform.parent.gameObject, container);
            rowObject.SetActive(true); rows.Add(rowObject.GetComponentInChildren<TextMeshProUGUI>());
        }
        for (int i=0;i<rows.Count;i++)
        {
            rows[i].transform.parent.gameObject.SetActive(i < lines.Length);
            if (i < lines.Length) rows[i].text = lines[i].Replace("、あと", "   あと");
        }
    }
    public void Pause() { if (game.PauseUI != null) game.PauseUI.Pause(); }
    public void Resume() { if (game.PauseUI != null) game.PauseUI.Resume(); }
    public void ReturnToTitle() { if (game.PauseUI != null) game.PauseUI.ConfirmReturnToTitle(); }
}
