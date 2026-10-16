using TMPro;
using DG.Tweening;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>プレイヤーまたは現在の敵のHPを、左から伸びる緑ゲージと数値で表示します。</summary>
public class HealthBarUI : MonoBehaviour
{
    public enum HealthSource { Player, CurrentEnemy }

    public HealthSource source;
    [Header("参照")]
    public PlayerStatus playerStatus;
    public EnemyManager enemyManager;
    [Tooltip("緑ゲージの背面に置く黒い Image")]
    public Image blackBackgroundImage;
    [Tooltip("Image Type を Filled、Fill Method を Horizontal、Origin を Left にします")]
    public Image greenFillImage;
    public TextMeshProUGUI hpText;
    public bool showHPLabel;
    [Tooltip("バフ・デバフ・状態異常の表示先。未設定ならHPバーの下に自動生成します")]
    public TextMeshProUGUI statusText;
    [Header("色")]
    public Color healthyColor = new Color(0.15f, 0.8f, 0.2f, 1f);
    public Color emptyColor = Color.black;
    [Header("アニメーション")]
    [Min(0.05f)] public float changeDuration = 0.45f;

    public bool IsAnimating { get; private set; }
    private float displayedRatio;
    private Tween ratioTween;

    private void Awake()
    {
        EnsureStatusText();
        if (greenFillImage != null)
        {
            greenFillImage.type = Image.Type.Simple;
            greenFillImage.color = healthyColor;
        }
        if (blackBackgroundImage != null) blackBackgroundImage.color = emptyColor;
        RefreshImmediate();
    }

    private void OnDisable()
    {
        ratioTween?.Kill();
        IsAnimating = false;
    }

    public void RefreshImmediate()
    {
        GetHealth(out int current, out int maximum);
        ratioTween?.Kill();
        IsAnimating = false;
        displayedRatio = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
        ApplyRatio(displayedRatio);
        SetText(current, maximum);
        RefreshStatus();
    }

    public void AnimateToCurrent()
    {
        GetHealth(out int current, out int maximum);
        float targetRatio = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
        SetText(current, maximum);
        RefreshStatus();
        ratioTween?.Kill();
        if (Mathf.Approximately(displayedRatio, targetRatio))
        {
            ApplyRatio(targetRatio);
            IsAnimating = false;
            return;
        }

        IsAnimating = true;
        ratioTween = DOTween.To(() => displayedRatio, value =>
        {
            displayedRatio = value;
            ApplyRatio(value);
        }, targetRatio, changeDuration).SetEase(Ease.OutCubic).OnComplete(() => IsAnimating = false);
    }

    private void GetHealth(out int current, out int maximum)
    {
        if (source == HealthSource.Player && playerStatus != null)
        {
            current = playerStatus.currentHP;
            maximum = playerStatus.maxHP;
            return;
        }
        EnemyInstance enemy = enemyManager != null ? enemyManager.CurrentEnemy : null;
        current = enemy != null ? enemy.currentHP : 0;
        maximum = enemy != null ? enemy.maxHP : 0;
    }

    private void ApplyRatio(float ratio)
    {
        if (greenFillImage != null)
        {
            RectTransform fillRect = greenFillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(ratio, 1f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;
            greenFillImage.enabled = ratio > 0f;
        }
    }

    private void SetText(int current, int maximum)
    {
        if (hpText != null) hpText.text = (showHPLabel ? "HP " : string.Empty) + $"{current}/{maximum}";
    }

    public void RefreshStatus()
    {
        EnsureStatusText();
        if (statusText == null) return;

        PoisonStatus poison = null;
        DecayingHitStatus bleeding = null, electrocution = null;
        BattleModifiers modifiers = null;
        if (source == HealthSource.Player && playerStatus != null)
        {
            poison = playerStatus.poison;
            bleeding = playerStatus.bleeding; electrocution = playerStatus.electrocution;
            modifiers = playerStatus.modifiers;
        }
        else
        {
            EnemyInstance enemy = enemyManager != null ? enemyManager.CurrentEnemy : null;
            if (enemy != null) { poison = enemy.poison; modifiers = enemy.modifiers;
                bleeding = enemy.bleeding; electrocution = enemy.electrocution; }
        }

        StringBuilder text = new StringBuilder();
        int block = 0;
        if (source == HealthSource.Player && playerStatus != null)
            block = playerStatus.currentBlock;
        else
        {
            EnemyInstance enemy = enemyManager != null ? enemyManager.CurrentEnemy : null;
            if (enemy != null) block = enemy.currentBlock;
        }
        if (block > 0) text.Append("ブロック").Append(block);
        if (source != HealthSource.Player && enemyManager != null && enemyManager.CurrentEnemy != null
            && enemyManager.CurrentEnemy.PermanentAttackBonus > 0)
        {
            if (text.Length > 0) text.Append('\n');
            text.Append("攻撃力アップ ").Append(enemyManager.CurrentEnemy.PermanentAttackBonus).Append("（戦闘中）");
        }
        if (modifiers != null)
            foreach (TimedModifier modifier in modifiers.Active)
            {
                if (modifier.effectType == BattleEffectType.NextAttackReduction)
                {
                    if (text.Length > 0) text.Append('\n');
                    text.Append("次の攻撃ダメージ軽減").Append(modifier.value).Append("（1ヒット）");
                }
                else AppendLine(text, GetModifierLabel(modifier), modifier.remainingTurns);
            }
        if (poison != null)
            foreach (PoisonStatus.Stack stack in poison.Stacks)
                AppendLine(text, $"毒{stack.damagePerTurn}ダメージ", stack.remainingTurns);
        foreach (var entry in new[] { bleeding, electrocution })
            if (entry != null)
                foreach (var stack in entry.Stacks)
                {
                    if (text.Length > 0) text.Append('\n');
                    text.Append(entry == bleeding ? "出血" : "感電").Append(stack.value)
                        .Append("（発動ごと−").Append(stack.decayPerTrigger).Append('）');
                }
        if (source == HealthSource.Player && playerStatus != null)
        {
            var status = playerStatus.cardStatus;
            foreach (var buff in status.blockBonuses) AppendLine(text, $"カードのブロック獲得量{SignedDirection(buff.value)}", buff.remainingTurns);
            foreach (var buff in status.endTurnBlocks) AppendLine(text, $"ターン終了時ブロック+{buff.value}", buff.remainingTurns);
            foreach (var buff in status.extraDraws) AppendLine(text, $"次ターンからドロー追加 {buff.value}枚", buff.remainingTurns);
            foreach (var reaction in status.reactions)
            {
                string label = reaction.poisonTurns > 0 ? $"毒反撃{reaction.damage}（毒{reaction.poisonTurns}ターン）" : $"反撃{reaction.damage}";
                if (reaction.effectType == BattleEffectType.BleedingShield)
                    label = $"出血の盾 {reaction.damage}（発動ごと−{reaction.decayPerTrigger}）";
                else if (reaction.effectType == BattleEffectType.WeakeningShield)
                    label = $"弱体の盾（攻撃力{SignedDirection(reaction.damage)}／{reaction.debuffDuration}ターン）";
                else if (reaction.effectType == BattleEffectType.HPDamageRetaliation)
                    label = $"HP被害時の報復{reaction.damage}";
                AppendLine(text, label, reaction.turns);
            }
            foreach (var stance in status.damageTriggeredBonuses)
                AppendLine(text, $"報復体制 HP被害÷{stance.value}（切り上げ／攻撃力アップ{stance.secondaryDuration}ターン）", stance.remainingTurns);
            foreach (var buff in status.usageBuffs)
            {
                string label;
                if (buff.effect.effectType == BattleEffectType.ChainAttackBonus) label = $"連鎖強化 使用枚数×{buff.effect.value}（合計攻撃力アップ {status.ChainAttackBonus}）";
                else if (buff.effect.effectType == BattleEffectType.CategoryElectrocution) label = $"種類連携 感電{buff.effect.value}（{CategoryRecord(buff.categoryMask)}）";
                else label = $"{(buff.effect.effectType == BattleEffectType.EveryCardsDraw ? "循環" : "コスト還元")}: {buff.progress}/{buff.effect.value}枚";
                AppendLine(text, label, buff.turns);
            }
            if (status.LightweightReduction > 0)
            {
                if (text.Length > 0) text.Append('\n');
                int minimum = int.MaxValue; foreach (int remaining in status.lightweightRemaining) minimum = Mathf.Min(minimum, remaining);
                text.Append($"軽量化 次の{minimum}枚を−{status.LightweightReduction}");
            }
            if (status.nextTurnCost > 0) AppendLine(text, $"次ターンコスト追加 {status.nextTurnCost}", 1);
            if (status.preserveCost) AppendLine(text, "残りコスト温存", status.preserveCostTurns);
            foreach (int threshold in status.adversityThresholds)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append($"逆境 HP{threshold}以下でコスト-1").Append(playerStatus.currentHP <= threshold ? "（有効）" : "（待機）");
            }
        }
        statusText.text = text.ToString();
    }

    private void EnsureStatusText()
    {
        if (statusText != null) return;
        GameObject obj = new GameObject("StatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -6f);
        rect.sizeDelta = new Vector2(0f, 140f);
        statusText = obj.GetComponent<TextMeshProUGUI>();
        if (hpText != null) statusText.font = hpText.font;
        statusText.fontSize = hpText != null ? Mathf.Max(14f, hpText.fontSize * 0.65f) : 18f;
        statusText.color = Color.white;
        statusText.alignment = TextAlignmentOptions.Top;
        statusText.enableWordWrapping = false;
        statusText.raycastTarget = false;
    }

    private static void AppendLine(StringBuilder text, string label, int turns)
    {
        if (text.Length > 0) text.Append('\n');
        text.Append(label).Append("、あと").Append(turns).Append("ターン");
    }

    private static string GetModifierLabel(TimedModifier modifier)
    {
        int value = modifier.value;
        switch (modifier.effectType)
        {
            case BattleEffectType.CardDamageBonus: return $"攻撃力{SignedDirection(value)}";
            case BattleEffectType.CardPoisonDamageBonus: return $"毒ダメージ{SignedDirection(value)}";
            case BattleEffectType.CardPoisonDurationBonus: return $"毒の継続ターン{SignedDirection(value)}";
            case BattleEffectType.OutgoingDamageModifier: return $"攻撃力{SignedDirection(value)}";
            case BattleEffectType.IncomingDamageModifier: return $"受けるダメージ{SignedDirection(value)}";
            case BattleEffectType.IncomingCardDamageBonus: return $"カードから受けるダメージ{SignedDirection(value)}";
            case BattleEffectType.OutgoingPoisonDurationModifier: return $"付与する毒の継続ターン{SignedDirection(value)}";
            case BattleEffectType.CardCostModifier: return $"カードの使用コスト{SignedDirection(value)}";
            case BattleEffectType.CardElectrocutionBonus: return $"カードの感電値{SignedDirection(value)}";
            case BattleEffectType.BlockGainModifier: return $"ブロック獲得量{SignedDirection(value)}";
            case BattleEffectType.DeepWound: return $"攻撃後に出血{value}（発動ごと−{modifier.decayPerTrigger}）";
            case BattleEffectType.CursedAttacks: return $"攻撃後に攻撃力ダウン {value}（{modifier.secondaryDuration}ターン）";
            default: return $"{modifier.effectType} {value}";
        }
    }

    private static string SignedDirection(int value)
    {
        return value >= 0 ? $"アップ {value}" : $"ダウン {Mathf.Abs(value)}";
    }
    private static string CategoryRecord(int mask)
    {
        var names = new System.Collections.Generic.List<string>();
        foreach (CardCategory category in System.Enum.GetValues(typeof(CardCategory)))
            if ((mask & (1 << (int)category)) != 0) names.Add(EnemyIntentForecast.CategoryName(category));
        return names.Count == 0 ? "未使用" : string.Join("・", names);
    }
}
