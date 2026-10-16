using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum BattleTurnState { None, PlayerIntro, Player, Enemy, Won, Lost }

public class BattleManager : MonoBehaviour
{
    [Header("各種マネージャー")]
    public DeckManager deckManager;
    public EnemyManager enemyManager;
    public PlayerStatus playerStatus;
    public PlayerVisualManager playerVisualManager;
    public int SelectedPackIndex { get; set; }
    public BattleHandUI handUI;
    public GameBalanceData balanceData;
    [HideInInspector] public BattleProgressionUI progressionUI;

    [Header("バトルUI")]
    public TextMeshProUGUI turnText;
    public TextMeshProUGUI intentText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI deckCountText;
    public TextMeshProUGUI discardCountText;
    public Button endTurnButton;

    [Header("表示時間")]
    public float turnBannerDuration = 0.8f;
    public float enemyActionDelay = 0.8f;

    [Header("進行状況")]
    [Range(1, 10)] public int currentBattleCount = 1;
    public UnityEvent onBattleWon;
    public UnityEvent onBattleLost;

    private readonly List<CardInstance> drawPile = new List<CardInstance>();
    private readonly List<CardInstance> discardPile = new List<CardInstance>();
    private readonly List<CardInstance> hand = new List<CardInstance>();
    private HealthBarUI[] healthBars;
    private int enemyActionsRemaining;
    private bool cardsDrawnDuringPlay;

    public IReadOnlyList<CardInstance> DrawPile => drawPile;
    public IReadOnlyList<CardInstance> DiscardPile => discardPile;
    public IReadOnlyList<CardInstance> Hand => hand;
    public BattleTurnState State { get; private set; }
    public bool IsPaused { get; set; }
    public bool CanPlayerAct => !IsPaused && State == BattleTurnState.Player && (handUI == null || !handUI.IsAnimating);

    private int MaxCost => balanceData != null ? balanceData.maxCost : 3;
    private int HandSize => balanceData != null ? balanceData.handDrawCount : 5;

    private void Awake()
    {
        if (endTurnButton != null) endTurnButton.onClick.AddListener(EndPlayerTurn);
        healthBars = FindObjectsOfType<HealthBarUI>(true);
        var damageUI = GetComponent<DamagePopupUI>();
        if (damageUI == null) damageUI = gameObject.AddComponent<DamagePopupUI>();
        damageUI.Initialize(this);
        RefreshUI();
    }

    public void StartBattle()
    {
        StopAllCoroutines();
        if (!ValidateSetup()) return;
        GetComponent<DamagePopupUI>()?.Clear();

        if (handUI != null) handUI.ResetForNewBattle();

        enemyManager.SpawnEnemy(currentBattleCount);
        if (enemyManager.CurrentEnemy == null) return;

        GameAudio.PlayBattle(enemyManager.CurrentEnemy.Data.enemyType == EnemyType.Boss);

        if (playerVisualManager != null) playerVisualManager.SpawnPlayer(SelectedPackIndex, playerStatus);

        RefreshHealthBarsImmediate();

        playerStatus.ResetForBattle();
        deckManager.ResetCardsForBattle();
        drawPile.Clear();
        discardPile.Clear();
        hand.Clear();
        drawPile.AddRange(deckManager.currentDeck);
        State = BattleTurnState.None;
        BattleCameraMotion.Begin(this);
        StartCoroutine(PlayerTurnRoutine());
    }

    private IEnumerator PlayerTurnRoutine()
    {
        State = BattleTurnState.PlayerIntro;
        SetInput(false);
        enemyActionsRemaining = 0;
        enemyManager.CurrentEnemy.BeginPlayerTurn();
        playerStatus.cardStatus.BeginPlayerTurn();
        playerStatus.currentCost = MaxCost + playerStatus.cardStatus.nextTurnCost;
        playerStatus.cardStatus.nextTurnCost = 0;
        int drawCount = HandSize + playerStatus.cardStatus.BeginTurnDrawBonus();
        ShowBanner("プレイヤーのターン");
        RefreshIntent();
        RefreshUI();
        yield return new WaitForSeconds(turnBannerDuration);
        if (turnText != null) turnText.text = string.Empty;

        DrawCards(drawCount);
        if (handUI != null) yield return handUI.ShowDrawnCards(hand);
        State = BattleTurnState.Player;
        SetInput(true);
        RefreshUI();
    }

    private void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (drawPile.Count == 0) RecycleDiscardPile();
            if (drawPile.Count == 0) break;

            int index = Random.Range(0, drawPile.Count);
            CardInstance card = drawPile[index];
            drawPile.RemoveAt(index);
            card.RollPerformance();
            hand.Add(card);
        }
        RefreshUI();
    }

    private void RecycleDiscardPile()
    {
        for (int i = discardPile.Count - 1; i >= 0; i--)
        {
            if (discardPile[i].RemainsInDiscard) continue;
            drawPile.Add(discardPile[i]);
            discardPile.RemoveAt(i);
        }
    }

    public bool TryPlayCard(CardInstance card)
    {
        if (!CanPlayerAct || card == null || !hand.Contains(card) || !card.IsPerformanceDetermined) return false;
        card.CostReduction = playerStatus.CardCostReduction;
        if (card.EffectiveCost > playerStatus.currentCost) return false;

        int paidCost = card.EffectiveCost;
        playerStatus.currentCost -= paidCost;
        playerStatus.cardStatus.ConsumeLightweight();
        var existingBuffs = playerStatus.cardStatus.BeginCardUse();
        State = BattleTurnState.PlayerIntro;
        SetInput(false);
        ApplyCardEffects(card);
        card.RecordPlayed();
        enemyManager.CurrentEnemy.RecordPlayerCard(card.Data.category);
        hand.Remove(card);
        discardPile.Add(card);
        if (handUI != null) handUI.NotifyCardPlayed(card);
        int additionalDraws = playerStatus.cardStatus.CompleteCardUse(card.Data.category, paidCost, existingBuffs, playerStatus, enemyManager.CurrentEnemy);
        int previousHandSize = hand.Count;
        if (additionalDraws > 0) DrawCards(additionalDraws);
        cardsDrawnDuringPlay = hand.Count > previousHandSize;
        RefreshUI();
        if (Application.isPlaying) StartCoroutine(ResolvePlayedCard());
        return true;
    }

    private IEnumerator ResolvePlayedCard()
    {
        yield return AnimateHealthBars();
        yield return WaitForEnemyAnimation();
        if (playerStatus.IsDead)
        {
            yield return FinishBattle(false);
            yield break;
        }
        if (enemyManager.CurrentEnemy == null || enemyManager.CurrentEnemy.IsDead)
        {
            yield return FinishBattle(true);
            yield break;
        }
        if (cardsDrawnDuringPlay && handUI != null) yield return handUI.ShowAdditionalCards(hand);
        cardsDrawnDuringPlay = false;
        State = BattleTurnState.Player;
        SetInput(true);
    }

    private void ApplyCardEffects(CardInstance card)
    {
        if (card.CurrentPerformance.effects == null) return;
        var effects = card.CurrentPerformance.effects;
        for (int i = 0; i < effects.Count; i++)
        {
            if (playerStatus.IsDead || enemyManager.CurrentEnemy.IsDead) break;
            BattleEffect effect = effects[i];
            if (effect.effectType == BattleEffectType.PoisonShield && i + 1 < effects.Count
                && effects[i + 1].effectType == BattleEffectType.PoisonShieldPayload)
            {
                BattleEffect poison = effects[++i];
                if (effect.duration > 0)
                    playerStatus.cardStatus.reactions.Add(new PlayerCardStatus.Reaction
                    { turns = effect.duration, damage = poison.value, poisonTurns = poison.duration });
            }
            else if (effect.effectType == BattleEffectType.LowHPBlock && i + 1 < effects.Count
                && effects[i + 1].effectType == BattleEffectType.ConditionalBlockPayload)
            {
                BattleEffect block = effects[++i];
                if (effect.value > 0 && (long)playerStatus.currentHP * effect.value <= playerStatus.maxHP)
                    GainPlayerCardBlock(block.value);
            }
            else ApplyEffect(effect, true, card);
        }
    }

    private void GainPlayerCardBlock(int amount)
    {
        playerStatus.GainBlock(Mathf.Max(0, amount + playerStatus.cardStatus.BlockBonus));
    }

    private void ReactToEnemyAttack(int hpDamage = 0)
    {
        EnemyInstance enemy = enemyManager.CurrentEnemy;
        foreach (var reaction in playerStatus.cardStatus.reactions)
        {
            if (enemy.IsDead) break;
            if (reaction.effectType == BattleEffectType.BleedingShield) enemy.AddBleeding(reaction.damage, reaction.decayPerTrigger);
            else if (reaction.effectType == BattleEffectType.WeakeningShield)
                enemy.modifiers.Add(BattleEffectType.OutgoingDamageModifier, reaction.damage, reaction.debuffDuration, true);
            else if (reaction.effectType == BattleEffectType.HPDamageRetaliation)
            {
                if (hpDamage > 0) enemy.TakeDamage(reaction.damage);
            }
            else if (reaction.poisonTurns > 0) enemy.AddPoison(reaction.poisonTurns, reaction.damage);
            else enemy.TakeDamage(reaction.damage, popupKind: DamagePopupKind.Thorns);
        }
    }

    private void ApplyEffect(BattleEffect effect, bool sourceIsPlayer, CardInstance card = null)
    {
        PlayerStatus player = playerStatus;
        EnemyInstance enemy = enemyManager.CurrentEnemy;
        bool targetPlayer = sourceIsPlayer ? effect.target == EffectTarget.Self : effect.target == EffectTarget.Enemy;
        BattleModifiers sourceModifiers = sourceIsPlayer ? player.modifiers : enemy.modifiers;
        BattleModifiers targetModifiers = targetPlayer ? player.modifiers : enemy.modifiers;

        switch (effect.effectType)
        {
            case BattleEffectType.Damage:
            case BattleEffectType.BlockDamage:
            case BattleEffectType.ConsumeBlockDamage:
            case BattleEffectType.LifestealDamage:
            case BattleEffectType.GrowingDamage:
            case BattleEffectType.RandomDamage:
            case BattleEffectType.RemainingHPDamage:
            case BattleEffectType.DebuffCountDamage:
            case BattleEffectType.BleedingBonusDamage:
            case BattleEffectType.ExecutionDamage:
            case BattleEffectType.PlayedCardsDamage:
            case BattleEffectType.SpentCostDamage:
            case BattleEffectType.AlternatingDamage:
            case BattleEffectType.ElectrocutionBonusDamage:
                int baseDamage = sourceIsPlayer ? effect.value : Mathf.RoundToInt(effect.value * enemy.BattleScalingMultiplier);
                switch (effect.effectType)
                {
                    case BattleEffectType.BlockDamage:
                        baseDamage = (sourceIsPlayer ? player.currentBlock : enemy.currentBlock) + effect.value;
                        break;
                    case BattleEffectType.ConsumeBlockDamage:
                        baseDamage = (sourceIsPlayer ? player.currentBlock : enemy.currentBlock) + effect.value;
                        if (sourceIsPlayer) player.currentBlock = 0; else enemy.currentBlock = 0;
                        break;
                    case BattleEffectType.GrowingDamage:
                        baseDamage = card != null ? card.GetBaseValue(effect) : effect.value;
                        break;
                    case BattleEffectType.RandomDamage:
                        int minimum = Mathf.Max(0, Mathf.Min(effect.minimumMultiplier, effect.maximumMultiplier));
                        int maximum = Mathf.Max(minimum, Mathf.Max(effect.minimumMultiplier, effect.maximumMultiplier));
                        // intの上限でも「上限+1」がオーバーフローしないようにします。
                        int multiplier = maximum < int.MaxValue
                            ? Random.Range(minimum, maximum + 1)
                            : (int)(minimum + (long)System.Math.Floor(new System.Random().NextDouble() * ((long)maximum - minimum + 1)));
                        baseDamage = effect.value * multiplier;
                        break;
                    case BattleEffectType.RemainingHPDamage:
                        if (effect.value <= 0)
                        {
                            Debug.LogWarning("残りHP割合攻撃のValue（分母）は1以上にしてください。");
                            return;
                        }
                        baseDamage = (targetPlayer ? player.currentHP : enemy.currentHP) / effect.value;
                        break;
                    case BattleEffectType.DebuffCountDamage:
                        baseDamage = enemy.modifiers.DebuffCount * effect.value;
                        break;
                    case BattleEffectType.BleedingBonusDamage:
                        baseDamage = effect.value + enemy.bleeding.TotalValue * effect.secondaryValue;
                        break;
                    case BattleEffectType.ExecutionDamage:
                        baseDamage = effect.value;
                        if (effect.secondaryValue > 0 && (long)enemy.currentHP * effect.secondaryValue <= enemy.maxHP)
                            baseDamage += effect.tertiaryValue;
                        break;
                    case BattleEffectType.PlayedCardsDamage: baseDamage = player.cardStatus.CardsPlayedThisTurn * effect.value; break;
                    case BattleEffectType.SpentCostDamage: baseDamage = player.cardStatus.CostSpentThisTurn * effect.value; break;
                    case BattleEffectType.AlternatingDamage:
                        baseDamage = effect.value + (player.cardStatus.PreviousCategory.HasValue && player.cardStatus.PreviousCategory.Value != CardCategory.Attack ? effect.secondaryValue : 0);
                        break;
                    case BattleEffectType.ElectrocutionBonusDamage:
                        baseDamage = effect.value + enemy.electrocution.TotalValue * effect.secondaryValue; break;
                }
                int nextAttackReduction = !sourceIsPlayer && targetPlayer ? enemy.modifiers.ConsumeNextAttackReduction() : 0;
                int damage = Mathf.Max(0, baseDamage + sourceModifiers.outgoingDamageModifier
                    + (sourceIsPlayer ? sourceModifiers.cardDamageBonus + player.cardStatus.ChainAttackBonus : enemy.PermanentAttackBonus) - nextAttackReduction);
                int receivedHPDamage = 0;
                if (targetPlayer)
                {
                    int hpDamage = player.TakeDamage(damage, popupKind: sourceIsPlayer ? DamagePopupKind.SelfDamage : DamagePopupKind.Attack);
                    receivedHPDamage = hpDamage;
                    // 攻撃の命中時に吸血し、その後に反撃を解決します。毒やブロック吸収分は含みません。
                    if (!sourceIsPlayer && effect.effectType == BattleEffectType.LifestealDamage && !enemy.IsDead)
                        enemy.Heal(hpDamage * Mathf.Clamp(effect.duration, 0, 100) / 100);
                }
                else enemy.TakeDamage(damage, isCard: card != null, popupKind: sourceIsPlayer ? DamagePopupKind.Attack : DamagePopupKind.SelfDamage);
                // 直接攻撃の各ヒット後。状態異常ダメージはこの経路を通さず再発動しません。
                if (sourceIsPlayer && !targetPlayer)
                {
                    if (card != null && card.Data.category == CardCategory.Attack) enemy.TriggerElectrocution();
                    player.TriggerBleeding();
                }
                else if (!sourceIsPlayer && targetPlayer)
                {
                    player.TriggerElectrocution();
                    enemy.TriggerBleeding();
                    enemy.OnAttackCompleted();
                    player.cardStatus.OnEnemyHPDamage(player, receivedHPDamage);
                    ReactToEnemyAttack(receivedHPDamage);
                }
                break;
            case BattleEffectType.ApplyBleeding:
                if (targetPlayer) player.AddBleeding(effect.value, effect.decayPerTrigger);
                else enemy.AddBleeding(effect.value, effect.decayPerTrigger);
                break;
            case BattleEffectType.ApplyElectrocution:
                int shockValue = Mathf.Max(0, effect.value + (sourceIsPlayer && !targetPlayer && card != null ? sourceModifiers.cardElectrocutionBonus : 0));
                if (targetPlayer) player.AddElectrocution(shockValue, effect.decayPerTrigger);
                else enemy.AddElectrocution(shockValue, effect.decayPerTrigger);
                break;
            case BattleEffectType.PlayedCardsCostRecovery:
                if (player.cardStatus.CardsPlayedThisTurn >= effect.value) player.currentCost += Mathf.Max(0, effect.secondaryValue);
                break;
            case BattleEffectType.PlayedCardsBlock: GainPlayerCardBlock(player.cardStatus.CardsPlayedThisTurn * effect.value); break;
            case BattleEffectType.SpentCostBlock: GainPlayerCardBlock(player.cardStatus.CostSpentThisTurn * effect.value); break;
            case BattleEffectType.PlayedCardsConditionalBlock:
                if (player.cardStatus.CardsPlayedThisTurn >= effect.value) GainPlayerCardBlock(effect.secondaryValue);
                break;
            case BattleEffectType.AmplifyElectrocution: enemy.electrocution.Amplify(effect.value); break;
            case BattleEffectType.ReduceElectrocutionDecay: enemy.electrocution.ReduceDecay(effect.value); break;
            case BattleEffectType.LightweightCards:
                if (effect.value > 0) player.cardStatus.lightweightRemaining.Add(effect.value);
                break;
            case BattleEffectType.ChainAttackBonus:
            case BattleEffectType.EveryCardsDraw:
            case BattleEffectType.EveryCardsCostRecovery:
            case BattleEffectType.CategoryElectrocution: player.cardStatus.AddUsageBuff(effect); break;
            case BattleEffectType.DebuffCountBlock:
                GainPlayerCardBlock(enemy.modifiers.DebuffCount * effect.value);
                break;
            case BattleEffectType.AmplifyBleeding:
                enemy.bleeding.Amplify(effect.value);
                break;
            case BattleEffectType.DebuffCountCardBonus:
                player.modifiers.Add(BattleEffectType.CardDamageBonus, enemy.modifiers.DebuffCount * effect.value, effect.duration);
                break;
            case BattleEffectType.NextAttackReduction:
                targetModifiers.Add(effect.effectType, Mathf.Max(0, effect.value), int.MaxValue);
                break;
            case BattleEffectType.BlockGainModifier:
            case BattleEffectType.DeepWound:
            case BattleEffectType.CursedAttacks:
                targetModifiers.Add(effect);
                break;
            case BattleEffectType.ReduceBuffs:
                enemy.ReduceBuffs(effect.value);
                break;
            case BattleEffectType.BleedingShield:
            case BattleEffectType.WeakeningShield:
            case BattleEffectType.HPDamageRetaliation:
                if (effect.duration > 0)
                    player.cardStatus.reactions.Add(new PlayerCardStatus.Reaction { effectType = effect.effectType, turns = effect.duration,
                        damage = effect.value, decayPerTrigger = effect.decayPerTrigger, debuffDuration = effect.secondaryDuration });
                break;
            case BattleEffectType.DamageTriggeredCardBonus:
                player.cardStatus.AddTimed(player.cardStatus.damageTriggeredBonuses, effect);
                break;
            case BattleEffectType.GainBlock:
                if (targetPlayer && card != null) GainPlayerCardBlock(effect.value);
                else if (targetPlayer) player.GainBlock(effect.value);
                else enemy.GainBlock(effect.value);
                break;
            case BattleEffectType.PermanentAttackBonus:
                if (!sourceIsPlayer && !targetPlayer) enemy.GainPermanentAttack(effect.value);
                break;
            case BattleEffectType.CardBlockBonus:
                player.cardStatus.AddTimed(player.cardStatus.blockBonuses, effect);
                break;
            case BattleEffectType.Thorns:
                if (effect.duration > 0)
                    player.cardStatus.reactions.Add(new PlayerCardStatus.Reaction { turns = effect.duration, damage = effect.value });
                break;
            case BattleEffectType.BuffToBlock:
                int convertibleBonus = player.modifiers.PositiveAttackBonus + Mathf.Max(0, player.cardStatus.ChainAttackBonus);
                if (convertibleBonus > 0) GainPlayerCardBlock(convertibleBonus);
                break;
            case BattleEffectType.EndTurnBlock:
                player.cardStatus.AddTimed(player.cardStatus.endTurnBlocks, effect);
                break;
            case BattleEffectType.NextTurnCost:
                player.cardStatus.nextTurnCost += effect.value;
                break;
            case BattleEffectType.ExtraDraw:
                player.cardStatus.AddTimed(player.cardStatus.extraDraws, effect);
                break;
            case BattleEffectType.AmplifyPoison:
                enemy.poison.Amplify(effect.value);
                break;
            case BattleEffectType.PreserveCost:
                player.cardStatus.PreserveCostFor(effect.duration);
                break;
            case BattleEffectType.Adversity:
                player.cardStatus.adversityThresholds.Add(effect.value);
                break;
            case BattleEffectType.Heal:
                if (targetPlayer) player.Heal(effect.value); else enemy.Heal(effect.value);
                break;
            case BattleEffectType.RemoveBlock:
                if (targetPlayer) player.currentBlock = 0; else enemy.currentBlock = 0;
                break;
            case BattleEffectType.ReduceBlock:
                if (targetPlayer) player.currentBlock = Mathf.Max(0, player.currentBlock - Mathf.Max(0, effect.value));
                else enemy.currentBlock = Mathf.Max(0, enemy.currentBlock - Mathf.Max(0, effect.value));
                break;
            case BattleEffectType.ApplyPoison:
                int poisonDamage = Mathf.Max(0, effect.value + (sourceIsPlayer ? sourceModifiers.cardPoisonDamageBonus : 0));
                int turns = Mathf.Max(0, effect.duration + sourceModifiers.outgoingPoisonDurationModifier + (sourceIsPlayer ? sourceModifiers.cardPoisonDurationBonus : 0));
                if (targetPlayer) player.AddPoison(turns, poisonDamage); else enemy.AddPoison(turns, poisonDamage);
                break;
            case BattleEffectType.CardDamageBonus:
            case BattleEffectType.CardPoisonDamageBonus:
            case BattleEffectType.CardPoisonDurationBonus:
            case BattleEffectType.OutgoingDamageModifier:
            case BattleEffectType.IncomingDamageModifier:
            case BattleEffectType.OutgoingPoisonDurationModifier:
            case BattleEffectType.IncomingCardDamageBonus:
            case BattleEffectType.CardCostModifier:
            case BattleEffectType.CardElectrocutionBonus:
                targetModifiers.Add(effect.effectType, effect.value, effect.duration);
                break;
        }
    }

    public void EndPlayerTurn()
    {
        if (!CanPlayerAct) return;
        enemyActionsRemaining = enemyManager.CurrentEnemy.ActionsPerTurn;
        State = BattleTurnState.Enemy;
        SetInput(false);
        StartCoroutine(EndPlayerTurnRoutine());
    }

    private void ApplyEnemyAction(EnemyActionData action)
    {
        EnemyInstance enemy = enemyManager.CurrentEnemy;
        foreach (BattleEffect effect in enemy.ResolveEffects(action))
        {
            if (playerStatus.IsDead || enemy.IsDead) break;
            ApplyEffect(effect, false);
        }
    }

    private IEnumerator EndPlayerTurnRoutine()
    {
        discardPile.AddRange(hand);
        hand.Clear();
        if (handUI != null) yield return handUI.DiscardAll();
        playerStatus.cardStatus.EndPlayerTurn(playerStatus);
        playerStatus.TickModifiers();
        enemyManager.CurrentEnemy.TickPoison();
        RefreshUI();
        yield return AnimateHealthBars();
        yield return WaitForEnemyAnimation();
        if (enemyManager.CurrentEnemy.IsDead) { yield return FinishBattle(true); yield break; }

        ShowBanner("敵のターン");
        yield return new WaitForSeconds(turnBannerDuration);
        EnemyInstance actingEnemy = enemyManager.CurrentEnemy;
        for (int actionNumber = 0; actionNumber < actingEnemy.ActionsPerTurn; actionNumber++)
        {
            if (playerStatus.IsDead || actingEnemy.IsDead) break;
            EnemyActionData action = actingEnemy.GetNextAction();
            if (action == null) break;
            if (turnText != null) turnText.text = actingEnemy.ActionsPerTurn > 1
                ? $"{action.actionName}（{actionNumber + 1}/{actingEnemy.ActionsPerTurn}）" : action.actionName;
            yield return new WaitForSeconds(enemyActionDelay);
            ApplyEnemyAction(action);
            actingEnemy.AdvanceActionIndex();
            enemyActionsRemaining--;
            RefreshUI();
            yield return AnimateHealthBars();
            yield return WaitForEnemyAnimation();
            if (playerStatus.IsDead) { yield return FinishBattle(false); yield break; }
            if (actingEnemy.IsDead) { yield return FinishBattle(true); yield break; }
        }

        playerStatus.TickPoison();
        playerStatus.cardStatus.EndEnemyTurn();
        enemyManager.CurrentEnemy.TickModifiers();
        RefreshUI();
        yield return AnimateHealthBars();
        if (playerStatus.IsDead) { yield return FinishBattle(false); yield break; }
        if (enemyManager.CurrentEnemy.IsDead) { yield return FinishBattle(true); yield break; }
        yield return new WaitForSeconds(enemyActionDelay);
        StartCoroutine(PlayerTurnRoutine());
    }

    private IEnumerator FinishBattle(bool won)
    {
        State = won ? BattleTurnState.Won : BattleTurnState.Lost;
        SetInput(false);
        if (!won && playerVisualManager != null)
        {
            playerVisualManager.PlayDeath();
            while (playerVisualManager.CurrentPlayer != null && playerVisualManager.CurrentPlayer.IsAnimatingDeath)
                yield return null;
        }
        yield return WaitForEnemyAnimation();
        if (turnText != null) turnText.text = won ? "勝利！" : "敗北";
        yield return null;
        BattleCameraMotion.End(this);
        var owner = FindObjectOfType<GameManager>();
        if (owner != null) owner.RecordRunFinished(won,
            enemyManager != null && enemyManager.CurrentEnemy != null && enemyManager.CurrentEnemy.Data != null
                ? enemyManager.CurrentEnemy.Data.enemyName : "不明な敵");
        if (enemyManager != null) enemyManager.DespawnCurrentEnemy();
        if (playerVisualManager != null) playerVisualManager.DespawnPlayer();
        if (won) onBattleWon?.Invoke(); else onBattleLost?.Invoke();
        if (progressionUI != null) progressionUI.HandleBattleFinished(won);
    }

    private IEnumerator WaitForEnemyAnimation()
    {
        while (enemyManager != null && enemyManager.CurrentEnemy != null && enemyManager.CurrentEnemy.IsAnimating)
            yield return null;
    }

    public void CancelBattle()
    {
        BattleCameraMotion.End(this);
        GameAudio.PlayTitle();
        StopAllCoroutines();
        State = BattleTurnState.None;
        SetInput(false);
        if (playerVisualManager != null) playerVisualManager.DespawnPlayer();
        if (enemyManager != null) enemyManager.DespawnCurrentEnemy();
    }

    private void RefreshIntent()
    {
        if (intentText != null)
        {
            intentText.enableAutoSizing = true;
            intentText.fontSizeMin = 14;
            intentText.fontSizeMax = 22;
            intentText.enableWordWrapping = true;
            bool resolving = State == BattleTurnState.Enemy;
            intentText.text = resolving && enemyActionsRemaining == 0 ? "敵の行動完了"
                : EnemyIntentForecast.Create(enemyManager.CurrentEnemy, playerStatus, !resolving,
                    resolving ? enemyActionsRemaining : -1,
                    resolving && enemyManager.CurrentEnemy != null ? enemyManager.CurrentEnemy.ActionsPerTurn - enemyActionsRemaining + 1 : 1).text;
        }
    }

    private void ShowBanner(string value) { if (turnText != null) turnText.text = value; }

    public IEnumerator AnimateHealthBars()
    {
        if (healthBars == null || healthBars.Length == 0)
            healthBars = FindObjectsOfType<HealthBarUI>(true);
        foreach (HealthBarUI bar in healthBars)
            if (bar != null && bar.gameObject.activeInHierarchy) bar.AnimateToCurrent();

        bool animating;
        do
        {
            animating = false;
            foreach (HealthBarUI bar in healthBars)
                if (bar != null && bar.gameObject.activeInHierarchy && bar.IsAnimating)
                {
                    animating = true;
                    break;
                }
            if (animating) yield return null;
        } while (animating);
    }

    private void RefreshHealthBarsImmediate()
    {
        if (healthBars == null || healthBars.Length == 0)
            healthBars = FindObjectsOfType<HealthBarUI>(true);
        foreach (HealthBarUI bar in healthBars)
            if (bar != null) bar.RefreshImmediate();
    }

    private void SetInput(bool enabled)
    {
        if (handUI != null) handUI.SetInteractable(enabled);
        if (endTurnButton != null) endTurnButton.interactable = enabled;
    }

    private void RefreshUI()
    {
        if (playerStatus != null && deckManager != null)
        {
            int reduction = playerStatus.CardCostReduction;
            foreach (var card in deckManager.currentDeck) card.CostReduction = reduction;
            if (handUI != null) handUI.RefreshCards();
        }
        if (costText != null) costText.text = $"{playerStatus?.currentCost ?? 0} / {MaxCost}";
        if (enemyManager != null) RefreshIntent();
        if (deckCountText != null) deckCountText.text = drawPile.Count.ToString();
        if (discardCountText != null) discardCountText.text = discardPile.Count.ToString();
        if (healthBars != null)
            foreach (HealthBarUI bar in healthBars)
                if (bar != null) bar.RefreshStatus();
    }

    private bool ValidateSetup()
    {
        if (deckManager != null && enemyManager != null && playerStatus != null) return true;
        Debug.LogError("BattleManagerの必須参照（DeckManager / EnemyManager / PlayerStatus）が未設定です。");
        return false;
    }
}
